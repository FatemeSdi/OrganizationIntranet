namespace OrganizationIntranet.Api.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ApplicationIntegrationAttribute : Attribute
{
    /// <summary>نام کلید در <c>ApplicationIntegrations:{CODE}:{KeyName}</c>؛ هر کاربرد (دریافت اعلان، SSO) کلید جدای خودش را دارد.</summary>
    public string KeyName { get; init; } = "PushKey";
}
