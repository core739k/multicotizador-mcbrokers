using System.Security.Claims;
using McBrokers.Domain.Agents;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace McBrokers.Infrastructure.Identity;

public static class McBrokersAuthenticationExtensions
{
    private const string AllowedHostedDomain = "mcbrokers.com.mx";

    public static IServiceCollection AddMcBrokersGoogleAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var clientId = configuration["Authentication:Google:ClientId"]
            ?? throw new InvalidOperationException(
                "Missing configuration: Authentication:Google:ClientId (use Key Vault, not appsettings).");
        var clientSecret = configuration["Authentication:Google:ClientSecret"]
            ?? throw new InvalidOperationException(
                "Missing configuration: Authentication:Google:ClientSecret (use Key Vault, not appsettings).");

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Name = "mcbrokers.auth";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.LoginPath = "/signin";
                options.AccessDeniedPath = "/access-denied";
            })
            .AddGoogle(options =>
            {
                options.ClientId = clientId;
                options.ClientSecret = clientSecret;
                options.Scope.Add("email");
                options.Scope.Add("profile");
                options.SaveTokens = false;

                // Sugerencia a Google: pre-filtrar al dominio. NO confiable por sí solo.
                options.AdditionalAuthorizationParameters["hd"] = AllowedHostedDomain;

                // Defensa en profundidad: validar el claim email del proveedor
                // antes de emitir el ticket. Bloquea cualquier identidad fuera del dominio.
                options.Events.OnCreatingTicket = context =>
                {
                    var email = context.Identity?.FindFirst(ClaimTypes.Email)?.Value;
                    var emailResult = AgentEmail.Create(email ?? string.Empty);

                    if (!emailResult.IsSuccess)
                    {
                        context.Fail(new UnauthorizedAccessException(
                            $"Sign-in rejected: {emailResult.Error}"));
                    }

                    return Task.CompletedTask;
                };
            });

        return services;
    }
}
