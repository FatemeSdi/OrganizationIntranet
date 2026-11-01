namespace OrganizationIntranet.Api.Security;

// Endpoint metadata: the requirement follows controller routing, including encoded/case variants.
/// <summary>برچسب مسیرهای مدیریت: AdminClientMiddleware برای آن‌ها کلید مدیریتی را می‌خواهد. به‌صورت metadata مسیر است تا با حالت‌های حروف یا encode هم دور زده نشود.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdminClientAttribute : Attribute { }
