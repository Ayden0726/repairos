using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using WorkshopOS.Application.Abstractions;
using WorkshopOS.Infrastructure.Persistence;
using WorkshopOS.Infrastructure.Security;
using WorkshopOS.Infrastructure.Services;

namespace WorkshopOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkshopInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.AddDbContext<WorkshopDbContext>(opt =>
            opt.UseNpgsql(config.GetConnectionString("Default")));

        services.AddScoped<TokenService>();
        services.AddScoped<IClock, SystemClock>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<ISearchService, SearchService>();

        var jwt = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            foreach (var key in Domain.Security.PermissionKeys.AllKeys)
            {
                options.AddPolicy($"perm:{key}", policy =>
                    policy.RequireAssertion(ctx =>
                        ctx.User.HasClaim("is_owner", "true") ||
                        ctx.User.HasClaim("permission", key)));
            }
        });

        return services;
    }
}
