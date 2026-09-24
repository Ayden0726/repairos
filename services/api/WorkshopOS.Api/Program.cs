using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using WorkshopOS.Application.Common;
using WorkshopOS.Api.Hubs;
using WorkshopOS.Infrastructure;
using WorkshopOS.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "WorkshopOS API", Version = "v1" }));
builder.Services.AddSignalR();
builder.Services.AddWorkshopInfrastructure(builder.Configuration);
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyHeader().AllowAnyMethod().AllowCredentials().SetIsOriginAllowed(_ => true)));
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var ex = feature?.Error;
        if (ex is AppException appEx)
        {
            context.Response.StatusCode = appEx.StatusCode;
            await context.Response.WriteAsJsonAsync(new { title = appEx.Message, code = appEx.Code, status = appEx.StatusCode });
            return;
        }
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new { title = "An unexpected error occurred.", status = 500 });
    });
});

if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Enabled", true))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<WorkshopHub>("/hubs/workshop");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WorkshopDbContext>();
    await db.Database.MigrateAsync();
    await DbSeed.EnsureFoundationAsync(db);

    // Ensure a stable LAN pairing code exists for /connect and Windows auto-discover.
    var pairing = await DbSeed.GetSettingAsync(db, SettingKeys.PairingCode, "", CancellationToken.None);
    if (string.IsNullOrWhiteSpace(pairing))
    {
        await DbSeed.SetSettingAsync(db, SettingKeys.PairingCode, WorkshopOS.Api.Controllers.DiscoveryController.NewPairingCode(), null);
        await db.SaveChangesAsync();
    }

    var seedDemo = app.Configuration.GetValue("Seed:Demo", false);
    if (seedDemo && !await DbSeed.GetSettingAsync(db, SettingKeys.SetupCompleted, false))
    {
        // Dev-only convenience: do not auto-complete setup. Staff must run setup or explicit seed script.
    }
}

app.Run();

public partial class Program { }
