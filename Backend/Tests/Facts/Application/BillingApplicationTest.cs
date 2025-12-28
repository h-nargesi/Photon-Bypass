using PhotonBypass.Application.Billing;
using PhotonBypass.Application.Billing.Model;
using PhotonBypass.Domain;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Application;

public class BillingApplicationTest : UnitLevelServiceInitializer
{
    [Fact]
    public async Task GenerateInvoiceCode()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_repo = scope.ServiceProvider.GetRequiredService<IBillingApplication>();

        job_context.InjectJobContext(1, "User01", "User01");

        await billing_repo.GenerateInvoiceCode(new NewInvoiceInfo
        {
            Descripttion = "",
        });
    }

    [Fact]
    public async Task GetInvoice()
    {
        using var scope = App.Services.CreateScope();
        var billing_repo = scope.ServiceProvider.GetRequiredService<IBillingApplication>();

        throw new NotImplementedException();
    }

    [Fact(Skip = "NotImplementedException")]
    public async Task PaymentCallback()
    {
        using var scope = App.Services.CreateScope();
        var billing_repo = scope.ServiceProvider.GetRequiredService<IBillingApplication>();

        throw new NotImplementedException();
    }
}
