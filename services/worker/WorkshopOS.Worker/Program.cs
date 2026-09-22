using Microsoft.EntityFrameworkCore;
using WorkshopOS.Infrastructure;
using WorkshopOS.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWorkshopInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();
var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WorkshopDbContext>();
    await db.Database.MigrateAsync();
}

await host.RunAsync();

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopes;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory scopes)
    {
        _logger = logger;
        _scopes = scopes;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WorkshopOS worker started (Phase 1: heartbeat only).");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<WorkshopDbContext>();
                _ = await db.Database.CanConnectAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Worker health check failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
