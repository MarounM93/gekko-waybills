using System.Text.Json;
using Gekko.Waybills.Application.Events;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Gekko.Waybills.Infrastructure.Messaging;

public sealed class RabbitMqEventPublisher : IEventPublisher, IDisposable
{
    private const string ExchangeName = "waybills";
    private const string RoutingKey = "waybills.imported";
    private readonly ConnectionFactory _factory;
    private readonly Lazy<Task<(IConnection Connection, IChannel Channel)>> _connectionAndChannel;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<RabbitMqEventPublisher> _logger;

    public RabbitMqEventPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqEventPublisher> logger)
    {
        var settings = options.Value;
        _factory = new ConnectionFactory
        {
            HostName = settings.HostName,
            Port = settings.Port,
            UserName = settings.UserName,
            Password = settings.Password,
            VirtualHost = settings.VirtualHost
        };
        _connectionAndChannel = new Lazy<Task<(IConnection, IChannel)>>(InitializeAsync);
        _logger = logger;
    }

    public async Task PublishWaybillsImportedAsync(WaybillsImportedEvent payload, CancellationToken cancellationToken)
    {
        try
        {
            var (connection, channel) = await _connectionAndChannel.Value.ConfigureAwait(false);
            var body = JsonSerializer.SerializeToUtf8Bytes(payload, _serializerOptions);
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent
            };

            await channel.BasicPublishAsync(
                exchange: ExchangeName,
                routingKey: RoutingKey,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
            _logger.LogInformation(
                "RabbitMQ published event Exchange={Exchange} RoutingKey={RoutingKey} Tenant={TenantId} JobId={JobId}",
                ExchangeName,
                RoutingKey,
                payload.TenantId,
                payload.ImportJobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "RabbitMQ publish failed Exchange={Exchange} RoutingKey={RoutingKey} Tenant={TenantId} JobId={JobId}",
                ExchangeName,
                RoutingKey,
                payload.TenantId,
                payload.ImportJobId);
            throw;
        }
    }

    public void Dispose()
    {
        if (!_connectionAndChannel.IsValueCreated)
        {
            return;
        }

        var initialized = _connectionAndChannel.Value.GetAwaiter().GetResult();
        initialized.Channel.Dispose();
        initialized.Connection.Dispose();
    }

    private async Task<(IConnection Connection, IChannel Channel)> InitializeAsync()
    {
        var connection = await _factory.CreateConnectionAsync().ConfigureAwait(false);
        var channel = await connection.CreateChannelAsync().ConfigureAwait(false);
        await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true)
            .ConfigureAwait(false);
        return (connection, channel);
    }
}
