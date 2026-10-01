using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Integrations;

public sealed class AccessRequestSmsWorker(IServiceScopeFactory scopes, ILogger<AccessRequestSmsWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try { await DispatchAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { logger.LogWarning("Access request SMS dispatch failed ({ExceptionType})", ex.GetType().Name); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
    public async Task DispatchAsync(CancellationToken cancellationToken)
    {
        List<long> ids;
        using (var listScope = scopes.CreateScope())
        {
            var db = listScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;
            ids = await db.AccessRequestAlerts.Where(a => (a.SmsStatus == "Pending" || a.SmsStatus == "Processing") && a.NextAttemptAt <= now)
                .OrderBy(a => a.NextAttemptAt).Select(a => a.AccessRequestAlertId).Take(20).ToListAsync(cancellationToken);
        }
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var alert = await db.AccessRequestAlerts.Include(a => a.User).Include(a => a.Request).ThenInclude(r => r.Application)
                .SingleAsync(a => a.AccessRequestAlertId == id, cancellationToken);
            if (alert.SmsStatus is not ("Pending" or "Processing") || alert.NextAttemptAt > DateTime.UtcNow) continue;
            if (alert.Request.Status != AccessRequestStatuses.Pending || !await SupervisionQueries.Recipients(db, alert.Request.ApplicationId).AnyAsync(u => u.UserId == alert.UserId, cancellationToken))
            { alert.SmsStatus = "Cancelled"; alert.NextAttemptAt = null; alert.Revision = Guid.NewGuid(); }
            else if (alert.SmsAttempts >= 5 || !Regex.IsMatch(alert.User.MobileNumber ?? "", @"^\+?[0-9]{10,15}$"))
            { alert.SmsStatus = "Failed"; alert.SmsError = "شماره موبایل معتبر نیست یا تعداد تلاش‌ها به حد مجاز رسیده است."; alert.NextAttemptAt = null; alert.Revision = Guid.NewGuid(); }
            else
            { alert.SmsStatus = "Processing"; alert.SmsAttempts++; alert.NextAttemptAt = DateTime.UtcNow.AddMinutes(2); alert.Revision = Guid.NewGuid(); }
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException) { continue; }
            if (alert.SmsStatus != "Processing") continue;
            try
            {
                var message = $"درخواست دسترسی جدید به «{alert.Request.Application.ApplicationName}» ثبت شده است.\nشماره درخواست: {alert.ApplicationAccessRequestId}\nکارتابل نظارت اینترانت را بررسی کنید.";
                await scope.ServiceProvider.GetRequiredService<IAccessRequestSmsSender>().SendAsync(alert.User.MobileNumber!, message, $"intranet-access-request-{alert.AccessRequestAlertId}", cancellationToken);
                alert.SmsStatus = "Sent"; alert.SmsSentAt = DateTime.UtcNow; alert.SmsError = null; alert.NextAttemptAt = null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning("Access request SMS {AlertId} failed ({ExceptionType})", id, ex.GetType().Name);
                alert.SmsStatus = alert.SmsAttempts >= 5 ? "Failed" : "Pending";
                alert.SmsError = "ارسال پیامک ناموفق بود؛ تنظیمات سرویس و شماره موبایل را بررسی کنید.";
                alert.NextAttemptAt = alert.SmsAttempts >= 5 ? null : DateTime.UtcNow.AddMinutes(Math.Pow(2, alert.SmsAttempts));
            }
            alert.Revision = Guid.NewGuid();
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException) { logger.LogWarning("SMS {AlertId} delivery state changed concurrently; retry uses the same idempotency key", id); }
        }
    }
}
