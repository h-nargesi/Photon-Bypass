using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Account;

public interface IInvoiceRepository
{
    IDbContext DbContext { get; }

    Task<int> GenerateNewInvoiceCode();

    Task<int> Insert(InvoiceEntity invoice);

    Task<InvoiceEntity?> GetByCode(int code);

    Task<InvoiceEntity?> GetByCodeOwner(string target, int code);

    Task<List<InvoiceEntity>> GetPendingByAccount(int account_id);

    Task<int> CancelPreviousPending(int account_id);

    Task<int> TransitionStatus(int code, BalanceStatus from, BalanceStatus to);

    Task<int> RegisterReceipt(int code, byte[]? image, string? text);
}
