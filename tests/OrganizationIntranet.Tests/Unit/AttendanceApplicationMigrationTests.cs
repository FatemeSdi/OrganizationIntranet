using System.Runtime.CompilerServices;
using Xunit;

namespace OrganizationIntranet.Tests;

/// <summary>
/// migration 021 سامانه‌ی «حضور و غیاب» (نشانی از نسخه‌ی دوم صفحه‌ی اینترانت قدیمی) را عمومی اضافه می‌کند و توضیح کارت دیماند و جیکامز را عوض می‌کند.
/// این آزمون (بدون بانک) نشانی، توضیح و عمومی بودن را در خود فایل می‌سنجد؛ اجرای واقعی روی SQL Server را آزمون‌های اختیاری نصب تازه و verify می‌سنجند.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AttendanceApplicationMigrationTests
{
    private static string Read(string name, [CallerFilePath] string source = "") =>
        File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "..", "database", "migrations", name)));

    [Fact]
    public void Attendance_is_added_as_a_public_application_with_the_address_and_description_of_the_old_page()
    {
        var sql = Read("021_AttendanceApplication.up.sql");
        Assert.Contains("N'حضور و غیاب'", sql);
        Assert.Contains("'ATTENDANCE'", sql);
        Assert.Contains("N'http://10.2.28.95'", sql);
        Assert.Contains("N'سامانه امور کارکنان'", sql);
        Assert.Contains("IsPublic = 1, IsUnitRestricted = 0, IsActive = 1", sql);          // با قید CK_Application_AccessMode سازگار است
    }

    [Fact]
    public void The_card_descriptions_of_demand_and_gcoms_are_updated_and_the_migration_is_idempotent_and_guarded()
    {
        var sql = Read("021_AttendanceApplication.up.sql");
        Assert.Contains("SET Description = N'محاسبه مدت زمان کارکرد خن شناورها' WHERE ApplicationCode = 'DEMAND'", sql);
        Assert.Contains("SET Description = N'سامانه مدیریت کالاهای متفرقه و نفتی' WHERE ApplicationCode = 'GCOMS'", sql);
        Assert.Contains("Already applied", sql);                                              // اجرای دوباره بی‌اثر است
        Assert.Contains("Apply 020_ApplicationImages first.", sql);                          // پیش‌نیاز
        Assert.Contains("same name but another code", sql);                                   // برخورد نام با کد دیگر
    }

    [Fact]
    public void The_rollback_deletes_the_application_and_its_image_only_when_nothing_references_it()
    {
        var down = Read("021_AttendanceApplication.down.sql");
        Assert.Contains("DELETE App.ApplicationImage WHERE ApplicationId = @id;", down);      // ردیف تصویر پیش از سامانه (کلید خارجی)
        Assert.Contains("IsPublic = 0, IsActive = 0", down);                                  // با وابستگی فقط غیرفعال می‌شود
        Assert.Contains("App.ApplicationAccess", down);
        Assert.Contains("Sec.SsoTicket", down);
    }
}
