using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Repository;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class InvoiceRepository(LocalDbContext context) : DapperRepository<InvoiceEntity>(context), IInvoiceRepository
{
    public IDbContext DbContext { get; } = context;

    public Task<int> GenerateNewInvoiceCode()
    {
        return ExecuteScalarAsync<int>("select next value for InvoiceSequence");
    }

    public Task<int> Insert(InvoiceEntity invoice)
    {
        var sql = $"""
                   insert into {TableName} ({nameof(InvoiceEntity.Code)}, {nameof(InvoiceEntity.AccountId)}, {nameof(InvoiceEntity.Kind)},
                                              {nameof(InvoiceEntity.Title)}, {nameof(InvoiceEntity.TotalPrice)}, {nameof(InvoiceEntity.WalletDeduction)},
                                              {nameof(InvoiceEntity.Payable)}, {nameof(InvoiceEntity.Action)}, {nameof(InvoiceEntity.Status)})
                   values (@code, @account_id, @kind, @title, @total_price, @wallet_deduction, @payable, @action, @status)
                   """;

        return ExecuteAsync(sql, new
        {
            code = invoice.Code,
            account_id = invoice.AccountId,
            kind = invoice.Kind,
            title = invoice.Title,
            total_price = invoice.TotalPrice,
            wallet_deduction = invoice.WalletDeduction,
            payable = invoice.Payable,
            action = invoice.Action,
            status = invoice.Status,
        });
    }

    public async Task<InvoiceEntity?> GetByCode(int code)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(InvoiceEntity.Code)} = @code")
            .WithParameters(new { code }));

        return result.FirstOrDefault();
    }

    public async Task<InvoiceEntity?> GetByCodeOwner(string target, int code)
    {
        var sql = $"""
                   select i.*
                   from {TableName} i
                   join {AccountRepository.TableName} a on i.{nameof(InvoiceEntity.AccountId)} = a.{nameof(AccountEntity.Id)}
                   where i.{nameof(InvoiceEntity.Code)} = @code
                     and a.{nameof(AccountEntity.Username)} = @target
                   """;

        var result = await QueryAsync<InvoiceEntity>(sql, new { target, code });

        return result.FirstOrDefault();
    }

    public async Task<List<InvoiceEntity>> GetPendingByAccount(int account_id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(InvoiceEntity.AccountId)} = @account_id and {nameof(InvoiceEntity.Status)} = @status")
            .WithParameters(new { account_id, status = BalanceStatus.Pending }));

        return [.. result];
    }

    public Task<int> CancelPreviousPending(int account_id)
    {
        var sql = $"""
                   update {WalletRepository.TableName}
                   set {nameof(WalletEntity.Status)} = @canceled
                   where {nameof(WalletEntity.Status)} = @pending
                     and {nameof(WalletEntity.InvoiceCode)} in (select {nameof(InvoiceEntity.Code)} from {TableName}
                                                                where {nameof(InvoiceEntity.AccountId)} = @account_id
                                                                  and {nameof(InvoiceEntity.Status)} = @pending);

                   update {TableName}
                   set {nameof(InvoiceEntity.Status)} = @canceled
                   where {nameof(InvoiceEntity.AccountId)} = @account_id
                     and {nameof(InvoiceEntity.Status)} = @pending;
                   """;

        return ExecuteAsync(sql, new { account_id, pending = BalanceStatus.Pending, canceled = BalanceStatus.Canceled });
    }

    public Task<int> TransitionStatus(int code, BalanceStatus from, BalanceStatus to)
    {
        var sql = $"""
                   update {TableName}
                   set {nameof(InvoiceEntity.Status)} = @to
                   where {nameof(InvoiceEntity.Code)} = @code
                     and {nameof(InvoiceEntity.Status)} = @from
                   """;

        return ExecuteAsync(sql, new { code, from, to });
    }

    public Task<int> RegisterReceipt(int code, byte[]? image, string? text)
    {
        var sql = $"""
                   update {TableName}
                   set {nameof(InvoiceEntity.Status)} = @verifying,
                       {nameof(InvoiceEntity.ReceiptImage)} = @image,
                       {nameof(InvoiceEntity.ReceiptText)} = @text,
                       {nameof(InvoiceEntity.ReceiptAt)} = GETDATE()
                   where {nameof(InvoiceEntity.Code)} = @code
                     and {nameof(InvoiceEntity.Status)} = @pending
                     and {nameof(InvoiceEntity.ReceiptAt)} is null
                   """;

        return ExecuteAsync(sql, new { code, image, text, verifying = BalanceStatus.Verifying, pending = BalanceStatus.Pending });
    }
}
