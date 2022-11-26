using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace InverterHistorian;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _config;
    private readonly Dictionary<InverterPoints, InverterPointStatus> _points;

    public Worker(ILogger<Worker> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
        _points = new Dictionary<InverterHistorian.InverterPoints, InverterPointStatus>
        {
            {InverterPoints.Mode, new InverterPointStatus(new InverterPointConfig {
                Type = InverterPointType.String
            })},
            {InverterPoints.LoadWatt, new InverterPointStatus(new InverterPointConfig {
                Type = InverterPointType.Analog,
                Deadband = 5,
            })},
            {InverterPoints.AcChargeOn, new InverterPointStatus(new InverterPointConfig {
                Type = InverterPointType.Digital
            })},
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory();
        factory.HostName = _config.GetValue<string>("MQHost");
        factory.UserName = _config.GetValue<string>("MQUser");
        factory.Password = _config.GetValue<string>("MQPassword");
        factory.VirtualHost = "/";

        using var mq = factory.CreateConnection();
        using var channel = mq.CreateModel();

        channel.ExchangeDeclare("inverter", ExchangeType.Topic, false, false);

        var queue = channel.QueueDeclare().QueueName;
        channel.QueueBind(queue, "inverter", "status");

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var routingKey = ea.RoutingKey;
            _logger.LogDebug($" [x] Received '{routingKey}':'{message}'");

            var status = JsonSerializer.Deserialize<InverterMon.InverterStatus>(message);
            if (status != null)
            {
                foreach (var kvp in _points)
                {
                    switch (kvp.Key)
                    {
                        case InverterPoints.Mode:
                            await StringPointHistory(status.Mode, kvp.Key, kvp.Value);
                            break;
                        case InverterPoints.LoadWatt:
                            await AnalogPointHistory(status.LoadWatt, kvp.Key, kvp.Value);
                            break;
                        case InverterPoints.AcChargeOn:
                            await DigitalPointHistory(status.AcChargeOn, kvp.Key, kvp.Value);
                            break;
                    }
                }
            }
        };
        channel.BasicConsume(queue, true, consumer);

        while (!stoppingToken.IsCancellationRequested)
        {
            // _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(1000, stoppingToken);
        }

        channel.Close();
        mq.Close();
    }

    private Task StringPointHistory(string currentValue, InverterPoints point, InverterPointStatus status)
    {
        var lastValue = status.LastValue;
        if (currentValue.ToLower() != lastValue?.ToLower())
        {
            status.UpdateLastValue(currentValue);
            _logger.LogInformation($"{status.LastChanged}: {point} {lastValue} => {currentValue}");
        }

        return Task.CompletedTask;
    }

    private Task AnalogPointHistory(double currentValue, InverterPoints point, InverterPointStatus status)
    {
        var lastValue = !string.IsNullOrEmpty(status.LastValue) ? Double.Parse(status.LastValue) : 0.0;

        if (Math.Abs(currentValue - lastValue) >= status.Config.Deadband)
        {
            status.UpdateLastValue(currentValue.ToString());
            _logger.LogInformation($"{status.LastChanged}: {point} {lastValue} => {currentValue}");
        }

        return Task.CompletedTask;
    }

    private Task DigitalPointHistory(bool currentValue, InverterPoints point, InverterPointStatus status)
    {
        var lastValue = !string.IsNullOrEmpty(status.LastValue) ? Boolean.Parse(status.LastValue) : false;

        if (currentValue != lastValue)
        {
            status.UpdateLastValue(currentValue.ToString());
            _logger.LogInformation($"{status.LastChanged}: {point} {lastValue} => {currentValue}");
        }

        return Task.CompletedTask;
    }
}
