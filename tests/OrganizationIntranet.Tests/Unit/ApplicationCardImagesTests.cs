using System.Buffers.Binary;
using Xunit;

namespace OrganizationIntranet.Tests;

// تصویر کارت سامانه‌ها (wwwroot/assets/images/app-logos): همه ۹۰۰×۴۳۲ (نسبت کارت)، JPEG سبک (کمتر از ۱۲۰ کیلوبایت تا کارت‌ها سریع بالا بیایند)، بدون متن داخل تصویر و با ترکیب رنگ تم پرتال در منبع SVG.
public sealed class ApplicationCardImagesTests
{
    private static readonly string Folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "wwwroot", "assets", "images", "app-logos"));

    public static IEnumerable<object[]> Cards() => Directory.EnumerateFiles(Folder, "*-card.jpg").Select(f => new object[] { Path.GetFileName(f) });

    /// <summary>ابعاد JPEG از اولین بخش SOF (بدون کتابخانه‌ی تصویر).</summary>
    private static (int Width, int Height) JpegSize(byte[] bytes)
    {
        var i = 2;
        while (i + 9 < bytes.Length)
        {
            if (bytes[i] != 0xFF) { i++; continue; }
            var marker = bytes[i + 1];
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
                return (BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(i + 7, 2)), BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(i + 5, 2)));
            i += 2 + BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(i + 2, 2));
        }
        throw new InvalidDataException("SOF not found");
    }

    [Fact]
    public void Every_active_system_known_to_the_portal_has_a_card_image() =>
        Assert.True(Directory.EnumerateFiles(Folder, "*-card.jpg").Count() >= 13, "تصویر کارت ۱۳ سامانه (مدیریت وظایف و ۱۲ سامانه‌ی دیگر) باید در پوشه باشد");

    [Theory, MemberData(nameof(Cards))]
    public void Card_image_is_a_light_jpeg_of_the_card_ratio_within_the_upload_limit(string name)
    {
        var bytes = File.ReadAllBytes(Path.Combine(Folder, name));
        Assert.True(bytes.Length is > 0 and <= 120 * 1024, $"{name}: حجم {bytes.Length} بایت؛ تصویر سنگین کارت پورتال را کند می‌کند");
        Assert.True(bytes.Length <= OrganizationIntranet.Contracts.ApplicationImageRules.MaxBytes);
        Assert.Equal("image/jpeg", OrganizationIntranet.Application.Services.PublicationService.DetectImageType(bytes));
        Assert.Equal((900, 432), JpegSize(bytes));
    }

    [Theory, MemberData(nameof(Cards))]
    public void Card_svg_source_has_no_text_and_uses_only_the_portal_palette(string jpgName)
    {
        var svg = File.ReadAllText(Path.Combine(Folder, Path.ChangeExtension(jpgName, ".svg")));
        Assert.DoesNotContain("<text", svg);                                              // نام و توضیح زیر تصویر در کارت می‌آید، نه داخل آن
        Assert.Contains("viewBox=\"0 0 1200 576\"", svg);
        var allowed = new[] { "#0A2036", "#123A5C", "#1573B8", "#D6A24C", "#F7F4EC", "#D9D4C4", "#FFFFFF", "#9AA7B1", "#E9E5D8", "#E4C98F" };
        foreach (System.Text.RegularExpressions.Match color in System.Text.RegularExpressions.Regex.Matches(svg, "#[0-9A-Fa-f]{6}"))
            Assert.Contains(color.Value.ToUpperInvariant(), allowed);
    }
}
