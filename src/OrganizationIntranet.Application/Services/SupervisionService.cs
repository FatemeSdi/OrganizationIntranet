using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Services;

public sealed class SupervisionService(ISupervisionRepository repository, IApplicationCatalogRepository catalog)
{
    public async Task<List<SupervisionGroupDto>> GroupsAsync(int applicationId)
    {
        _ = await catalog.FindAsync(applicationId) ?? throw new KeyNotFoundException();
        return (await repository.GroupsAsync(applicationId)).Select(g => new SupervisionGroupDto(g.ApplicationSupervisionGroupId, g.ApplicationId, g.Name, g.IsActive,
            g.Members.Where(m => m.UserId.HasValue).Select(m => m.UserId!.Value).ToList(), g.Members.Where(m => m.RoleId.HasValue).Select(m => m.RoleId!.Value).ToList(), g.Revision)).ToList();
    }
    public async Task SaveGroupAsync(int applicationId, SupervisionGroupRequest input)
    {
        _ = await catalog.FindAsync(applicationId) ?? throw new KeyNotFoundException();
        var name = input.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 150 || input.UserIds is null || input.RoleIds is null) throw new ArgumentException("نام و اعضای گروه معتبر نیست");
        var users = input.UserIds.Distinct().ToArray(); var roles = input.RoleIds.Distinct().ToArray();
        if (input.IsActive && users.Length + roles.Length == 0) throw new ArgumentException("گروه فعال باید حداقل یک کاربر یا نقش داشته باشد");
        if (!await repository.ValidPrincipalsAsync(users, roles)) throw new ArgumentException("اعضای انتخاب‌شده معتبر نیستند");
        var group = input.Id == 0 ? new ApplicationSupervisionGroup { ApplicationId = applicationId } : await repository.GroupAsync(applicationId, input.Id) ?? throw new KeyNotFoundException();
        if (input.Id != 0 && group.Revision != input.Revision) throw new AccessRequestConflictException();
        if (input.Id == 0) repository.AddGroup(group);
        group.Name = name; group.IsActive = input.IsActive; group.Revision = Guid.NewGuid();
        repository.SetMembers(group, users, roles); await repository.SaveAsync();
    }
    public async Task<AccessRequestAlertsDto> AlertsAsync(long userId)
    {
        var result = await repository.AlertsAsync(userId);
        return new(result.Items.Select(a => new AccessRequestAlertDto(a.AccessRequestAlertId, a.ApplicationAccessRequestId,
            a.Request.Application.ApplicationName, $"{a.Request.User.Name} {a.Request.User.LastName}".Trim(), a.CreatedAt, a.IsRead)).ToList(), result.UnreadCount);
    }
    public async Task MarkReadAsync(long userId, long? id) { await repository.MarkReadAsync(userId, id); await repository.SaveAsync(); }
    public async Task<AccessRequestListDto> InboxAsync(long userId, int page)
    {
        ValidatePage(page);
        var result = await repository.InboxAsync(userId, page, 20);
        return new(result.Items.Select(r => new AccessRequestDto(r.ApplicationAccessRequestId, r.ApplicationId, r.Application.ApplicationName,
            r.UserId, r.User.Username, $"{r.User.Name} {r.User.LastName}".Trim(), r.Reason, r.Status, r.CreatedAt, r.ResolvedAt,
            null, r.ReviewNote, r.Revision)).ToList(), result.Total, page, 20);
    }
    public async Task<AccessRequestSmsListDto> SmsAsync(int page)
    {
        ValidatePage(page);
        var result = await repository.SmsAsync(page, 20);
        return new(result.Items.Select(a => new AccessRequestSmsDto(a.AccessRequestAlertId, a.ApplicationAccessRequestId, a.Request.Application.ApplicationName,
            $"{a.User.Name} {a.User.LastName} ({a.User.Username})", a.SmsStatus, a.SmsAttempts, a.SmsError, a.SmsSentAt)).ToList(), result.Total, page, 20);
    }
    public async Task RetrySmsAsync(long id)
    {
        var alert = await repository.FindSmsAsync(id) ?? throw new KeyNotFoundException();
        if (alert.SmsStatus != "Failed" || alert.Request.Status != AccessRequestStatuses.Pending) throw new ArgumentException("این پیامک قابل ارسال مجدد نیست");
        alert.SmsStatus = "Pending"; alert.SmsAttempts = 0; alert.NextAttemptAt = DateTime.UtcNow; alert.SmsError = null; alert.Revision = Guid.NewGuid();
        await repository.SaveAsync();
    }
    private static void ValidatePage(int page) { if (page < 1 || page > 100000) throw new ArgumentException("شماره صفحه معتبر نیست"); }
}
