extern alias PortalEntry;
extern alias AdminEntry;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using DNTCaptcha.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Api.Security;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;
using Xunit;

namespace OrganizationIntranet.SmokeTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string ClientKey = "integration-test-only-key-0000000000000000";
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public ApiFactory()
    {
        connection.Open();
        connection.CreateFunction("GETUTCDATE", () => DateTime.UtcNow);
        connection.CreateFunction("GETDATE", () => DateTime.Now);
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Api:ClientKey", ClientKey);
        builder.UseSetting("ConnectionStrings:DefaultConnection", "unused-in-tests");
        builder.ConfigureServices(services => {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        });
    }
    public void Seed()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        var admin = new Role { RoleName = "مدیر", RoleCode = "ADMIN", IsActive = true };
        var member = new Role { RoleName = "کارمند", RoleCode = "MEMBER", IsActive = true };
        var hasher = new PasswordService();
        foreach (var (username, role) in new[] { ("admin", admin), ("member", member) }) {
            var user = new User { Username = username, Name = "نام", LastName = "آزمایشی", NationalId = username == "admin" ? "1111111111" : "2222222222", MobileNumber = "09120000000", IsActive = true };
            user.PasswordHash = hasher.Hash(user, "test-password");
            user.UserRoles.Add(new UserRole { Role = role }); db.Users.Add(user);
        }
        db.Applications.Add(new OrganizationIntranet.Domain.Entities.Application { ApplicationName = "سامانه", ApplicationCode = "TEST", IsActive = true });
        db.SaveChanges();
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) connection.Dispose(); }
}

