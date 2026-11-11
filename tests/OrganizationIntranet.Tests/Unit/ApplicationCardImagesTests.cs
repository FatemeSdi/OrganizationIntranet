using System.Buffers.Binary;
using Xunit;

namespace OrganizationIntranet.Tests;

// تصویر کارت سامانه‌ها (wwwroot/assets/images/app-logos): همه ۱۲۰۰×۵۷۶، PNG زیر ۲ مگابایت (سقف API)، بدون متن داخل تصویر و با ترکیب رنگ تم پرتال.
public sealed class ApplicationCardImagesTests
{
    private static readonly string Folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "wwwroot", "assets", "images", "app-logos"));

    public static IEnumerable<object[]> Cards() => Directory.EnumerateFiles(Folder, "*-card.png").Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Every_active_system_known_to_the_portal_has_a_card_image() =>
        Assert.True(Directory.EnumerateFiles(Folder, "*-card.png").Count() >= 13, "تصویر کارت ۱۳ سامانه (مدیریت وظایف و ۱۲ سامانه‌ی دیگر) باید در پوشه باشد");

    [Theory, MemberData(nameof(Cards))]
    public void Card_image_is_a_png_of_the_card_size_within_the_upload_limit(string name)
    {
        var bytes = File.ReadAllBytes(Path.Combine(Folder, name));
        Assert.True(bytes.Length is > 0 and <= OrganizationIntranet.Contracts.ApplicationImageRules.MaxBytes, $"{name}: حجم");
        Assert.Equal("image/png", OrganizationIntranet.Application.Services.PublicationService.DetectImageType(bytes));
        Assert.Equal(1200, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));   // عرض در بخش IHDR
        Assert.Equal(576, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));    // ارتفاع
    }

    [Theory, MemberData(nameof(Cards))]
    public void Card_svg_source_has_no_text_and_uses_only_the_portal_palette(string pngName)
    {
        var svg = File.ReadAllText(Path.Combine(Folder, Path.ChangeExtension(pngName, ".svg")));
        Assert.DoesNotContain("<text", svg);                                              // نام و توضیح زیر تصویر در کارت می‌آید، نه داخل آن
        Assert.Contains("viewBox=\"0 0 1200 576\"", svg);
        var allowed = new[] { "#0A2036", "#123A5C", "#1573B8", "#D6A24C", "#F7F4EC", "#D9D4C4", "#FFFFFF", "#9AA7B1", "#E9E5D8", "#E4C98F" };
        foreach (System.Text.RegularExpressions.Match color in System.Text.RegularExpressions.Regex.Matches(svg, "#[0-9A-Fa-f]{6}"))
            Assert.Contains(color.Value.ToUpperInvariant(), allowed);
    }
}
