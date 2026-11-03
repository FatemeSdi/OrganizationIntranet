extern alias AdminEntry;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;
using Xunit;

namespace OrganizationIntranet.Tests;

// اولین مدیر نصب تازه: فقط وقتی هیچ مدیر فعالی نیست، با رمز معتبر، فقط هش رمز ذخیره می‌شود و کاربر می‌تواند وارد شود.
public sealed partial class PortalSmokeTests
{
    /// <summary>همه‌ی مدیرها را غیرفعال می‌کند تا حالت «نصب بدون مدیر» ساخته شود (فقط در بانک آزمون).</summary>
    private void RemoveAllAdmins()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Where(u => u.UserRoles.Any(r => r.Role.RoleCode == "ADMIN")).ToList().ForEach(u => u.IsActive = false);
        db.SaveChanges();
    }

    private async Task<long> CreateFirstAdmin(string? username, string? password, string name = "مدیر", string lastName = "اول")
    {
        using var scope = api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<FirstAdminService>().CreateAsync(username, name, lastName, password);
    }

    [Fact]
    public async Task First_admin_is_refused_while_an_active_admin_exists()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateFirstAdmin("second.admin", "a-long-enough-password"));
        Assert.Contains("از قبل یک مدیر فعال", ex.Message);
        using var scope = api.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AnyAsync(u => u.Username == "second.admin"));
        Assert.True(await scope.ServiceProvider.GetRequiredService<FirstAdminService>().AdminExistsAsync());
    }

    [Fact]
    public async Task First_admin_validates_input_and_never_takes_over_an_existing_account()
    {
        RemoveAllAdmins();
        foreach (var (user, password) in new (string?, string?)[] { (null, "a-long-enough-password"), (" ", "a-long-enough-password"), ("has space", "a-long-enough-password"),
                                                                    (new string('u', 101), "a-long-enough-password"), ("ok.user", null), ("ok.user", "short"), ("ok.user", new string('p', 129)) })
            await Assert.ThrowsAsync<ArgumentException>(() => CreateFirstAdmin(user, password));
        await Assert.ThrowsAsync<ArgumentException>(() => CreateFirstAdmin("ok.user", "a-long-enough-password", name: " "));
        await Assert.ThrowsAsync<ArgumentException>(() => CreateFirstAdmin("member", "a-long-enough-password"));        // حساب موجود ارتقا داده نمی‌شود
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Users.AnyAsync(u => u.Username == "ok.user"));
        Assert.Empty(db.UserRoles.Where(r => r.User.Username == "member" && r.Role.RoleCode == "ADMIN"));
    }

    [Fact]
    public async Task First_admin_gets_the_admin_role_a_hashed_password_and_can_sign_in()
    {
        RemoveAllAdmins();
        const string password = "first-admin-password-1";
        var id = await CreateFirstAdmin("  first.admin  ", password, "علی", "احمدی");
        using (var scope = api.Services.CreateScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Include(u => u.UserRoles).ThenInclude(r => r.Role).SingleAsync(u => u.UserId == id);
            Assert.Equal("first.admin", user.Username);
            Assert.True(user.IsActive);
            Assert.Equal(["ADMIN"], user.UserRoles.Select(r => r.Role.RoleCode));
            Assert.DoesNotContain(password, user.PasswordHash!);                                                   // فقط هش
            Assert.True(scope.ServiceProvider.GetRequiredService<IPasswordService>().Verify(user, password));
            Assert.False(scope.ServiceProvider.GetRequiredService<IPasswordService>().Verify(user, password + "x"));
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateFirstAdmin("another.admin", password));       // حالا مدیر فعال هست

        await Login("first.admin", password);                                                                       // ورود واقعی از راه API
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/dashboard")).StatusCode);
    }

    [Fact]
    public void First_admin_command_reads_options_and_the_wrapper_never_prints_or_passes_the_password_as_an_argument()
    {
        var args = new[] { OrganizationIntranet.Api.FirstAdminCommand.Flag, "--username", "u", "--name", "n", "--last-name", "l" };
        Assert.Equal("u", OrganizationIntranet.Api.FirstAdminCommand.Option(args, "--username"));
        Assert.Equal("l", OrganizationIntranet.Api.FirstAdminCommand.Option(args, "--last-name"));
        Assert.Null(OrganizationIntranet.Api.FirstAdminCommand.Option(args, "--password"));                          // رمز آرگومان نیست
        Assert.Null(OrganizationIntranet.Api.FirstAdminCommand.Option(["--username"], "--username"));

        var script = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tools", "New-FirstAdmin.ps1")));
        Assert.Contains("Read-Host -AsSecureString", script);
        Assert.DoesNotContain("--password", script);
        Assert.DoesNotMatch(@"(?i)Write-(Host|Output)[^\r\n]*\$password", script);
        Assert.Contains("Remove-Item Env:INTRANET_FIRST_ADMIN_PASSWORD", script);
    }
}
