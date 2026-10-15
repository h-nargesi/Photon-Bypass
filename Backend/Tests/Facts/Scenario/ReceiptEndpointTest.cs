using PhotonBypass.Result;
using PhotonBypass.Test.Initializer;
using System.Text;

namespace PhotonBypass.Test.Facts.Scenario;

public class ReceiptEndpointTest(ProgramLevelInitializer.Factory factory) : HttpHandler(factory)
{
    // minimal valid JPEG (SOI + JFIF APP0 + EOI)
    private static byte[] JpegImage =>
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
        0xFF, 0xD9,
    ];

    // minimal valid PNG (signature + IHDR chunk header)
    private static byte[] PngImage =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
    ];

    // minimal RIFF/WEBP header
    private static byte[] WebpImage =>
    [
        0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50,
    ];

    /// <summary>
    /// Registers a fresh user and issues a Pending plan invoice with a positive
    /// payable amount through the real estimate + renewal endpoints.
    /// </summary>
    private async Task<int> NewPendingInvoice(string username, string phone)
    {
        await Register(username, phone);
        await Login(username, "Password");

        var renewal = await Renewal(username, 1, 120, 50);
        Assert.NotNull(renewal["invoiceCode"]);

        return int.Parse(renewal["invoiceCode"]!);
    }

    [Fact]
    public async Task RegisterReceipt_ValidJpeg_MovesInvoiceToVerifying()
    {
        var code = await NewPendingInvoice("rcpt_user01", "31");

        var result = await RegisterReceipt(code, JpegImage, "receipt.jpg", "image/jpeg");
        Assert.Equal("رسید ثبت شد و در انتظار تایید است.", result.Message);

        var invoice = await GetInvoice(code);
        Assert.Equal("4", invoice["status"]); // BalanceStatus.Verifying
        Assert.Equal("True", invoice["hasReceipt"]);
    }

    [Fact]
    public async Task RegisterReceipt_ValidPng_MovesInvoiceToVerifying()
    {
        var code = await NewPendingInvoice("rcpt_user_png", "42");

        await RegisterReceipt(code, PngImage, "receipt.png", "image/png");

        var invoice = await GetInvoice(code);
        Assert.Equal("4", invoice["status"]);
    }

    [Fact]
    public async Task RegisterReceipt_ValidWebp_MovesInvoiceToVerifying()
    {
        var code = await NewPendingInvoice("rcpt_user_webp", "43");

        await RegisterReceipt(code, WebpImage, "receipt.webp", "image/webp");

        var invoice = await GetInvoice(code);
        Assert.Equal("4", invoice["status"]);
    }

    [Fact]
    public async Task RegisterReceipt_TextOnly_IsAccepted()
    {
        var code = await NewPendingInvoice("rcpt_user03", "33");

        await RegisterReceipt(code, text: "card-to-card, bank receipt 1234");

        var invoice = await GetInvoice(code);
        Assert.Equal("4", invoice["status"]);
    }

    [Fact]
    public async Task RegisterReceipt_FileAndTextTogether_IsRejected()
    {
        var code = await NewPendingInvoice("rcpt_user04", "34");

        await RegisterReceipt(code, JpegImage, "receipt.jpg", "image/jpeg", text: "also a text", status: 4);

        var invoice = await GetInvoice(code);
        Assert.Equal("0", invoice["status"]); // still Pending
    }

    [Fact]
    public async Task RegisterReceipt_NeitherFileNorText_IsRejected()
    {
        var code = await NewPendingInvoice("rcpt_user05", "35");

        await RegisterReceipt(code, status: 4);
    }

    [Fact]
    public async Task RegisterReceipt_DisallowedContentType_IsRejected()
    {
        var code = await NewPendingInvoice("rcpt_user06", "36");

        await RegisterReceipt(code, Encoding.UTF8.GetBytes("%PDF-1.7 fake"), "receipt.pdf", "application/pdf", status: 4);

        var invoice = await GetInvoice(code);
        Assert.Equal("0", invoice["status"]);
    }

    [Fact]
    public async Task RegisterReceipt_OversizedImage_IsRejected()
    {
        var code = await NewPendingInvoice("rcpt_user07", "37");

        // RequestSizeLimit is 3MB; the receipt rule rejects images above 2MB
        var oversized = new byte[2_097_153 + 1];
        oversized[0] = 0xFF;
        oversized[1] = 0xD8;

        await RegisterReceipt(code, oversized, "receipt.jpg", "image/jpeg", status: 4);

        var invoice = await GetInvoice(code);
        Assert.Equal("0", invoice["status"]);
    }

    [Fact]
    public async Task RegisterReceipt_UnknownInvoice_ReturnsUserError()
    {
        await Register("rcpt_user08", "38");
        await Login("rcpt_user08", "Password");

        await RegisterReceipt(999_999, text: "some receipt", status: 4);
    }

    [Fact]
    public async Task RegisterReceipt_WithForeignTarget_IsForbidden()
    {
        var code = await NewPendingInvoice("rcpt_owner09", "39");

        await Register("rcpt_attacker09", "40");
        await Login("rcpt_attacker09", "Password");

        await RegisterReceipt(code, text: "stolen receipt", target: "rcpt_owner09", status: 403);

        await Login("rcpt_owner09", "Password");
        var invoice = await GetInvoice(code);
        Assert.Equal("0", invoice["status"]); // untouched
    }

    [Fact]
    public async Task RegisterReceipt_OtherUsersInvoiceCode_ReturnsUserError()
    {
        var code = await NewPendingInvoice("rcpt_owner10", "44");

        await Register("rcpt_attacker10", "45");
        await Login("rcpt_attacker10", "Password");

        // no target: attacker is the owner from the repo's point of view → not found
        await RegisterReceipt(code, text: "stolen receipt", status: 4);
    }

    [Fact]
    public async Task RegisterReceipt_SecondAttemptOnSameInvoice_Fails()
    {
        var code = await NewPendingInvoice("rcpt_user11", "46");

        await RegisterReceipt(code, JpegImage, "receipt.jpg", "image/jpeg");
        await RegisterReceipt(code, text: "second receipt", status: 4);

        var invoice = await GetInvoice(code);
        Assert.Equal("4", invoice["status"]); // stays at first receipt
    }
}
