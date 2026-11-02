namespace OrganizationIntranet.Api.Security;

/// <summary>برچسب مسیرهای ویژه‌ی سامانه‌های دیگر: با کلید همان سامانه احراز می‌شوند نه با توکن کاربر.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ApplicationIntegrationAttribute : Attribute
{
    /// <summary>نام کلید در <c>ApplicationIntegrations:{CODE}:{KeyName}</c>؛ هر کاربرد (دریافت اعلان، SSO) کلید جدای خودش را دارد.</summary>
    public string KeyName { get; init; } = "PushKey";
}
