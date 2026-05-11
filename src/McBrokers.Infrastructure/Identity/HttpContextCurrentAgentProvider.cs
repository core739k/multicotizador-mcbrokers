using System.Security.Claims;
using McBrokers.Application.Ports;
using Microsoft.AspNetCore.Http;

namespace McBrokers.Infrastructure.Identity;

public sealed class HttpContextCurrentAgentProvider : ICurrentAgentProvider
{
    public const string AgentIdClaim = "mcb:agent-id";

    private readonly IHttpContextAccessor _httpContext;

    public HttpContextCurrentAgentProvider(IHttpContextAccessor httpContext)
    {
        _httpContext = httpContext;
    }

    public Guid AgentId
    {
        get
        {
            var user = _httpContext.HttpContext?.User
                ?? throw new InvalidOperationException("No HttpContext: AgentId cannot be resolved.");

            var raw = user.FindFirstValue(AgentIdClaim);
            if (string.IsNullOrWhiteSpace(raw) || !Guid.TryParse(raw, out var id))
            {
                throw new InvalidOperationException(
                    $"Authenticated user has no '{AgentIdClaim}' claim. Did the auth pipeline run?");
            }

            return id;
        }
    }
}
