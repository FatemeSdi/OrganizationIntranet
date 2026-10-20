using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Integrations;

public sealed class NotificationPullWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<NotificationPullWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = Math.Clamp(configuration.GetValue("ApplicationIntegrations:PollIntervalSeconds", 60), 15, 3600);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(interval));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try { await PollAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { logger.LogWarning("Notification poll could not start ({ExceptionType})", ex.GetType().Name); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
    public async Task PollAsync(CancellationToken cancellationToken)
    {
        List<(int Id, string Code)> registrations;
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = await db.ApplicationServices.AsNoTracking().Where(s => s.IsActive && s.Application.IsActive
                && s.ServiceCode == ApplicationCatalogOptions.NotificationsService && s.EndpointUrl != null)
                .Select(s => new { Id = s.ApplicationServiceRegistrationId, Code = s.Application.ApplicationCode }).ToListAsync(cancellationToken);
            registrations = rows.Select(r => (r.Id, r.Code)).ToList();
        }
        foreach (var registration in registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(configuration[$"ApplicationIntegrations:{registration.Code}:PullUrl"])) continue;
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var current = await db.ApplicationServices.SingleAsync(s => s.ApplicationServiceRegistrationId == registration.Id, cancellationToken);
                if (!current.IsActive || string.IsNullOrWhiteSpace(current.EndpointUrl)) continue;
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                var batch = await scope.ServiceProvider.GetRequiredService<NotificationPullClient>().FetchAsync(registration.Code, current.EndpointUrl, current.SyncCursor, timeout.Token);
                await scope.ServiceProvider.GetRequiredService<NotificationIntegrationService>().ImportAsync(registration.Code, batch, advanceCursor: true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning("Notification pull failed for application {ApplicationCode} ({ExceptionType})", registration.Code, ex.GetType().Name);
                using var failureScope = scopes.CreateScope();
                var failureDb = failureScope.ServiceProvider.GetRequiredService<AppDbContext>();
                var service = await failureDb.ApplicationServices.FindAsync([registration.Id], cancellationToken);
                if (service is not null) { service.LastSyncError = "واکشی ناموفق بود؛ تنظیمات اتصال، پاسخ سرویس و گیرندگان را بررسی کنید."; await failureDb.SaveChangesAsync(cancellationToken); }
            }
        }
    }
}
