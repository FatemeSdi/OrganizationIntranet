using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api;

/// <summary>یک ردیف فهرست وارد کردن: فایل (نسبت به پوشه‌ی مبدأ)، بخش، عنوان دسته، عنوان و توضیح سند.</summary>
public sealed record LibraryImportItem(string File, string Group, string Category, string Title, string? Description = null);

/// <summary>
/// حالت خط فرمان API برای وارد کردن فایل‌های موجود به کتابخانه‌ی اسناد:
/// <c>dotnet run --project src/OrganizationIntranet.Api -- --import-library manifest.json --base "D:\پوشه" [--actor نام‌کاربری] [--apply]</c>.
/// بدون <c>--apply</c> فقط گزارش می‌دهد (بدون نوشتن). با <c>--apply</c> هر فایل از همان مسیر <see cref="LibraryService"/> می‌گذرد، پس پسوند، امضا و حجم مثل آپلود پنل بررسی می‌شود.
/// تکرارپذیر است: سندی که همان عنوان را در همان دسته دارد دوباره ساخته نمی‌شود. دسته باید از قبل باشد. مبدأ فقط خوانده می‌شود.
/// </summary>
public static class LibraryImportCommand
{
    public const string Flag = "--import-library";

    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        var manifestPath = FirstAdminCommand.Option(args, Flag);
        var baseDirectory = FirstAdminCommand.Option(args, "--base");
        var apply = args.Contains("--apply");
        if (manifestPath is null || baseDirectory is null || !File.Exists(manifestPath) || !Directory.Exists(baseDirectory))
        { Console.Error.WriteLine("ناموفق: مسیر فهرست (manifest) یا پوشه‌ی مبدأ (--base) پیدا نشد."); return 1; }
        List<LibraryImportItem>? items;
        try { items = JsonSerializer.Deserialize<List<LibraryImportItem>>(await File.ReadAllTextAsync(manifestPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException) { Console.Error.WriteLine("ناموفق: فایل فهرست JSON معتبر نیست."); return 1; }
        if (items is null || items.Count == 0) { Console.Error.WriteLine("ناموفق: فهرست خالی است."); return 1; }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<LibraryService>();
        var maxBytes = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetValue<long?>("Library:MaxFileBytes") is > 0 and var configured ? configured : LibraryRules.DefaultMaxFileBytes;
        var actorName = FirstAdminCommand.Option(args, "--actor");
        var actor = await db.Users.AsNoTracking().Where(u => u.IsActive && (actorName != null ? u.Username == actorName : u.UserRoles.Any(r => r.Role.RoleCode == AdminPermissions.AdminRoleCode && r.Role.IsActive)))
            .OrderBy(u => u.UserId).Select(u => (long?)u.UserId).FirstOrDefaultAsync();
        if (actor is null) { Console.Error.WriteLine("ناموفق: کاربر فعال انجام‌دهنده (مدیر یا --actor) پیدا نشد."); return 1; }

        int created = 0, existing = 0, failed = 0;
        foreach (var item in items)
        {
            var path = Path.GetFullPath(Path.Combine(baseDirectory, item.File));
            var label = $"{item.File} ← «{item.Title}» ({item.Group}/{item.Category})";
            if (!path.StartsWith(Path.GetFullPath(baseDirectory), StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) { Console.WriteLine("رد (فایل نیست): " + label); failed++; continue; }
            var category = await db.LibraryCategories.AsNoTracking().FirstOrDefaultAsync(c => c.GroupCode == item.Group && c.Title == item.Category);
            if (category is null) { Console.WriteLine("رد (دسته نیست): " + label); failed++; continue; }
            if (await db.LibraryDocuments.AnyAsync(d => d.CategoryId == category.CategoryId && d.Title == item.Title)) { Console.WriteLine("موجود: " + label); existing++; continue; }
            if (!apply) { Console.WriteLine($"آماده ({new FileInfo(path).Length / 1024} KB): " + label); created++; continue; }
            try
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                await service.CreateDocumentAsync(new(category.CategoryId, item.Title, item.Description, true), Path.GetFileName(path), stream, stream.Length, maxBytes, actor.Value);
                Console.WriteLine("ثبت شد: " + label); created++;
            }
            catch (ArgumentException ex) { Console.WriteLine($"رد ({ex.Message}): " + label); failed++; }
        }
        Console.WriteLine($"{(apply ? "ثبت‌شده" : "آماده‌ی ثبت (آزمایشی، چیزی نوشته نشد)")}: {created}، از قبل موجود: {existing}، ردشده: {failed}");
        if (!apply && failed == 0) Console.WriteLine("برای ثبت واقعی --apply را اضافه کنید (پیش از آن از بانک و پوشه‌ی Library:StoragePath نسخه‌ی پشتیبان بگیرید).");
        return failed == 0 ? 0 : 1;
    }
}
