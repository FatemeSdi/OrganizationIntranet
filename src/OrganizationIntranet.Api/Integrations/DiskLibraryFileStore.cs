using System.Security.Cryptography;
using System.Text.RegularExpressions;
using OrganizationIntranet.Application.Abstractions;

namespace OrganizationIntranet.Api.Integrations;

/// <summary>
/// نگهداری فایل اسناد کتابخانه روی دیسک سرور API، در پوشه‌ی تنظیم‌شده (<c>Library:StoragePath</c>؛ پیش‌فرض <c>App_Data/library</c> کنار برنامه).
/// نام فایل فقط ۳۲ نویسه‌ی هگز تصادفی است و هر نام دیگری رد می‌شود، پس مسیر هرگز از ورودی کاربر ساخته نمی‌شود (جلوگیری از path traversal).
/// نوشتن اول در فایل موقت و بعد جابه‌جایی انجام می‌شود تا فایل ناقص هرگز با نام نهایی دیده نشود؛ حجم هنگام نوشتن شمرده می‌شود، نه از هدر درخواست.
/// </summary>
public sealed partial class DiskLibraryFileStore(IConfiguration configuration, IWebHostEnvironment environment) : ILibraryFileStore
{
    [GeneratedRegex("^[0-9a-f]{32}$")] private static partial Regex StoredNamePattern();

    /// <summary>پوشه‌ی ذخیره‌سازی؛ مسیر نسبی نسبت به پوشه‌ی برنامه حساب می‌شود.</summary>
    public string Root => Path.GetFullPath(configuration["Library:StoragePath"] is { Length: > 0 } configured ? configured : Path.Combine(environment.ContentRootPath, "App_Data", "library"), environment.ContentRootPath);

    private string PathOf(string storedName) =>
        StoredNamePattern().IsMatch(storedName) ? Path.Combine(Root, storedName) : throw new ArgumentException("نام فایل ذخیره‌شده معتبر نیست");

    public async Task<(byte[] Sha256, long Size)> SaveAsync(string storedName, Stream content, long maxBytes, CancellationToken cancellationToken)
    {
        var target = PathOf(storedName);
        Directory.CreateDirectory(Root);
        var temp = target + ".tmp";
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long size = 0;
        try
        {
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                for (int read; (read = await content.ReadAsync(buffer, cancellationToken)) > 0;)
                {
                    size += read;
                    if (size > maxBytes) throw new ArgumentException($"حجم فایل باید حداکثر {maxBytes / (1024 * 1024)} مگابایت باشد");
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }
            if (size == 0) throw new ArgumentException("فایل خالی است");
            File.Move(temp, target);
        }
        catch { try { File.Delete(temp); } catch (IOException) { } throw; }
        return (hash.GetHashAndReset(), size);
    }

    public Stream? OpenRead(string storedName)
    {
        var path = PathOf(storedName);
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true) : null;
    }

    public void Delete(string storedName)
    {
        try { File.Delete(PathOf(storedName)); } catch (IOException) { /* فایل در حال استفاده؛ بعداً با پاک‌سازی دستی */ }
    }
}
