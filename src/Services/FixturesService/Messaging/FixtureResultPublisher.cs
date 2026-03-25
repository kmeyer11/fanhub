using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace FixturesService.Messaging;

public class FixtureResultMessage
{
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public string Competition { get; set; } = string.Empty;
}

public class FixtureResultPublisher : IDisposable
{
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;
    private IChannel? _channel;
    private const string QueueName = "fixture.results";

    public FixtureResultPublisher(IConfiguration config)
    {
        _factory = new ConnectionFactory
        {
            HostName = config["RabbitMQ:Host"] ?? "rabbitmq",
            UserName = config["RabbitMQ:Username"] ?? "guest",
            Password = config["RabbitMQ:Password"] ?? "guest"
        };
    }

    private async Task EnsureConnectedAsync()
    {
        if (_channel is { IsOpen: true }) return;
        _connection = await _factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();
        await _channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false);
    }

    public async Task PublishAsync(FixtureResultMessage message)
    {
        await EnsureConnectedAsync();
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        await _channel!.BasicPublishAsync(exchange: "", routingKey: QueueName, body: body);
    }

    public void Dispose()
    {
        _channel?.CloseAsync();
        _connection?.CloseAsync();
    }
}