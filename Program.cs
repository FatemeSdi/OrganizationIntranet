using DNTCaptcha.Core;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Authorization;
using OrganizationIntranet.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// DNTCaptcha serves its captcha image through its own internal MVC controller,
// so plain controller routing must be registered even though this app has none of its own.
builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDNTCaptcha(options =>
{
    options.UseCookieStorageProvider()
           .ShowThousandsSeparators(false)
           .WithEncryptionKey(builder.Configuration["DNTCaptcha:EncryptionKey"] ?? "OrganizationIntranet-Captcha-Key");
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
    });

// امکان [Authorize(Policy = "Permission:USER_VIEW")] را بدون ثبت دستی هر Policy فراهم می‌کند.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// جلوگیری از کش شدن صفحات پویا در مرورگر، تا دکمه‌ی "بازگشت" بعد از خروج از حساب
// دوباره همان صفحه‌ی محافظت‌شده را از حافظه‌ی مرورگر نشان ندهد.
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
    context.Response.Headers.Pragma = "no-cache";
    context.Response.Headers.Expires = "-1";
    await next();
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/Account/Login"));

app.MapControllers();
app.MapRazorPages();

app.Run();
