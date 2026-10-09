using Microsoft.Extensions.Options;
using PhotonBypass.Application.Billing.Model;
using PhotonBypass.Application.Management;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Application.Plan;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Plan;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using PhotonBypass.Tools;
using Serilog;

namespace PhotonBypass.Application.Billing;

class BillingApplication(
    IWalletRepository wallet_repo,
    IInvoiceRepository invoice_repo,
    Lazy<IRenewalRepository> renewal_repo,
    Lazy<IHistoryRepository> history_repo,
    Lazy<IPlanApplication> plan_app,
    Lazy<IWalletThresholdService> wallet_threshold_srv,
    IAccountSettlementGate settlement_gate,
    IOptions<ManagementOptions> options,
    Lazy<IJobContext> job_context)
    : IBillingApplication
{
    private const string WalletDeductionItemTitle = "کسر کیف پول";

    private static readonly string[] ReceiptImageContentTypes = ["image/jpeg", "image/png", "image/webp"];

    private static readonly string[] ReceiptImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    private IWalletRepository WalletRepo { get; } = wallet_repo;
    private IInvoiceRepository InvoiceRepo { get; } = invoice_repo;
    private Lazy<IRenewalRepository> RenewalRepo { get; } = renewal_repo;
    private Lazy<IHistoryRepository> HistoryRepo { get; } = history_repo;
    private Lazy<IPlanApplication> PlanApp { get; } = plan_app;
    private Lazy<IWalletThresholdService> WalletThresholdSrv { get; } = wallet_threshold_srv;
    private IAccountSettlementGate SettlementGate { get; } = settlement_gate;
    private IOptions<ManagementOptions> Options { get; } = options;
    private Lazy<IJobContext> JobContext { get; } = job_context;

    public async Task<ApiResult<int?>> IssueTopUp(int value)
    {
        if (value is < 1 or > 100_000)
        {
            throw new UserException("مبلغ وارد شده معتبر نیست!", $"Invalid pay amount: {value}");
        }

        var account_id = RequireAccountId();

        var code = await InvoiceRepo.GenerateNewInvoiceCode();

        await InvoiceRepo.DbContext.BeginTransactionAsync();

        try
        {
            await InvoiceRepo.CancelPreviousPending(account_id);

            await InvoiceRepo.Insert(new InvoiceEntity
            {
                Code = code,
                AccountId = account_id,
                Kind = InvoiceKind.TopUp,
                Title = "افزایش موجودی",
                TotalPrice = value,
                WalletDeduction = 0,
                Payable = value,
                Status = BalanceStatus.Pending,
            });

            await InvoiceRepo.DbContext.CommitAsync();
        }
        catch
        {
            await InvoiceRepo.DbContext.RollbackAsync();
            throw;
        }

        Log.Information("Top-Up invoice issued: account-id={0}, code={1}, value={2}", account_id, code, value);

        await SaveIssueHistory(account_id, value);

        return ApiResult<int?>.Success(code);
    }

    public async Task<ApiResult<int?>> IssuePlanInvoice(int estimate, string action, string title)
    {
        var account_id = RequireAccountId();

        var balance = await WalletRepo.GetBalance(account_id);

        var wallet_deduction = Math.Max(Math.Min(balance, estimate), 0);
        var payable = estimate - balance;

        var code = await InvoiceRepo.GenerateNewInvoiceCode();

        await InvoiceRepo.DbContext.BeginTransactionAsync();

        try
        {
            await InvoiceRepo.CancelPreviousPending(account_id);

            await InvoiceRepo.Insert(new InvoiceEntity
            {
                Code = code,
                AccountId = account_id,
                Kind = InvoiceKind.Plan,
                Title = title,
                TotalPrice = estimate,
                WalletDeduction = wallet_deduction,
                Payable = payable,
                Action = action,
                Status = BalanceStatus.Pending,
            });

            await InvoiceRepo.DbContext.CommitAsync();
        }
        catch
        {
            await InvoiceRepo.DbContext.RollbackAsync();
            throw;
        }

        Log.Information(
            "Plan invoice issued: account-id={0}, code={1}, estimate={2}, balance={3}, deduction={4}, payable={5}, action={6}",
            account_id, code, estimate, balance, wallet_deduction, payable, action);

        await SaveIssueHistory(account_id, estimate);

        return ApiResult<int?>.Success(code);
    }

    public async Task<ApiResult<InvoiceModel?>> GetInvoice(int code)
    {
        var invoice = await InvoiceRepo.GetByCodeOwner(JobContext.Value.Target, code)
                      ?? throw new UserException("فاکتور مورد نظر پیدا نشد!", $"Invoice not found: code={code}");

        var wallet_balance = await WalletRepo.GetBalance(invoice.AccountId);

        var items = new List<InvoiceItemModel>
        {
            new() { Title = invoice.Title, Value = invoice.TotalPrice },
        };

        if (invoice.WalletDeduction > 0)
        {
            items.Add(new InvoiceItemModel { Title = WalletDeductionItemTitle, Value = -invoice.WalletDeduction });
        }

        var result = new InvoiceModel
        {
            Code = invoice.Code,
            Kind = invoice.Kind,
            Status = invoice.Status,
            TotalPrice = invoice.TotalPrice,
            WalletDeduction = invoice.WalletDeduction,
            Payable = invoice.Payable,
            WalletBalance = wallet_balance,
            AllowWallet = invoice.Kind == InvoiceKind.Plan &&
                          invoice.Status == BalanceStatus.Pending &&
                          wallet_balance >= invoice.TotalPrice,
            HasReceipt = invoice.HasReceipt,
            Items = [.. items],
            CardInfo = [.. Options.Value.PaymentCards ?? []],
        };

        return ApiResult<InvoiceModel?>.Success(result);
    }

    public async Task<ApiResult> RegisterReceipt(int code, byte[]? image, string? file_name, string? content_type, string? text)
    {
        var invoice = await InvoiceRepo.GetByCodeOwner(JobContext.Value.Target, code)
                      ?? throw new UserException("فاکتور مورد نظر پیدا نشد!", $"Invoice not found: code={code}");

        var has_image = image is { Length: > 0 };
        var has_text = !string.IsNullOrEmpty(text);

        if (has_image == has_text)
        {
            throw new UserException("برای ثبت رسید باید تصویر یا متن (فقط یکی) ارسال شود!",
                $"Invalid receipt payload: code={code}, has-image={has_image}, has-text={has_text}");
        }

        if (has_text && text!.Length > 1000)
        {
            throw new UserException("متن رسید بیش از حد طولانی است!", $"Receipt text too long: {text.Length}");
        }

        if (has_image)
        {
            ValidateReceiptImage(image!, file_name, content_type);
        }

        if (invoice.Status != BalanceStatus.Pending || invoice.Payable <= 0)
        {
            throw new UserException("این فاکتور در انتظار ثبت رسید نیست!",
                $"Invoice is not awaiting a receipt: code={code}, status={invoice.Status}, payable={invoice.Payable}");
        }

        var credit = new WalletEntity
        {
            AccountId = invoice.AccountId,
            Amount = invoice.Payable,
            Direction = BalanceDirection.Credit,
            Status = BalanceStatus.Verifying,
            InvoiceCode = invoice.Code,
            Description = invoice.Title,
        };

        await InvoiceRepo.DbContext.BeginTransactionAsync();

        try
        {
            var updated = await InvoiceRepo.RegisterReceipt(invoice.Code, has_image ? image : null, has_text ? text : null);

            if (updated < 1)
            {
                throw new UserException("رسید این فاکتور قبلاً ثبت شده است!",
                    $"Receipt was already registered: code={code}");
            }

            await WalletRepo.Save(credit);

            await InvoiceRepo.DbContext.CommitAsync();
        }
        catch
        {
            await InvoiceRepo.DbContext.RollbackAsync();
            throw;
        }

        Log.Information("Receipt registered: account-id={0}, code={1}, has-image={2}, amount={3}",
            invoice.AccountId, invoice.Code, has_image, invoice.Payable);

        if (invoice.Kind == InvoiceKind.Plan)
        {
            await SettlementGate.RunExclusively(invoice.AccountId, () => RunRenewalAfterReceipt(invoice, credit));
        }

        await WalletThresholdSrv.Value.CheckAndApply();

        await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
        {
            Target = invoice.AccountId,
            Category = EventCategory.Transaction,
            Type = EventType.Information,
            Title = "مالی",
            Description = "رسید ثبت شد.",
            Price = invoice.Payable,
        });

        return ApiResult.Success("رسید ثبت شد و در انتظار تایید است.");
    }

    public async Task<ApiResult> SettleWallet(int code)
    {
        var invoice = await InvoiceRepo.GetByCodeOwner(JobContext.Value.Target, code)
                      ?? throw new UserException("فاکتور مورد نظر پیدا نشد!", $"Invoice not found: code={code}");

        if (invoice.Kind != InvoiceKind.Plan || invoice.Status != BalanceStatus.Pending)
        {
            throw new UserException("این فاکتور قابل تسویه از کیف پول نیست!",
                $"Invoice is not settle-able by wallet: code={code}, kind={invoice.Kind}, status={invoice.Status}");
        }

        return await SettlementGate.RunExclusively(invoice.AccountId, () => SettleWalletCore(invoice, code));
    }

    private async Task<ApiResult> SettleWalletCore(InvoiceEntity invoice, int code)
    {
        var balance = await WalletRepo.GetBalance(invoice.AccountId);

        if (balance < invoice.TotalPrice)
        {
            throw new UserException("موجودی کیف پول برای تسویه این فاکتور کافی نیست!",
                $"Wallet balance is not enough to settle: code={code}, balance={balance}, total-price={invoice.TotalPrice}");
        }

        var updated = await InvoiceRepo.TransitionStatus(invoice.Code, BalanceStatus.Pending, BalanceStatus.Canceled);

        if (updated < 1)
        {
            throw new UserException("این فاکتور قبلاً تسویه شده است!",
                $"Invoice status race lost: code={code}");
        }

        try
        {
            var renewal_result = await PlanApp.Value.Renewal(invoice.AccountId, null, invoice.Action!, invoice.Code);

            if (renewal_result.Code / 100 != 2)
            {
                throw new UserException("تسویه از کیف پول با خطا مواجه شد؛ لطفاً دوباره تلاش کنید!",
                    $"Settlement renewal was unsuccessful: code={invoice.Code}, result-code={renewal_result.Code}");
            }
        }
        catch (UserException)
        {
            await RevertToPending(invoice.Code);
            throw;
        }
        catch (Exception e)
        {
            Log.Error(e, "Settlement renewal failed: account-id={0}, code={1}", invoice.AccountId, invoice.Code);

            await RevertToPending(invoice.Code);

            throw new UserException("تسویه از کیف پول با خطا مواجه شد؛ لطفاً دوباره تلاش کنید!",
                $"Settlement renewal failed: code={invoice.Code}");
        }

        await WalletThresholdSrv.Value.CheckAndApply();

        await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
        {
            Target = invoice.AccountId,
            Category = EventCategory.Transaction,
            Type = EventType.Information,
            Title = "مالی",
            Description = "فاکتور از کیف پول تسویه شد.",
            Price = -invoice.TotalPrice,
        });

        return ApiResult.Success("فاکتور از کیف پول تسویه شد.");
    }

    private async Task RunRenewalAfterReceipt(InvoiceEntity invoice, WalletEntity credit)
    {
        if (credit.Id > 0 && await RenewalRepo.Value.GetByWalletCredit(credit.Id) != null)
        {
            Log.Information("Skip already executed renewal after receipt: code={0}, wallet-id={1}",
                invoice.Code, credit.Id);

            return;
        }

        try
        {
            var renewal_result = await PlanApp.Value.Renewal(invoice.AccountId, credit.Id, invoice.Action!, invoice.Code);

            if (renewal_result.Code / 100 != 2)
            {
                throw new UserException("فعال‌سازی پلن پس از ثبت رسید ناموفق بود.",
                    $"Renewal after receipt was unsuccessful: code={invoice.Code}, result-code={renewal_result.Code}");
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Renewal after receipt registration failed: account-id={0}, code={1}, wallet-id={2}",
                invoice.AccountId, invoice.Code, credit.Id);

            throw new UserException(
                "رسید شما ثبت شد، اما فعال‌سازی پلن با خطا مواجه شد. اعتبار شما در کیف پول حفظ شده است؛ با صدور فاکتور جدید یا تماس با پشتیبانی ادامه دهید.",
                $"Renewal failed after receipt: code={invoice.Code}");
        }
    }

    private async Task RevertToPending(int code)
    {
        try
        {
            await InvoiceRepo.TransitionStatus(code, BalanceStatus.Canceled, BalanceStatus.Pending);
        }
        catch (Exception e)
        {
            Log.Error(e, "Reverting invoice status to pending failed: code={0}", code);
        }
    }

    private async Task SaveIssueHistory(int account_id, int price)
    {
        await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
        {
            Target = account_id,
            Category = EventCategory.Transaction,
            Type = EventType.Information,
            Title = "مالی",
            Description = "فاکتور صادر شد.",
            Price = price,
        });
    }

    private int RequireAccountId()
    {
        return JobContext.Value.AccountId
               ?? throw new Exception("Account-Id is not set in job-context!");
    }

    private void ValidateReceiptImage(byte[] image, string? file_name, string? content_type)
    {
        var max_bytes = Options.Value.ReceiptMaxBytes;

        if (image.Length > max_bytes)
        {
            throw new UserException("حجم تصویر رسید بیش از حد مجاز است!",
                $"Receipt image too large: {image.Length} > {max_bytes}");
        }

        var extension = Path.GetExtension(file_name ?? string.Empty).ToLowerInvariant();

        if (!ReceiptImageExtensions.Contains(extension))
        {
            throw new UserException("فرمت تصویر رسید مجاز نیست!",
                $"Receipt image extension is not allowed: {extension}");
        }

        var normalized_type = (content_type ?? string.Empty).ToLowerInvariant();

        if (!ReceiptImageContentTypes.Contains(normalized_type))
        {
            throw new UserException("نوع محتوای تصویر رسید مجاز نیست!",
                $"Receipt image content-type is not allowed: {content_type}");
        }
    }
}
