using System.Security.Claims;
using McBrokers.Application.Auth;
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

                options.AdditionalAuthorizationParameters["hd"] = AllowedHostedDomain;

                options.Events.OnCreatingTicket = async context =>
                {
                    var email = context.Identity?.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
                    var fullName = context.Identity?.FindFirst(ClaimTypes.Name)?.Value ?? email;

                    var resolve = context.HttpContext.RequestServices.GetRequiredService<ResolveAgentFromGoogleToken>();
                    var result = await resolve.ResolveAsync(
                        new GoogleIdentity(email, fullName),
                        context.HttpContext.RequestAborted).ConfigureAwait(false);

                    if (!result.IsSuccess)
                    {
                        context.Fail(new UnauthorizedAccessException($"Sign-in rejected: {result.Error}"));
                        return;
                    }

                    var agent = result.Value;
                    context.Identity!.AddClaim(new Claim(HttpContextCurrentAgentProvider.AgentIdClaim, agent.Id.ToString()));
                    context.Identity.AddClaim(new Claim(ClaimTypes.Role, agent.Role.ToString()));
                    context.Identity.AddClaim(new Claim("mcb:full-name", agent.FullName));
                    if (!string.IsNullOrWhiteSpace(agent.AgentCode))
                    {
                        context.Identity.AddClaim(new Claim("mcb:agent-code", agent.AgentCode));
                    }
                    if (agent.IsTechnical)
                    {
                        context.Identity.AddClaim(new Claim("mcb:is-technical", "true"));
                    }
                };
            });

        return services;
    }
}
