using SkiaSharp;

namespace PhotonBypass.Portal.Basical.Captcha;

/// <summary>
/// Renders a captcha code to a PNG data-url with SkiaSharp. Deliberately
/// cheap: fixed small canvas, one paint per pass, a handful of noise
/// primitives — the dedicated rate-limit policy on the captcha endpoint is
/// the outer CPU guard.
/// </summary>
internal static class CaptchaImageGenerator
{
    private static readonly SKColor[] GlyphColors =
    [
        new(0x1F, 0x3A, 0x5F),
        new(0x2E, 0x5E, 0x1E),
        new(0x6B, 0x1F, 0x1F),
        new(0x4A, 0x2C, 0x6B),
        new(0x1F, 0x5F, 0x5F),
    ];

    private static readonly SKColor NoiseColor = new(0xA0, 0xA0, 0xA0, 0x60);

    public static string Render(string code, int width, int height)
    {
        var random = Random.Shared;

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(0xF5, 0xF5, 0xF0));

        DrawNoise(canvas, random, width, height);

        using var font = new SKFont(SKTypeface.Default, height * 0.58f);
        using var glyph_paint = new SKPaint { IsAntialias = true };

        var step = (float)width / (code.Length + 1);
        var half_cap = font.Metrics.CapHeight / 2f;

        for (var i = 0; i < code.Length; i++)
        {
            var glyph = code[i].ToString();
            var glyph_width = font.MeasureText(glyph, glyph_paint);

            // center the glyph cell, jitter + rotate around its visual center
            var center_x = step * (i + 1);
            var x = center_x - glyph_width / 2 + random.Next(-3, 4);
            var y = height / 2f + half_cap + random.Next(-4, 5);

            canvas.Save();
            canvas.RotateDegrees(random.Next(-22, 23), center_x, height / 2f);
            glyph_paint.Color = GlyphColors[random.Next(GlyphColors.Length)];
            canvas.DrawText(glyph, x, y, font, glyph_paint);
            canvas.Restore();
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 80);

        return $"data:image/png;base64,{Convert.ToBase64String(data.ToArray())}";
    }

    private static void DrawNoise(SKCanvas canvas, Random random, int width, int height)
    {
        using var dot_paint = new SKPaint { IsAntialias = true, Color = NoiseColor };

        for (var i = 0; i < 70; i++)
        {
            canvas.DrawCircle(random.Next(width), random.Next(height), 1, dot_paint);
        }

        using var line_paint = new SKPaint { IsAntialias = true, Color = NoiseColor, StrokeWidth = 1.2f, Style = SKPaintStyle.Stroke };

        for (var i = 0; i < 5; i++)
        {
            canvas.DrawLine(
                random.Next(width), random.Next(height),
                random.Next(width), random.Next(height),
                line_paint);
        }
    }
}
