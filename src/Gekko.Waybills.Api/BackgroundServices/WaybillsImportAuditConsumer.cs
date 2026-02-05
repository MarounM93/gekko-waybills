using System.Text.Json;
using Gekko.Waybills.Application.Abstractions;
using Gekko.Waybills.Application.Events;
using Gekko.Waybills.Domain;
using Gekko.Waybills.Infrastructure;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Gekko.Waybills.Api.BackgroundServices;

public sealed class WaybillsImportAuditConsumer : BackgroundService
{
    private const string ExchangeName = "waybills";
    private const string RoutingKey = "waybills.imported";
    private const string QueueName = "waybills.imported.audit";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WaybillsImportAuditConsumer> _logger;
    private readonly RabbitMqOptions _options;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public WaybillsImportAuditConsumer(
        IServiceScopeFactory scopeFactory,
        ILogger<WaybillsImportAuditConsumer> logger,
        IOptions<RabbitMqOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);
        await _channel.QueueDeclareAsync(queue: QueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(QueueName, ExchangeName, RoutingKey, arguments: null, cancellationToken: stoppingToken);
        await _channel.BasicQosAsync(0, 10, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                var payload = JsonSerializer.Deserialize<WaybillsImportedEvent>(args.Body.Span, _serializerOptions);
                if (payload is null)
                {
                    await _channel.BasicAckAsync(args.DeliveryTag, false, stoppingToken);
                    return;
                }

                _logger.LogInformation(
                    "RabbitMQ consume start Tenant={TenantId} JobId={JobId}",
                    payload.TenantId,
                    payload.ImportJobId);

                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

                dbContext.ImportAudits.Add(new ImportAudit
                {
                    Id = Guid.NewGuid(),
                    TenantId = payload.TenantId,
                    ImportJobId = payload.ImportJobId,
                    TotalRows = payload.TotalRows,
                    InsertedCount = payload.InsertedCount,
                    UpdatedCount = payload.UpdatedCount,
                    RejectedCount = payload.RejectedCount,
                    ReceivedAtUtc = DateTime.UtcNow
                });

                await dbContext.SaveChangesAsync(stoppingToken);
                await _channel.BasicAckAsync(args.DeliveryTag, false, stoppingToken);
                _logger.LogInformation(
                    "RabbitMQ consume completed Tenant={TenantId} JobId={JobId}",
                    payload.TenantId,
                    payload.ImportJobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process waybills import audit message.");
                await _channel.BasicNackAsync(args.DeliveryTag, false, requeue: true, cancellationToken: stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
