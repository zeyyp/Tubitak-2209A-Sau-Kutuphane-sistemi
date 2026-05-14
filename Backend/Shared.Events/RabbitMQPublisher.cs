using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Shared.Events;

public class RabbitMQPublisher : IDisposable
{
    private IConnection? _connection;
    private IModel? _channel;
    private readonly string _exchangeName;
    private readonly ConnectionFactory _factory;
    private bool _available = false;

    public RabbitMQPublisher(string hostName, string userName, string password, string exchangeName = "library_events")
    {
        _exchangeName = exchangeName;
        _factory = new ConnectionFactory
        {
            HostName = hostName,
            UserName = userName,
            Password = password,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
        };

        TryConnect();
    }

    private void TryConnect()
    {
        try
        {
            _connection = _factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.ExchangeDeclare(
                exchange: _exchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false
            );

            _available = true;
        }
        catch (Exception)
        {
            // RabbitMQ mevcut değil — servis yine de çalışır, event'ler atlanır
            _available = false;
        }
    }

    public void Publish<T>(T message, string routingKey) where T : class
    {
        if (!_available || _channel == null || !_channel.IsOpen)
        {
            // RabbitMQ bağlantısı yok, sessizce geç
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            _channel.BasicPublish(
                exchange: _exchangeName,
                routingKey: routingKey,
                basicProperties: properties,
                body: body
            );
        }
        catch (Exception)
        {
            _available = false;
        }
    }

    public void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
    }
}
