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

    public InvoiceRepositoryMoq(AccountRepositoryMoq account_moq)
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

                foreach (var invoice in Data.Where(i => i.AccountId == account_id && i.Status == BalanceStatus.Pending))
                {
                    invoice.Status = BalanceStatus.Canceled;
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

                invoice.Status = BalanceStatus.Verifying;
                invoice.ReceiptImage = image;
                invoice.ReceiptText = text;
                invoice.ReceiptAt = DateTime.Now;

                return Task.FromResult(1);
            });

        var db_context_moq = new Mock<IDbContext>();
        db_context_moq.Setup(x => x.BeginTransactionAsync())
            .Returns(() =>
            {
                var mock = new Mock<IDbTransaction>();

                mock.Setup(t => t.Commit());
                mock.Setup(t => t.Rollback());

                return Task.FromResult(mock.Object);
            });

        Setup(x => x.DbContext).Returns(db_context_moq.Object);
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<InvoiceRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<InvoiceRepositoryMoq>().Object);
    }
}
