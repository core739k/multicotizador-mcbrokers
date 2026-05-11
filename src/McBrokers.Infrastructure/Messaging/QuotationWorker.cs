using McBrokers.Application.Ports;
using McBrokers.Application.Quotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace McBrokers.Infrastructure.Messaging;

/// <summary>
/// IHostedService que consume la cola y ejecuta ProcessQuotation por cada item.
/// </summary>
public sealed class QuotationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IQuotationQueue _queue;
    private readonly ILogger<QuotationWorker> _logger;

    public QuotationWorker(
        IServiceScopeFactory scopeFactory,
        IQuotationQueue queue,
        ILogger<QuotationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("QuotationWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            QuotationWorkItem item;
            try
            {
                item = await _queue.DequeueAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<ProcessQuotation>();

            try
            {
                await processor.ExecuteAsync(item.QuotationId, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "QuotationWorker failed processing quotation {QuotationId} (correlation {CorrelationId})",
                    item.QuotationId, item.CorrelationId);
            }
        }

        _logger.LogInformation("QuotationWorker stopped");
    }
}
