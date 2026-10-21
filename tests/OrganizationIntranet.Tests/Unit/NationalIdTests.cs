using OrganizationIntranet.Application.Services;
using Xunit;

namespace OrganizationIntranet.Tests;

[Trait("Category", "Unit")]
public sealed class NationalIdTests
{
    [Theory]
    [InlineData("0012345679", true)]   // رقم کنترل درست (باقی‌مانده ۲ ⇒ ۱۱-۲=۹)
    [InlineData("1234567891", true)]   // باقی‌مانده ۱ ⇒ رقم کنترل خود ۱
    [InlineData("0012345678", false)]  // رقم کنترل اشتباه
    [InlineData("1111111111", false)]  // همه ارقام یکسان
    [InlineData("0000000000", false)]
    [InlineData("001234567", false)]   // ۹ رقم
    [InlineData("00123456790", false)] // ۱۱ رقم
    [InlineData("00123a5679", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Validates_length_digits_and_check_digit(string? id, bool expected) =>
        Assert.Equal(expected, AuthenticationService.NationalIdValid(id));

    [Theory]
    [InlineData("۰۰۱۲۳۴۵۶۷۹", "0012345679")] // ارقام فارسی
    [InlineData("٠٠١٢٣٤٥٦٧٩", "0012345679")] // ارقام عربی
    [InlineData(" 001-234567 9 ", "0012345679")]
    [InlineData(null, "")]
    public void Normalizes_persian_and_arabic_digits_spaces_and_dashes(string? input, string expected) =>
        Assert.Equal(expected, AuthenticationService.NormalizeNationalId(input));
}
