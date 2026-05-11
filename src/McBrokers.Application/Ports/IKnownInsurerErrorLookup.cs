using McBrokers.Domain.Quotations;

namespace McBrokers.Application.Ports;

/// <summary>
/// Busca el mensaje humano administrable para un error de aseguradora. La intención es
/// que el vendedor vea siempre un mensaje claro en es-MX en lugar del código crudo.
/// </summary>
public interface IKnownInsurerErrorLookup
{
    /// <summary>
    /// Devuelve el error conocido (si existe) o null si no hay mapeo para ese código.
    /// El llamador hace fallback al mensaje crudo del adapter.
    /// </summary>
    Task<KnownInsurerErrorView?> FindAsync(
        Guid insurerId, string externalCode, CancellationToken cancellationToken);
}

public sealed record KnownInsurerErrorView(
    string ExternalCode,
    ErrorCategory Category,
    string HumanMessage,
    string? SuggestedAction,
    AutoRetryStrategy AutoRetry);
