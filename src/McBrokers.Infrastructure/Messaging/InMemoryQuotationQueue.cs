using System.Threading.Channels;
using McBrokers.Application.Ports;

namespace McBrokers.Infrastructure.Messaging;

/// <summary>
/// Cola en memoria basada en Channel. Apta para single-instance del Api en Fase 3.
/// Cuando haya múltiples réplicas o se necesite persistencia, migrar a Azure Service Bus
/// o a una cola en BD con SKIP LOCKED.
/// </summary>
public sealed class InMemoryQuotationQueue : IQuotationQueue
{
    private readonly Channel<QuotationWorkItem> _channel = Channel.CreateUnbounded<QuotationWorkItem>(
        new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
        });

    public ValueTask EnqueueAsync(QuotationWorkItem item, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(item, cancellationToken);

    public ValueTask<QuotationWorkItem> DequeueAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
