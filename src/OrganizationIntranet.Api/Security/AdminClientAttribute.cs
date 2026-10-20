namespace OrganizationIntranet.Api.Security;

// Endpoint metadata: the requirement follows controller routing, including encoded/case variants.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdminClientAttribute : Attribute { }
