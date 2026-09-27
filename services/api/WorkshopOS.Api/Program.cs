using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using WorkshopOS.Application.Common;
using WorkshopOS.Api.Hubs;
using WorkshopOS.Infrastructure;
using WorkshopOS.Infrastructure.Persistence;
using WorkshopOS.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// Emergency owner password reset (server access required):
//   docker compose -f docker/docker-compose.yml run --rm --no-deps --entrypoint \
//     "dotnet" api WorkshopOS.Api.dll --reset-owner-password 'NewPasswordHere'
// Or: ./scripts/reset-owner-password.sh 'NewPasswordHere'
if (args is ["--reset-owner-password", var emergencyPassword])
{
    await ResetOwnerPasswordAsync(builder, emergencyPassword);
    return;
}

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
        var isDev = context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment();
        await context.Response.WriteAsJsonAsync(new
        {
            title = "An unexpected error occurred.",
            status = 500,
            detail = isDev ? ex?.ToString() : null
        });
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

    // Optional env-based emergency reset (set WORKSHOPOS_OWNER_PASSWORD_RESET then restart api once).
    var envReset = Environment.GetEnvironmentVariable("WORKSHOPOS_OWNER_PASSWORD_RESET");
    if (!string.IsNullOrWhiteSpace(envReset))
    {
        await ApplyOwnerPasswordResetAsync(db, scope.ServiceProvider.GetRequiredService<TokenService>(), envReset);
        Console.WriteLine("WORKSHOPOS_OWNER_PASSWORD_RESET applied for owner. Unset the env var and restart.");
    }

    var seedDemo = app.Configuration.GetValue("Seed:Demo", false);
    if (seedDemo && !await DbSeed.GetSettingAsync(db, SettingKeys.SetupCompleted, false))
    {
        // Dev-only convenience: do not auto-complete setup. Staff must run setup or explicit seed script.
    }
}

app.Run();

static async Task ResetOwnerPasswordAsync(WebApplicationBuilder builder, string newPassword)
{
    var problem = PasswordRules.Validate(newPassword);
    if (problem is not null)
    {
        Console.Error.WriteLine(problem);
        Environment.ExitCode = 1;
        return;
    }

    builder.Services.AddWorkshopInfrastructure(builder.Configuration);
    await using var host = builder.Build();
    using var scope = host.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<WorkshopDbContext>();
    await db.Database.MigrateAsync();
    var tokens = scope.ServiceProvider.GetRequiredService<TokenService>();
    await ApplyOwnerPasswordResetAsync(db, tokens, newPassword);
    Console.WriteLine("Owner password reset OK. Sign in with the new password.");
}

static async Task ApplyOwnerPasswordResetAsync(WorkshopDbContext db, TokenService tokens, string newPassword)
{
    var problem = PasswordRules.Validate(newPassword);
    if (problem is not null) throw new ValidationAppException(problem);

    var owner = await db.Users.FirstOrDefaultAsync(u => u.IsOwner && u.ArchivedAt == null)
        ?? throw new ValidationAppException("No owner account found. Complete first-run setup first.");

    owner.PasswordHash = tokens.HashPassword(owner, newPassword);
    owner.MustResetPassword = false;
    owner.UpdatedAt = DateTimeOffset.UtcNow;
    owner.Status = WorkshopOS.Domain.Enums.UserStatus.Active;

    var active = await db.RefreshTokens.Where(t => t.UserId == owner.Id && t.RevokedAt == null).ToListAsync();
    foreach (var t in active) t.RevokedAt = DateTimeOffset.UtcNow;

    await db.SaveChangesAsync();
    Console.WriteLine($"Reset password for owner {owner.Email}");
}

public partial class Program { }
