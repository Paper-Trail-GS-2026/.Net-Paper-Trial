using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using PaperTrail.API.Configuration.CatalogEvents;
using PaperTrail.Domain.Exceptions;
using PaperTrail.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PaperTrail.API.BackgroundServices;

public sealed class CatalogEventConsumer(IServiceScopeFactory scopes, IOptions<CatalogEventOptions> options,
    ILogger<CatalogEventConsumer> logger) : BackgroundService, IHealthCheck
{
    private volatile bool _connected;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var factory = new ConnectionFactory
        {
            Uri = new Uri(settings.Uri),
            AutomaticRecoveryEnabled = false,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(10),
            RequestedHeartbeat = TimeSpan.FromSeconds(10),
            ConsumerDispatchConcurrency = 1,
            MaxInboundMessageBodySize = 65536
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await factory.CreateConnectionAsync("papertrail-dotnet-catalog", stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await channel.ExchangeDeclareAsync(settings.Exchange, ExchangeType.Direct, durable: true,
                    autoDelete: false, cancellationToken: stoppingToken);
                await channel.QueueDeclareAsync(settings.Queue, durable: true, exclusive: false,
                    autoDelete: false, cancellationToken: stoppingToken);
                await channel.QueueBindAsync(settings.Queue, settings.Exchange, settings.RoutingKey,
                    cancellationToken: stoppingToken);
                await channel.BasicQosAsync(0, 1, false, stoppingToken);

                var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.UnregisteredAsync += (_, _) =>
                {
                    _connected = false;
                    disconnected.TrySetResult();
                    return Task.CompletedTask;
                };
                channel.CallbackExceptionAsync += (_, args) =>
                {
                    logger.LogError(args.Exception, "Catalog consumer callback failed.");
                    disconnected.TrySetResult();
                    return Task.CompletedTask;
                };
                consumer.ReceivedAsync += (_, message) => ReceiveAsync(channel, message, stoppingToken);
                await channel.BasicConsumeAsync(settings.Queue, autoAck: false, consumer, stoppingToken);
                _connected = true;
                logger.LogInformation("Consuming catalog events from {Queue}.", settings.Queue);
                await disconnected.Task.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Catalog consumer disconnected; retrying connection.");
            }
            finally
            {
                _connected = false;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(settings.RetryDelaySeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ReceiveAsync(IChannel channel, BasicDeliverEventArgs message, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<CatalogEventHandler>()
                .HandleAsync(message.Body, cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or DomainException)
        {
            logger.LogWarning("Invalid catalog event rejected: {Reason}", exception.Message);
            await channel.BasicRejectAsync(message.DeliveryTag, requeue: false, cancellationToken);
            return;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Catalog event could not be saved; returning it to the queue.");
            await Task.Delay(TimeSpan.FromSeconds(options.Value.RetryDelaySeconds), cancellationToken);
            await channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: true, cancellationToken);
            return;
        }

        await channel.BasicAckAsync(message.DeliveryTag, multiple: false, cancellationToken);
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default) => Task.FromResult(_connected
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Degraded());
}
