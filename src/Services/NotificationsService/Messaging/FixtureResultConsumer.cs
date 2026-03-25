using System.Text;
using System.Text.Json;
using NotificationsService.Models;
using NotificationsService.Repositories;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationsService.Messaging;

public class FixtureResultConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FixtureResultConsumer> _logger;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private IConnection? _connection;
    private IChannel? _channel;
    private const string QueueName = "fixture.results";

    public FixtureResultConsumer(
        IServiceScopeFactory scopeFactory,
        ILogger<FixtureResultConsumer> logger,
        IConfiguration config,
        IHttpClientFactory httpClientFactory)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    private async Task<IEnumerable<int>> GetUserIdsByTeamAsync(string teamName)
    {
        var userServiceUrl = _config["Services:UserService"];
        var client = _httpClientFactory.CreateClient();
        try
        {
            var response = await client.GetAsync($"{userServiceUrl}/users/by-team/{Uri.EscapeDataString(teamName)}");
            if (!response.IsSuccessStatusCode) return [];
            return await response.Content.ReadFromJsonAsync<IEnumerable<int>>() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to fetch user IDs for team {Team}: {Message}", teamName, ex.Message);
            return [];
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retries = 10;
        while (retries-- > 0)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _config["RabbitMQ:Host"] ?? "rabbitmq",
                    UserName = _config["RabbitMQ:Username"] ?? "guest",
                    Password = _config["RabbitMQ:Password"] ?? "guest"
                };

                _connection = await factory.CreateConnectionAsync(stoppingToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await _channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("RabbitMQ not ready, retrying... ({Message})", ex.Message);
                await Task.Delay(3000, stoppingToken);
            }
        }

        if (_channel is null)
        {
            _logger.LogError("Could not connect to RabbitMQ after retries.");
            return;
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                var message = JsonSerializer.Deserialize<FixtureResultMessage>(body);
                if (message is null) return;

                var homeUserIds = await GetUserIdsByTeamAsync(message.HomeTeam);
                var awayUserIds = await GetUserIdsByTeamAsync(message.AwayTeam);
                var allUserIds = homeUserIds.Union(awayUserIds);

                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

                var notifMessage = $"{message.HomeTeam} {message.HomeScore} - {message.AwayScore} {message.AwayTeam}";
                foreach (var userId in allUserIds)
                {
                    var notification = new Notification
                    {
                        UserId = userId.ToString(),
                        Title = "Match Result",
                        Message = notifMessage,
                        Type = "fixture_result",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    await repository.AddAsync(notification);
                }

                await _channel.BasicAckAsync(ea.DeliveryTag, false);
                _logger.LogInformation("Processed fixture result: {Message} for {Count} users", notifMessage, allUserIds.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing fixture result message.");
            }
        };

        await _channel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null) await _channel.CloseAsync();
        if (_connection is not null) await _connection.CloseAsync();
        await base.StopAsync(cancellationToken);
    }
}

public class FixtureResultMessage
{
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public string Competition { get; set; } = string.Empty;
}
