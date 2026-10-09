using System.Data;
using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Repository;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockLocalRepository;

internal class InvoiceRepositoryMoq : Mock<IInvoiceRepository>, IUnitLevelService
{
    public const int StartCode = 10000;

    public readonly List<InvoiceEntity> Data = [];

    public InvoiceRepositoryMoq(TransactionalMockDbContext db_context, AccountRepositoryMoq account_moq)
    {
        Setup(x => x.GenerateNewInvoiceCode())
            .Returns(() =>
            {
                var result = Data.Count > 0 ? Data.Max(i => i.Code) : StartCode;

                return Task.FromResult(result + 1);
            });

        Setup(x => x.Insert(It.IsAny<InvoiceEntity>()))
            .Returns<InvoiceEntity>(invoice =>
            {
                Data.Add(invoice);
                db_context.RegisterUndo(() => Data.Remove(invoice));

                return Task.FromResult(1);
            });

        Setup(x => x.GetByCode(It.IsAny<int>()))
            .Returns<int>(code => Task.FromResult(Data.FirstOrDefault(i => i.Code == code)));

        Setup(x => x.GetByCodeOwner(It.IsAny<string>(), It.IsAny<int>()))
            .Returns<string, int>((target, code) =>
            {
                var account = account_moq.Data.Values.FirstOrDefault(a => a.Username == target);

                if (account == null)
                {
                    return Task.FromResult<InvoiceEntity?>(null);
                }

                var result = Data.FirstOrDefault(i => i.Code == code && i.AccountId == account.Id);

                return Task.FromResult(result);
            });

        Setup(x => x.GetPendingByAccount(It.IsAny<int>()))
            .Returns<int>(account_id => Task.FromResult(Data
                .Where(i => i.AccountId == account_id && i.Status == BalanceStatus.Pending)
                .ToList()));

        Setup(x => x.CancelPreviousPending(It.IsAny<int>()))
            .Returns<int>(account_id =>
            {
                var updated = 0;

                var pending = Data.Where(i => i.AccountId == account_id && i.Status == BalanceStatus.Pending).ToList();

                foreach (var invoice in pending)
                {
                    invoice.Status = BalanceStatus.Canceled;
                    db_context.RegisterUndo(() => invoice.Status = BalanceStatus.Pending);
                    updated++;
                }

                return Task.FromResult(updated);
            });

        Setup(x => x.TransitionStatus(It.IsAny<int>(), It.IsAny<BalanceStatus>(), It.IsAny<BalanceStatus>()))
            .Returns<int, BalanceStatus, BalanceStatus>((code, from, to) =>
            {
                var invoice = Data.FirstOrDefault(i => i.Code == code && i.Status == from);

                if (invoice == null)
                {
                    return Task.FromResult(0);
                }

                invoice.Status = to;
                db_context.RegisterUndo(() => invoice.Status = from);

                return Task.FromResult(1);
            });

        Setup(x => x.RegisterReceipt(It.IsAny<int>(), It.IsAny<byte[]?>(), It.IsAny<string?>()))
            .Returns<int, byte[]?, string?>((code, image, text) =>
            {
                var invoice = Data.FirstOrDefault(i =>
                    i.Code == code && i.Status == BalanceStatus.Pending && i.ReceiptAt == null);

                if (invoice == null)
                {
                    return Task.FromResult(0);
                }

                var previous_status = invoice.Status;
                var previous_image = invoice.ReceiptImage;
                var previous_text = invoice.ReceiptText;

                invoice.Status = BalanceStatus.Verifying;
                invoice.ReceiptImage = image;
                invoice.ReceiptText = text;
                invoice.ReceiptAt = DateTime.Now;

                db_context.RegisterUndo(() =>
                {
                    invoice.Status = previous_status;
                    invoice.ReceiptImage = previous_image;
                    invoice.ReceiptText = previous_text;
                    invoice.ReceiptAt = null;
                });

                return Task.FromResult(1);
            });

        Setup(x => x.DbContext).Returns(db_context);
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<InvoiceRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<InvoiceRepositoryMoq>().Object);
    }
}
