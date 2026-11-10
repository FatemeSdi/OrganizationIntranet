using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace OrganizationIntranet.Tests;

/// <summary>
/// migration 018 ده سامانه‌ی صفحه‌ی اصلی اینترانت قدیمی را با نشانی و توضیح همان صفحه عمومی می‌کند. این آزمون (بدون بانک) نشانی‌ها و کدها را قفل می‌کند
/// تا غلط تایپی یا تغییر ناخواسته‌ی نشانی دیده شود؛ اجرای واقعی روی SQL Server را آزمون‌های اختیاری نصب تازه و verify می‌سنجند.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PublicApplicationsMigrationTests
{
    private static string Sql([CallerFilePath] string source = "") =>
        File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "..", "database", "migrations", "018_PublicApplications.up.sql")));

    [Theory]
    [InlineData("SALARYPAYMENTRECEIPT", "https://interanet.sez-hpa.ir:9091", "مشاهده فیش حقوق پرسنلی بندر شهید رجایی")]
    [InlineData("FILESHARING", "https://10.2.77.40", "سامانه اشتراک گذاری فایل ها بندر شهید رجایی")]
    [InlineData("GATEWAY", "https://sp.pmo.ir/", "درگاه واحد خدمات الکترونیک سازمان بنادر و دریانوردی")]
    [InlineData("MOBIN", "https://mobin.sez-hpa.ir", "سامانه ثبت درخواست کارها مبین بندر شهید رجایی")]
    [InlineData("AUTOMATION", "http://10.2.78.76:91", "سامانه اتوماسیون اداری فرزین بندر شهید رجایی")]
    [InlineData("FILEUPLOAD", "https://10.2.245.106", "سامانه ورود فایل بندر شهید رجایی")]
    [InlineData("GCOMS", "https://interanet.sez-hpa.ir:9092/", "سامانه گزارش جیکامز قدیم بندر شهید رجایی")]
    [InlineData("CUSTOMERS", "https://interanet.sez-hpa.ir:8082/Account/Login", "سامانه اطلاعات مشتریان بندر شهید رجایی")]
    [InlineData("BIJAKCONTROL", "https://interanet.sez-hpa.ir:8083/", "سامانه کنترل بیجک بندر شهید رجایی")]
    [InlineData("DEMAND", "http://10.2.78.133:8081", "دیماند بندر شهید رجایی")]
    public void Each_of_the_ten_systems_has_the_address_and_description_of_the_old_home_page(string code, string url, string description)
    {
        var row = Regex.Match(Sql(), $@"\('{code}',[^\r\n]*");
        Assert.True(row.Success, code + " is missing from the migration");
        Assert.Contains($"N'{url}'", row.Value);
        Assert.Contains($"N'{description}'", row.Value);
    }

    [Fact]
    public void The_migration_makes_them_public_without_touching_names_orders_or_the_unit_mode_conflict()
    {
        var sql = Sql();
        Assert.Contains("IsPublic = 1, IsUnitRestricted = 0, IsActive = 1", sql);          // با قید CK_Application_AccessMode سازگار است
        Assert.DoesNotMatch(@"UPDATE x SET[^;]*(ApplicationName|DisplayOrder|Icon)\s*=", sql);   // نام، ترتیب و آیکن سامانه‌ی موجود دست نمی‌خورد
        Assert.Equal(10, Regex.Matches(sql, @"^\s+\('[A-Z]+',", RegexOptions.Multiline).Count);
    }
}
