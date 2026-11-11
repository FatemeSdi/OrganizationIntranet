using OrganizationIntranet.Contracts;
using Xunit;

namespace OrganizationIntranet.Tests;

// حروف اول نام و نام خانوادگی در آیکن پروفایل هدر و منوی آن: با فاصله («ف ص»)، چون حروف فارسی بدون فاصله به هم می‌چسبند و «فص» خوانده می‌شود.
public sealed class ProfileInitialsTests
{
    private static ProfileDto Profile(string name, string last) => new(1, "u", name, last, null, null, null, DateTime.UtcNow, null, "", [], []);

    [Theory]
    [InlineData("فاطمه", "صادقی", "ف ص")]
    [InlineData("علی", "رضایی", "ع ر")]
    [InlineData("فاطمه", "", "ف")]          // بدون نام خانوادگی فاصله‌ی اضافه نمی‌ماند
    [InlineData("", "صادقی", "ص")]
    [InlineData("", "", "")]
    public void Initials_are_separated_by_a_space_so_persian_letters_do_not_join(string name, string last, string expected) =>
        Assert.Equal(expected, Profile(name, last).Initials);
}
