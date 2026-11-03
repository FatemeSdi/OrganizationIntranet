using OrganizationIntranet.Application.Services;

namespace OrganizationIntranet.Api;

/// <summary>
/// حالت خط فرمان API برای ساخت اولین مدیر: <c>dotnet run --project src/OrganizationIntranet.Api -- --create-first-admin --username u --name n --last-name l</c>.
/// رمز فقط از متغیر محیطی INTRANET_FIRST_ADMIN_PASSWORD یا پرسش پنهان کنسول می‌آید (هرگز آرگومان خط فرمان) و هیچ‌جا چاپ نمی‌شود.
/// برنامه پس از ساخت بدون راه‌اندازی وب‌سرور بسته می‌شود؛ ابزار آماده‌ی tools/New-FirstAdmin.ps1 همین را با پرسش امن رمز اجرا می‌کند.
/// </summary>
public static class FirstAdminCommand
{
    public const string Flag = "--create-first-admin";
    public const string PasswordVariable = "INTRANET_FIRST_ADMIN_PASSWORD";

    /// <summary>مقدار گزینه‌ی --key از آرگومان‌ها؛ null اگر نبود.</summary>
    public static string? Option(string[] args, string key)
    {
        var index = Array.IndexOf(args, key);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    /// <summary>رمز را از متغیر محیطی یا پرسش بدون نمایش می‌خواند.</summary>
    private static string? ReadPassword()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(PasswordVariable);
        if (!string.IsNullOrEmpty(fromEnvironment)) return fromEnvironment;
        if (Console.IsInputRedirected) return null;
        string Ask(string prompt)
        {
            Console.Write(prompt);
            var buffer = new System.Text.StringBuilder();
            for (ConsoleKeyInfo key; (key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter;)
            {
                if (key.Key == ConsoleKey.Backspace) { if (buffer.Length > 0) buffer.Length--; }
                else if (!char.IsControl(key.KeyChar)) buffer.Append(key.KeyChar);
            }
            Console.WriteLine();
            return buffer.ToString();
        }
        var first = Ask("رمز مدیر: ");
        return first == Ask("تکرار رمز: ") ? first : throw new ArgumentException("رمز و تکرار آن یکسان نیست");
    }

    /// <summary>ساخت مدیر و گزارش نتیجه؛ کد خروج ۰ موفق و ۱ ناموفق است. هیچ رمز یا هشی چاپ نمی‌شود.</summary>
    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            var id = await scope.ServiceProvider.GetRequiredService<FirstAdminService>()
                .CreateAsync(Option(args, "--username"), Option(args, "--name"), Option(args, "--last-name"), ReadPassword());
            Console.WriteLine($"مدیر ساخته شد (شناسه {id}). اکنون با همان نام کاربری و رمز وارد پنل شوید.");
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine("ناموفق: " + ex.Message);
            return 1;
        }
    }
}
