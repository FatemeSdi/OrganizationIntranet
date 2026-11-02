using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OrganizationIntranet.Api.OpenApi;

/// <summary>در Swagger برای هر مسیر نشان می‌دهد چه اعتباری لازم است: کلید UI، توکن Bearer (مگر مسیر ناشناس) و کلید مدیریتی (برای مسیرهای [AdminClient])؛ مسیرهای سامانه‌های دیگر فقط کلید همان سامانه.</summary>
public sealed class ApiSecurityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<OrganizationIntranet.Api.Security.ApplicationIntegrationAttribute>().Any())
        {
            operation.Security = new List<OpenApiSecurityRequirement> { new() { [Reference("ApplicationIntegration")] = Array.Empty<string>() } };
            return;
        }
        var security = new OpenApiSecurityRequirement
        {
            [Reference("ClientKey")] = Array.Empty<string>()
        };
        if (!context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            security[Reference("Bearer")] = Array.Empty<string>();
        if (context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<OrganizationIntranet.Api.Security.AdminClientAttribute>().Any())
            security[Reference("AdminClient")] = Array.Empty<string>();
        operation.Security = new List<OpenApiSecurityRequirement> { security };
    }

    private static OpenApiSecurityScheme Reference(string id) => new()
    {
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = id }
    };
}
