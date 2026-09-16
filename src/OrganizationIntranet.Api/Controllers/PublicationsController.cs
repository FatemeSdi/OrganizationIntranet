using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Controllers;

[ApiController, Route("api/publications"), Authorize]
public sealed class PublicationsController(PublicationService service) : ControllerBase
{
    [HttpGet] public Task<PublicationListDto> List(string? kind = null, string? search = null, int page = 1) => service.ListAsync(true, kind, search, page);
    [HttpGet("{id:long}")] public Task<PublicationDto> Get(long id) => service.GetAsync(id, true);
}

[ApiController, Route("api/admin/publications"), Authorize(Roles = "ADMIN")]
public sealed class PublicationAdminController(PublicationService service, ILogger<PublicationAdminController> logger) : ControllerBase
{
    [HttpGet] public Task<PublicationListDto> List(string? kind = null, string? search = null, int page = 1) => service.ListAsync(false, kind, search, page);
    [HttpGet("{id:long}")] public Task<PublicationDto> Get(long id) => service.GetAsync(id, false);
    [HttpPost] public Task<PublicationDto> Create(PublicationEditRequest request) => Save(null, request);
    [HttpPost("{id:long}")] public Task<PublicationDto> Update(long id, PublicationEditRequest request) => Save(id, request);
    private async Task<PublicationDto> Save(long? id, PublicationEditRequest request)
    {
        var actor = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await service.SaveAsync(id, request, actor);
        logger.LogInformation("Publication {PublicationId} saved by {UserId}; published: {Published}", result.PublicationId, actor, result.IsPublished);
        return result;
    }
}
