using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrganizationIntranet.Api.Integrations;
using OrganizationIntranet.Api.Security;
using OrganizationIntranet.Application.Abstractions;
using Xunit;

namespace OrganizationIntranet.Tests;

/// <summary>
/// آزمون واقعی HTTP روی loopback: کلاینت و ثبت DI همان‌هایی‌اند که در Program.cs استفاده می‌شوند و فقط درگاه شبیه‌سازی شده است.
/// این آزمون‌ها روی شبکه‌ی بیرونی هم اجرا می‌شوند و به 10.2.79.10 وصل نمی‌شوند.
/// </summary>
[Trait("Category", "Integration")]
public sealed class InternalSmsGatewayLoopbackTests
{
    private static ServiceProvider Build(FakeSmsProvider provider, string appName = FakeSmsProvider.RegisteredApp)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Sms:BaseUrl"] = provider.BaseUrl, ["Sms:AppName"] = appName }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInternalSmsGateway();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddScoped<IAuthenticationProviders, AuthenticationProviders>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Persian_text_and_reserved_characters_arrive_intact_and_cannot_add_parameters()
    {
        await using var provider = await FakeSmsProvider.StartAsync();
        using var services = Build(provider);
        var text = "درخواست «سامانه A&B» #12 +x %26 y\nشماره: ۱۲۳";
        await services.GetRequiredService<ISmsGateway>().SendAsync("09121234567", text, default);

        var sms = Assert.Single(provider.Messages);
        Assert.Equal(text, sms.Message);
        Assert.Equal("09121234567", sms.To);
        Assert.Equal(FakeSmsProvider.RegisteredApp, sms.AppName);
        Assert.Equal(2, sms.RawQuery.Count(c => c == '&')); // فقط appName، to و msg؛ متن پیام پارامتر اضافه نمی‌سازد.
    }

    [Fact]
    public async Task Unregistered_app_name_is_reported_as_a_failure()
    {
        await using var provider = await FakeSmsProvider.StartAsync();
        using var services = Build(provider, appName: "IDMS-lookalike");
        await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<ISmsGateway>().SendAsync("09121234567", "x", default));
        Assert.Equal(1, provider.Calls);
        Assert.Empty(provider.Messages);
    }

    [Theory]
    [InlineData("redirect")]
    [InlineData("server-error")]
    [InlineData("failure-body")]
    public async Task Abnormal_provider_responses_are_failures_and_redirects_are_not_followed(string behaviour)
    {
        await using var provider = await FakeSmsProvider.StartAsync();
        provider.Override = _ => behaviour switch
        {
            "redirect" => Results.Redirect("/api/SMSProvider?appName=OrganizationIntranet&to=09121234567&msg=hijack"),
            "server-error" => Results.Text("success", statusCode: 500),
            _ => Results.Text("failed"),
        };
        using var services = Build(provider);
        await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<ISmsGateway>().SendAsync("09121234567", "x", default));
        Assert.Equal(1, provider.Calls); // redirect دنبال نشد؛ اعتبارنامه یا پیام به مقصد دیگری نمی‌رود.
    }

    [Fact]
    public async Task Access_request_sender_delivers_through_the_gateway()
    {
        await using var provider = await FakeSmsProvider.StartAsync();
        using var services = Build(provider);
        // وابستگی‌های مسیر HTTPS/Bearer (دیتابیس تنظیمات و کلاینت‌ها) وقتی درگاه فعال است نباید لمس شوند.
        await new AccessRequestSmsSender(null!, null!, null!, services.GetRequiredService<ISmsGateway>())
            .SendAsync("09121234567", "درخواست دسترسی جدید به «حضور و غیاب» ثبت شده است.\nشماره درخواست: 42", "intranet-access-request-7", default);
        var sms = Assert.Single(provider.Messages);
        Assert.Contains("حضور و غیاب", sms.Message);
        Assert.Contains("42", sms.Message);
    }

    [Fact]
    public async Task Login_code_is_delivered_through_the_gateway_and_reports_internal_sms_as_available()
    {
        await using var provider = await FakeSmsProvider.StartAsync();
        using var services = Build(provider);
        using var scope = services.CreateScope();
        var providers = scope.ServiceProvider.GetRequiredService<IAuthenticationProviders>();
        Assert.True(providers.InternalSmsAvailable);
        await providers.SendSmsAsync(new(), "", "09121234567", "654321");
        var sms = Assert.Single(provider.Messages);
        Assert.Contains("654321", sms.Message);
        Assert.StartsWith("کد تأیید اینترانت", sms.Message);
    }

    [Fact]
    public async Task Test_host_never_reaches_the_real_gateway()
    {
        // appsettings.json پروژه‌ی API نام برنامه را دارد؛ ApiFactory باید آن را خنثی کند تا هیچ آزمونی پیامک واقعی نفرستد.
        using var factory = new ApiFactory();
        var gateway = factory.Services.GetRequiredService<ISmsGateway>();
        Assert.False(gateway.IsConfigured);
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.SendAsync("09121234567", "x", default));
    }

    private static DirectoryInfo? SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "") => new FileInfo(sourceFile).Directory;

    [Fact]
    public void Production_configuration_names_the_intranet_app_and_never_the_idms_app()
    {
        // فایل منبع پروژه‌ی Api؛ appsettings داخل خروجی آزمون ممکن است از Portal/Admin کپی شده باشد و مسیر خروجی به ریشه‌ی مخزن وابسته نیست.
        var directory = SourceDirectory();
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OrganizationIntranet.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var api = Path.Combine(directory!.FullName, "src", "OrganizationIntranet.Api", "appsettings.json");
        var configuration = new ConfigurationBuilder().AddJsonFile(api, optional: false).Build();
        Assert.Equal("OrganizationIntranet", configuration["Sms:AppName"]);
        Assert.NotEqual("IDMS", configuration["Sms:AppName"]);
    }
}
