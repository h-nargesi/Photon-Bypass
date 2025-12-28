using PhotonBypass.Application.Billing.Model;
using PhotonBypass.Application.Plan;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Business;
using PhotonBypass.Result;
using PhotonBypass.Tools;
using Serilog;

namespace PhotonBypass.Application.Billing;

class BillingApplication(
    IWalletRepository wallet_repo,
    Lazy<IRenewalRepository> renewal_repo,
    Lazy<IHistoryRepository> history_repo,
    Lazy<IPlanApplication> plan_app,
    Lazy<IJobContext> job_context) 
    : IBillingApplication
{
    private IWalletRepository WalletRepo { get; } = wallet_repo;
    private Lazy<IRenewalRepository> RenewalRepo { get; } = renewal_repo;
    private Lazy<IHistoryRepository> HistoryRepo { get; } = history_repo;
    private Lazy<IPlanApplication> PlanApp { get; } = plan_app;
    private Lazy<IJobContext> JobContext { get; } = job_context;

    public async Task<ApiResult<string?>> GenerateInvoiceCode(NewInvoiceInfo? new_info)
    {
        var invoice = await GenerateInvoice(new_info);

        if (invoice == null)
        {
            return ApiResult<string?>.Success(null);
        }

        return ApiResult<string?>.Success($"I{invoice.Value.Code}");
    }

    public async Task<ApiResult<InvoiceModel?>> GetInvoice(string? code)
    {
        List<WalletEntity>? items;

        if (code == null)
        {
            var invoice = await GenerateInvoice(null);
            if (invoice.HasValue)
            {
                items = invoice.Value.Items;
                code = invoice.Value.Code;
            }
            else items = null;
        }
        else
        {
            items = await WalletRepo.GetInvoice(code);
        }

        if (code == null || items == null || items.Count < 1)
        {
            return ApiResult<InvoiceModel?>.Success(null);
        }

        var result = new InvoiceModel
        {
            Code = code,
            Status = items.First().Status,
            InvoiceItems = items
                .Select(i => new InvoiceItemModel
                {
                    Title = i.Description,
                    Value = i.Amount,
                })
                .ToArray(),
        };

        return ApiResult<InvoiceModel?>.Success(result);
    }

    public async Task<ApiResult<BalanceStatus>> PaymentCallback(string token)
    {
        var invoice_items = await WalletRepo.GetInvoice(token);

        if (invoice_items.Count < 1)
        {
            Log.Fatal("PaymentCallback with no invoice: token={0}", token);

            return ApiResult<BalanceStatus>.Failed(BalanceStatus.Failed);
        }

        // TODO: check token

        invoice_items.ForEach(i => i.Status = BalanceStatus.Completed);
        await WalletRepo.Save(invoice_items);

        var actions = invoice_items.Where(i => i.Action != null)
             .Select(i => (i.AccountId, i.Id, i.Action));

        foreach (var action in actions)
        {
            if (string.IsNullOrEmpty(action.Action)) continue;

            Log.Information(@"Save Renewal: account-id={AccountId}, wallet-id={Id}, action={Action}", action);

            await PlanApp.Value.Renewal(action.AccountId, action.Id, action.Action);
        }

        return ApiResult<BalanceStatus>.Success(BalanceStatus.Completed);
    }

    private async Task<(string Code, List<WalletEntity> Items)?> GenerateInvoice(NewInvoiceInfo? new_info)
    {
        if (JobContext.Value.AccountId == null)
        {
            throw new Exception("Account-Id is not set in job-context!");
        }

        var account_id = JobContext.Value.AccountId.Value;

        var current_balance = await WalletRepo.GetBalance(account_id);

        if (current_balance < 0)
        {
            return null;
        }

        var payments = await WalletRepo.GetNotPaid(account_id);

        var not_paid_renewal = await RenewalRepo.Value.GetNotPaid(account_id);

        var not_paid_renewal_values = await WalletRepo.GetWalletsAmount(not_paid_renewal.Select(r => r.WalletDebit));

        if (payments.Count < 0 && not_paid_renewal.Count < 1 && new_info == null)
        {
            return null;
        }

        int invocie_code;
        await WalletRepo.DbContext.BeginTransactionAsync();

        try
        {
            if (not_paid_renewal.Count > 0)
            {
                var saving_wallet = not_paid_renewal
                    .Select(not_paid => new WalletEntity
                    {
                        AccountId = account_id,
                        Amount = not_paid_renewal_values[not_paid.WalletDebit],
                        Description = not_paid.GetPlanTitle(),
                        Direction = BalanceDirection.Credit,
                        RenewId = not_paid.Id,
                    })
                    .ToList();

                if (saving_wallet.Count > 0)
                {
                    await WalletRepo.Save(saving_wallet);

                    payments.AddRange(saving_wallet);
                }
            }

            if (new_info != null)
            {
                payments.Add(new WalletEntity
                {
                    AccountId = account_id,
                    Amount = new_info.Price,
                    Description = new_info.Descripttion,
                    Direction = BalanceDirection.Credit,
                    Action = new_info.Action,
                });
            }

            invocie_code = payments
                .Where(c => c.InvoiceCode.HasValue)
                .Select(i => i.InvoiceCode)
                .FirstOrDefault() ??
                await WalletRepo.GenerateNewInvoiceCode();

            payments.Where(p => p.InvoiceCode != invocie_code)
                .Foreach(p => p.InvoiceCode = invocie_code);
            
            await WalletRepo.Save(payments);

            foreach (var item in payments)
            {
                Log.Information(@"Generate Invoice: Id={Id}, Amount={Amount}, Direction={Direction}, InvoiceCode={InvoiceCode}, Status={Status},
    Description={Description},
    Action={Action}", 
                    item);
            }

            if (not_paid_renewal.Count > 0)
            {
                var renewals = not_paid_renewal.ToDictionary(i => i.Id);

                var saving_renewals = payments.Where(p => p.RenewId > 0)
                     .Select(p =>
                     {
                         var renewal = renewals[p.RenewId];
                         renewal.WalletCredit = p.Id;
                         return renewal;
                     })
                     .ToList();

                await RenewalRepo.Value.Save(saving_renewals);
            }

            await WalletRepo.DbContext.CommitAsync();

            await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
            {
                Target = account_id,
                Category = EventCategory.Transaction,
                Type = EventType.Information,
                Title = "مالی",
                Description = "فاکتور صادر شد.",
                Price = payments.Sum(p => p.Amount),
            });
        }
        catch
        {
            await WalletRepo.DbContext.RollbackAsync();
            throw;
        }

        return ($"I{invocie_code}", payments);
    }
}