public sealed class PortalSmokeTests : IDisposable
{
    private readonly ApiFactory api = new();
    private readonly HttpClient client;
    private string refreshToken = "";
    public PortalSmokeTests()
    {
        client = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Intranet-Client", ApiFactory.ClientKey);
        api.Seed();
    }
    private async Task<string> Login(string username = "admin", string password = "test-password")
    {
        var response = await client.PostAsJsonAsync("/api/account/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = json.RootElement.GetProperty("accessToken").GetString()!;
        refreshToken = json.RootElement.GetProperty("refreshToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return token;
    }
    [Fact]
    public async Task Swagger_is_available_in_development_without_weakening_API_security()
    {
        using var browser = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        Assert.Equal("swagger", (await browser.GetAsync("/")).Headers.Location!.ToString());
        (await browser.GetAsync("/swagger/index.html")).EnsureSuccessStatusCode();
        var document = await browser.GetStringAsync("/swagger/v1/swagger.json");
        using var json = JsonDocument.Parse(document);
        var paths = json.RootElement.GetProperty("paths");
        var login = paths.GetProperty("/api/account/login").GetProperty("post").GetProperty("security")[0];
        Assert.True(login.TryGetProperty("ClientKey", out _));
        Assert.False(login.TryGetProperty("Bearer", out _));
        var admin = paths.GetProperty("/api/admin/users").GetProperty("get").GetProperty("security")[0];
        Assert.True(admin.TryGetProperty("ClientKey", out _));
        Assert.True(admin.TryGetProperty("Bearer", out _));
        Assert.DoesNotContain(ApiFactory.ClientKey, document);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/admin/users")).StatusCode);
    }
    [Fact]
    public async Task Swagger_is_not_exposed_in_production()
    {
        using var factory = new ApiFactory();
        using var production = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var browser = production.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        browser.DefaultRequestHeaders.Add("X-Intranet-Client", ApiFactory.ClientKey);
        foreach (var path in new[] { "/swagger/index.html", "/swagger/v1/swagger.json" })
            Assert.False((await browser.GetAsync(path)).IsSuccessStatusCode);
    }
    [Fact]
    public async Task Api_enforces_client_key_authentication_and_admin_role()
    {
        using var external = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.Unauthorized, (await external.PostAsJsonAsync("/api/account/login", new LoginRequest("admin", "test-password"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/users")).StatusCode);
        await Login("member");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/users")).StatusCode);
        var profile = await client.GetFromJsonAsync<ProfileDto>("/api/account/me");
        Assert.Equal("member", profile!.Username);
        await Login();
        var users = await client.GetStringAsync("/api/admin/users");
        Assert.DoesNotContain("passwordHash", users, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, (await client.GetFromJsonAsync<DashboardDto>("/api/admin/dashboard"))!.TotalUsers);
    }
    [Fact]
    public async Task Admin_operations_and_password_flows_round_trip()
    {
        await Login();
        (await client.PostAsJsonAsync("/api/admin/roles", new RoleRequest("جدید", "NEW"))).EnsureSuccessStatusCode();
        var roles = (await client.GetFromJsonAsync<List<RoleDto>>("/api/admin/roles"))!;
        var id = roles.Single(r => r.RoleCode == "NEW").RoleId;
        (await client.PostAsJsonAsync("/api/admin/applications", new ApplicationRequest("جدید", "NEW", "https://example.org", null, 2))).EnsureSuccessStatusCode();
        var applications = (await client.GetFromJsonAsync<List<ApplicationDto>>("/api/admin/applications"))!;
        var appId = applications.Single(a => a.ApplicationCode == "NEW").ApplicationId;
        (await client.PostAsJsonAsync("/api/admin/permissions", new PermissionRequest("مجوز", "VIEW", appId, null))).EnsureSuccessStatusCode();
        var permission = (await client.GetFromJsonAsync<List<PermissionDto>>("/api/admin/permissions"))!.Single();
        (await client.PostAsJsonAsync($"/api/admin/permissions/{permission.PermissionId}/roles", new RolesRequest([id, id]))).EnsureSuccessStatusCode();
        Assert.Single((await client.GetFromJsonAsync<PermissionDto>($"/api/admin/permissions/{permission.PermissionId}"))!.SelectedRoleIds);
        (await client.PostAsJsonAsync("/api/admin/users", new UserEditRequest { Name = "جدید", LastName = "آزمایش", Username = "new", NewPassword = "initial-password", SelectedRoleIds = [id, id] })).EnsureSuccessStatusCode();
        var user = (await client.GetFromJsonAsync<List<UserDto>>("/api/admin/users"))!.Single(u => u.Username == "new");
        Assert.Single(user.SelectedRoleIds);
        (await client.PostAsJsonAsync("/api/admin/users", new UserEditRequest { Id = user.UserId, Name = "ویرایش", LastName = user.LastName, Username = user.Username, SelectedRoleIds = [] })).EnsureSuccessStatusCode();
        Assert.Empty((await client.GetFromJsonAsync<UserDto>($"/api/admin/users/{user.UserId}"))!.SelectedRoleIds);
        foreach (var path in new[] { $"roles/{id}", $"applications/{appId}", $"permissions/{permission.PermissionId}" })
            (await client.PostAsync($"/api/admin/{path}/toggle", null)).EnsureSuccessStatusCode();
        Assert.False((await client.GetFromJsonAsync<List<RoleDto>>("/api/admin/roles"))!.Single(r => r.RoleId == id).IsActive);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/users", new UserEditRequest { Name = "bad", LastName = "bad", Username = "invalid-role", NewPassword = "test", SelectedRoleIds = [999] })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/admin/roles", new RoleRequest("تکرار", "ADMIN"))).StatusCode);
        (await client.PostAsJsonAsync("/api/account/change-password", new ChangePasswordRequest("test-password", "changed-password", "changed-password"))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);
        await Login("admin", "changed-password");
        // Identity information alone can no longer reset a password; SMS recovery defaults to disabled.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/forgot-password", new ResetPasswordRequest("admin", "1111111111", "09120000000", "reset", "reset"))).StatusCode);
    }
    [Fact]
    public async Task Publications_enforce_visibility_validation_and_admin_access()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/publications")).StatusCode);
        await Login();
        var request = new PublicationEditRequest { Title = "خبر آزمایشی", Summary = "خلاصه", Body = "<script>alert(1)</script>", Kind = "News" };
        var response = await client.PostAsJsonAsync("/api/admin/publications", request);
        response.EnsureSuccessStatusCode();
        var draft = (await response.Content.ReadFromJsonAsync<PublicationDto>())!;
        Assert.Null(draft.PublishedAt);
        await Login("member");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/publications")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/admin/publications/{draft.PublicationId}", request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/publications/{draft.PublicationId}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<PublicationListDto>("/api/publications"))!.Items);
        await Login();
        request.IsPublished = true;
        request.Revision = draft.Revision;
        (await client.PostAsJsonAsync($"/api/admin/publications/{draft.PublicationId}", request)).EnsureSuccessStatusCode();
        foreach (var kind in new[] { "Announcement", "Circular" })
        {
            request.Kind = kind;
            (await client.PostAsJsonAsync("/api/admin/publications", request)).EnsureSuccessStatusCode();
        }
        request.Kind = "invalid";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/publications", request)).StatusCode);
        request.Kind = "News"; request.Title = " ";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/publications", request)).StatusCode);
        request.Title = new string('x', 201);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/publications", request)).StatusCode);
        request.Title = "ویرایش";
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/admin/publications/99999", request)).StatusCode);
        var token = await Login("member");
        var published = (await client.GetFromJsonAsync<PublicationDto>($"/api/publications/{draft.PublicationId}"))!;
        Assert.NotNull(published.PublishedAt);
        Assert.Equal(3, (await client.GetFromJsonAsync<PublicationListDto>("/api/publications"))!.Total);
        Assert.Single((await client.GetFromJsonAsync<PublicationListDto>("/api/publications?kind=Circular&search=" + Uri.EscapeDataString("آزمایشی")))!.Items);
        Assert.Empty((await client.GetFromJsonAsync<PublicationListDto>("/api/publications?page=2"))!.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/publications?page=0")).StatusCode);
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var browser = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        browser.DefaultRequestHeaders.Add("Cookie", Cookie(portal.Services, token, false));
        var html = await browser.GetStringAsync($"/Publications/Details/{draft.PublicationId}");
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        await Login(); request.IsPublished = false;
        request.Revision = published.Revision;
        (await client.PostAsJsonAsync($"/api/admin/publications/{draft.PublicationId}", request)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/Publications/Details/{draft.PublicationId}")).StatusCode);
    }

    [Fact]
    public async Task Publication_stale_or_missing_revision_cannot_overwrite_saved_content()
    {
        await Login();
        var request = new PublicationEditRequest { Title = "نسخه اول", Summary = "خلاصه", Body = "متن", Kind = "News", IsPublished = true };
        var created = await client.PostAsJsonAsync("/api/admin/publications", request);
        created.EnsureSuccessStatusCode();
        var original = (await created.Content.ReadFromJsonAsync<PublicationDto>())!;
        var url = $"/api/admin/publications/{original.PublicationId}";
        request.Title = "نسخه دوم";
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(url, request)).StatusCode);
        request.Revision = original.Revision;
        var saved = await client.PostAsJsonAsync(url, request);
        saved.EnsureSuccessStatusCode();
        var current = (await saved.Content.ReadFromJsonAsync<PublicationDto>())!;
        Assert.Equal(original.Revision + 1, current.Revision);
        Assert.Equal(original.PublishedAt, current.PublishedAt);
        request.Title = "ویرایش قدیمی";
        request.IsPublished = false;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(url, request)).StatusCode);
        var visible = (await client.GetFromJsonAsync<PublicationDto>($"/api/publications/{original.PublicationId}"))!;
        Assert.Equal("نسخه دوم", visible.Title);
        Assert.True(visible.IsPublished);
    }

    [Fact]
    public async Task Publication_editor_requires_antiforgery_and_saves_content()
    {
        var token = await Login();
        using var admin = Ui<AdminEntry::Program>("Admin");
        using var browser = admin.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        browser.DefaultRequestHeaders.Add("Cookie", Cookie(admin.Services, token, true));
        var values = new Dictionary<string,string> { ["Input.Title"] = "مطلب فرم", ["Input.Summary"] = "خلاصه", ["Input.Body"] = "متن", ["Input.Kind"] = "Circular", ["Input.IsPublished"] = "true" };
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync("/Admin/Publications/Edit", new FormUrlEncodedContent(values))).StatusCode);
        values["__RequestVerificationToken"] = await Antiforgery(browser, "/Admin/Publications/Edit");
        Assert.NotEmpty(values["__RequestVerificationToken"]);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync("/Admin/Publications/Edit", new FormUrlEncodedContent(values))).StatusCode);
        var item = Assert.Single((await client.GetFromJsonAsync<PublicationListDto>("/api/publications"))!.Items);
        Assert.Equal("Circular", item.Kind);
        var editUrl = $"/Admin/Publications/Edit?id={item.PublicationId}";
        var editor = await browser.GetStringAsync(editUrl);
        Assert.Contains("name=\"Input.Revision\"", editor);
        values["Id"] = item.PublicationId.ToString();
        values["Input.Revision"] = item.Revision.ToString();
        values["Input.Title"] = "Updated through editor";
        values["__RequestVerificationToken"] = await Antiforgery(browser, editUrl);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync(editUrl, new FormUrlEncodedContent(values))).StatusCode);
        values["Input.Title"] = "Unsaved stale editor text";
        var conflict = await browser.PostAsync(editUrl, new FormUrlEncodedContent(values));
        Assert.Equal(HttpStatusCode.OK, conflict.StatusCode);
        Assert.Contains("Unsaved stale editor text", await conflict.Content.ReadAsStringAsync());
        Assert.Equal("Updated through editor", (await client.GetFromJsonAsync<PublicationDto>($"/api/admin/publications/{item.PublicationId}"))!.Title);
        (await browser.GetAsync("/Admin/Publications")).EnsureSuccessStatusCode();
    }

    private WebApplicationFactory<T> Ui<T>(string name) where T : class => new WebApplicationFactory<T>().WithWebHostBuilder(builder => {
        builder.UseSetting("Api:ClientKey", ApiFactory.ClientKey);
        builder.UseSetting("Ui:Name", name);
        builder.ConfigureServices(services => services.AddHttpClient("ApiClient").ConfigurePrimaryHttpMessageHandler(() => api.Server.CreateHandler()));
    });
    private static string Cookie(IServiceProvider services, string token, bool admin, string? refresh = null)
    {
        var options = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var claims = new List<Claim> { new("api_token", token), new(ClaimTypes.NameIdentifier, admin ? "1" : "2"), new(ClaimTypes.GivenName, "نام"), new(ClaimTypes.Surname, "آزمایشی") };
        if (admin) claims.Add(new Claim(ClaimTypes.Role, "ADMIN"));
        if (refresh is not null) claims.Add(new Claim("api_refresh", refresh));
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)), new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow.AddDays(refresh is null ? 0 : -8), ExpiresUtc = DateTimeOffset.UtcNow.AddDays(refresh is null ? 14 : 6), AllowRefresh = true }, CookieAuthenticationDefaults.AuthenticationScheme);
        return options.Cookie.Name + "=" + options.TicketDataFormat.Protect(ticket);
    }
    private static async Task<string> Antiforgery(HttpClient ui, string path)
    {
        var html = await ui.GetStringAsync(path);
        return WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    }
    [Fact]
    public async Task Both_UIs_render_protected_pages_assets_and_submit_admin_form()
    {
        var token = await Login();
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var admin = Ui<AdminEntry::Program>("Admin");
        var options = new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false };
        using var portalClient = portal.CreateClient(options);
        using var adminClient = admin.CreateClient(options);
        Assert.Equal(HttpStatusCode.Redirect, (await portalClient.GetAsync("/Portal")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await adminClient.GetAsync("/Admin")).StatusCode);
        foreach (var ui in new[] { portalClient, adminClient }) {
            (await ui.GetAsync("/Account/Login")).EnsureSuccessStatusCode();
            (await ui.GetAsync("/Account/ForgotPassword")).EnsureSuccessStatusCode();
            (await ui.GetAsync("/assets/libs/bootstrap/css/bootstrap.rtl.min.css")).EnsureSuccessStatusCode();
        }
        var cookie = Cookie(portal.Services, token, true);
        portalClient.DefaultRequestHeaders.Add("Cookie", cookie);
        adminClient.DefaultRequestHeaders.Add("Cookie", cookie); // Same protected cookie is valid in both UI hosts.
        foreach (var path in new[] { "/Portal", "/Account/Profile" }) (await portalClient.GetAsync(path)).EnsureSuccessStatusCode();
        foreach (var path in new[] { "/Admin", "/Admin/Users", "/Admin/Users/Edit", "/Admin/Users/Edit?id=1", "/Admin/Roles", "/Admin/Applications", "/Admin/Permissions", "/Account/Profile" }) (await adminClient.GetAsync(path)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/admin/permissions", new PermissionRequest("مجوز", "VIEW", 1, null))).EnsureSuccessStatusCode();
        (await adminClient.GetAsync("/Admin/Permissions/Assign?id=1")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await adminClient.PostAsync("/Admin/Roles?handler=Create", new FormUrlEncodedContent(new Dictionary<string,string> { ["RoleName"] = "bad", ["RoleCode"] = "BAD" }))).StatusCode);
        var csrf = await Antiforgery(adminClient, "/Admin/Roles"); Assert.NotEmpty(csrf);
        var response = await adminClient.PostAsync("/Admin/Roles?handler=Create", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = csrf, ["RoleName"] = "فرم", ["RoleCode"] = "FORM" }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains((await client.GetFromJsonAsync<List<RoleDto>>("/api/admin/roles"))!, r => r.RoleCode == "FORM");
        Assert.Equal(HttpStatusCode.Redirect, (await portalClient.GetAsync("/Account/Logout")).StatusCode);
    }
    [Fact]
    public async Task Login_requires_captcha_and_admin_pages_reject_member_cookie()
    {
        using var admin = Ui<AdminEntry::Program>("Admin");
        using var ui = admin.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var csrf = await Antiforgery(ui, "/Account/Login");
        var response = await ui.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = csrf, ["Username"] = "admin", ["Password"] = "test-password" }));
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        ui.DefaultRequestHeaders.Add("Cookie", Cookie(admin.Services, await Login("member"), false));
        response = await ui.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("AccessDenied", response.Headers.Location!.ToString());
    }
    private sealed class AcceptedCaptcha : IDNTCaptchaValidatorService
    {
        public bool HasRequestValidCaptchaEntry() => true;
    }
    [Theory]
    [InlineData("/Admin/Users/Edit?id=1", "/Admin/Users/Edit?id=1")]
    [InlineData("https://example.org", "/Admin?welcome=true")]
    [InlineData("//example.org", "/Admin?welcome=true")]
    public async Task Login_preserves_local_menu_destination_only(string returnUrl, string expected)
    {
        using var admin = Ui<AdminEntry::Program>("Admin").WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IDNTCaptchaValidatorService, AcceptedCaptcha>()));
        using var ui = admin.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var csrf = await Antiforgery(ui, "/Account/Login?ReturnUrl=" + Uri.EscapeDataString(returnUrl));
        var response = await ui.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["__RequestVerificationToken"] = csrf, ["Username"] = "admin", ["Password"] = "test-password", ["ReturnUrl"] = returnUrl
        }));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.EndsWith(expected, json.RootElement.GetProperty("redirectUrl").GetString());
    }

    [Fact]
    public async Task Admin_menu_routes_and_assets_work_on_index_and_child_pages()
    {
        var token = await Login();
        using var admin = Ui<AdminEntry::Program>("Admin");
        using var ui = admin.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        ui.DefaultRequestHeaders.Add("Cookie", Cookie(admin.Services, token, true));
        (await client.PostAsJsonAsync("/api/admin/permissions", new PermissionRequest("مجوز", "VIEW", 1, null))).EnsureSuccessStatusCode();
        var routes = new[] { "/Admin", "/Admin/Index", "/Admin/Users", "/Admin/Users/Index", "/Admin/Users/Edit?id=1", "/Admin/Roles", "/Admin/Applications", "/Admin/Permissions", "/Admin/Permissions/Assign?id=1", "/Account/Profile" };
        foreach (var route in routes)
        {
            var html = await ui.GetStringAsync(route);
            Assert.Contains("id=\"mainSidebarToggle\"", html);
            var active = Regex.Matches(html, "class=\"side-menu__item active\"");
            Assert.Equal(route.StartsWith("/Admin") ? 1 : 0, active.Count);
            foreach (Match match in Regex.Matches(html, "<script[^>]+src=\"([^\"]+)\""))
                (await ui.GetAsync(WebUtility.HtmlDecode(match.Groups[1].Value))).EnsureSuccessStatusCode();
            Assert.Single(Regex.Matches(html, "src=\"[^\"]*bootstrap.bundle.min.js"));
            // Optional static fixtures contain only the in-memory test users, never live data.
            var fixturePath = Environment.GetEnvironmentVariable("INTRANET_UI_FIXTURES");
            if (!string.IsNullOrEmpty(fixturePath))
            {
                var path = Path.Combine(fixturePath, route.Split('?')[0].TrimStart('/'), "index.html");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, html);
            }
        }
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sliding_cookie_renews_API_tokens_or_signs_out(bool valid)
    {
        var token = await Login();
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var ui = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        ui.DefaultRequestHeaders.Add("Cookie", Cookie(portal.Services, token, true, valid ? refreshToken : "invalid"));
        var response = await ui.GetAsync("/Portal");
        Assert.Equal(valid ? HttpStatusCode.OK : HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.Contains("OrganizationIntranet.UI"));
    }
    [Theory]
    [InlineData("admin", "/Admin?welcome=true")]
    [InlineData("member", "/Portal")]
    public async Task Successful_UI_login_profile_password_and_logout(string username, string destination)
    {
        // Only the CAPTCHA verdict is substituted here. The separate negative test uses the real validator.
        using var portal = Ui<PortalEntry::Program>("Portal").WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IDNTCaptchaValidatorService, AcceptedCaptcha>()));
        using var ui = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var csrf = await Antiforgery(ui, "/Account/Login");
        var response = await ui.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = csrf, ["Username"] = username, ["Password"] = "test-password", ["RememberMe"] = "true" }));
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.EndsWith(destination, json.RootElement.GetProperty("redirectUrl").GetString());
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.Contains("OrganizationIntranet.UI") && cookie.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        (await ui.GetAsync("/Portal")).EnsureSuccessStatusCode();
        csrf = await Antiforgery(ui, "/Account/Profile");
        response = await ui.PostAsync("/Account/Profile?handler=ChangePassword", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = csrf, ["CurrentPassword"] = "test-password", ["NewPassword"] = "ui-password", ["ConfirmNewPassword"] = "ui-password" }));
        using var passwordResult = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(passwordResult.RootElement.GetProperty("success").GetBoolean());
        await Login(username, "ui-password");
        Assert.Equal(HttpStatusCode.Redirect, (await ui.GetAsync("/Account/Logout")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await ui.GetAsync("/Portal")).StatusCode);
    }
    public void Dispose() { client.Dispose(); api.Dispose(); }
}
