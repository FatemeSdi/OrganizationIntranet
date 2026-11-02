using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Services;

/// <summary>تعارض هم‌زمانی روی درخواست دسترسی: وضعیت درخواست در همین فاصله عوض شده (مثلاً مدیر تصمیم گرفته) یا درخواست مشابه باز وجود دارد. در API پاسخ ۴۰۹ می‌شود.</summary>
public sealed class AccessRequestConflictException() : Exception("وضعیت درخواست تغییر کرده یا درخواست مشابهی در انتظار بررسی است؛ صفحه را تازه کنید.");

/// <summary>منطق درخواست دسترسی: کاربر برای یک سامانه درخواست ثبت می‌کند، ناظران هشدار می‌گیرند و مدیر تأیید یا رد می‌کند. با تأیید دسترسی مستقیم کاربر ساخته می‌شود؛ تصمیم و ساخت دسترسی در یک تراکنش ذخیره می‌شود.</summary>
public sealed class AccessRequestService(IAccessRequestRepository repository, ISupervisionRepository supervision)
{
    private static AccessRequestDto Map(ApplicationAccessRequest r) => new(r.ApplicationAccessRequestId, r.ApplicationId,
        r.Application.ApplicationName, r.UserId, r.User.Username, $"{r.User.Name} {r.User.LastName}".Trim(), r.Reason,
        r.Status, r.CreatedAt, r.ResolvedAt, r.Reviewer is null ? null : $"{r.Reviewer.Name} {r.Reviewer.LastName}".Trim(), r.ReviewNote, r.Revision);
    /// <summary>سامانه‌هایی که کاربر می‌تواند برایشان درخواست دسترسی ثبت کند.</summary>
    public async Task<List<RequestableApplicationDto>> RequestableAsync(long userId) => (await repository.RequestableAsync(userId))
        .Select(a => new RequestableApplicationDto(a.ApplicationId, a.ApplicationName, a.Description)).ToList();
    /// <summary>فهرست درخواست‌ها با صفحه‌بندی ۲۰تایی. userId خالی = همه‌ی کاربران (مدیر)؛ مقدار دارد = فقط درخواست‌های همان کاربر. وضعیت نامعتبر خطا می‌دهد.</summary>
    public async Task<AccessRequestListDto> ListAsync(long? userId, string? status, int page)
    {
        status = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        if (status is not null && !AccessRequestStatuses.IsValid(status)) throw new ArgumentException("وضعیت درخواست معتبر نیست");
        if (page < 1 || page > 100000) throw new ArgumentException("شماره صفحه معتبر نیست");
        var result = await repository.ListAsync(userId, status, page, 20);
        return new(result.Items.Select(Map).ToList(), result.Total, page, 20);
    }
    /// <summary>ثبت درخواست تازه: دلیل الزامی و حداکثر ۱۰۰۰ نویسه است؛ سامانه باید فعال و بدون دسترسی و بدون درخواست باز باشد. هشدار ناظران (و پیامک در صورت تنظیم) همین‌جا در صف می‌رود.</summary>
    public async Task CreateAsync(long userId, CreateAccessRequest input)
    {
        var reason = input.Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length > 1000) throw new ArgumentException("دلیل درخواست را حداکثر در ۱۰۰۰ کاراکتر وارد کنید");
        if (!await repository.CanRequestAsync(userId, input.ApplicationId))
            throw new ArgumentException("این سامانه در دسترس شماست، درخواست باز دارد یا فعال نیست");
        var request = new ApplicationAccessRequest { UserId = userId, ApplicationId = input.ApplicationId, Reason = reason, CreatedAt = DateTime.UtcNow };
        repository.Add(request);
        await supervision.EnqueueAsync(request);
        await repository.SaveAsync();
    }
    /// <summary>لغو درخواست توسط خود کاربر؛ فقط درخواست در انتظار و با همان Revision (وگرنه تعارض).</summary>
    public async Task CancelAsync(long userId, long id, Guid revision)
    {
        var request = await repository.FindAsync(id, userId) ?? throw new KeyNotFoundException();
        EnsurePending(request, revision);
        request.Status = AccessRequestStatuses.Cancelled;
        request.ResolvedAt = DateTime.UtcNow; request.Revision = Guid.NewGuid();
        await repository.SaveAsync();
    }
    /// <summary>تأیید یا رد درخواست توسط مدیر. رد بدون توضیح پذیرفته نمی‌شود؛ تأیید برای کاربر یا سامانه‌ی غیرفعال رد می‌شود و اگر کاربر از قبل دسترسی ندارد، دسترسی مستقیم می‌سازد.</summary>
    public async Task ReviewAsync(long actor, long id, ReviewAccessRequest input)
    {
        var request = await repository.FindAsync(id) ?? throw new KeyNotFoundException();
        EnsurePending(request, input.Revision);
        var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        if (note?.Length > 1000 || (!input.Approve && note is null)) throw new ArgumentException("برای رد درخواست، دلیل را حداکثر در ۱۰۰۰ کاراکتر وارد کنید");
        if (input.Approve)
        {
            if (!request.User.IsActive || !request.Application.IsActive) throw new ArgumentException("کاربر یا سامانه غیرفعال است");
            if (!await repository.HasAccessAsync(request.UserId, request.ApplicationId)) repository.Grant(request);
        }
        request.Status = input.Approve ? AccessRequestStatuses.Approved : AccessRequestStatuses.Rejected;
        request.ReviewNote = note; request.ReviewedBy = actor;
        request.ResolvedAt = DateTime.UtcNow; request.Revision = Guid.NewGuid();
        await repository.SaveAsync(); // The access grant and decision commit in one transaction.
    }
    /// <summary>فقط درخواستِ هنوز در انتظار و با Revision یکسان قابل تغییر است؛ در غیر این صورت AccessRequestConflictException.</summary>
    private static void EnsurePending(ApplicationAccessRequest request, Guid revision)
    {
        if (request.Status != AccessRequestStatuses.Pending || request.Revision != revision) throw new AccessRequestConflictException();
    }
}
