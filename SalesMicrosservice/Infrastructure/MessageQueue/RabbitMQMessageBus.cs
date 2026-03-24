using System.Text;
using System.Text.Json;
using Domain.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Infrastructure.MessageQueue;

public class RabbitMQMessageBus : IMessageBus, IDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;

    public RabbitMQMessageBus(string connectionString)
    {
        var factory = new ConnectionFactory() { Uri = new Uri(connectionString) };
        _connection = factory.CreateConnectionAsync().Result;   
        _channel = _connection.CreateChannelAsync().Result;
    }

    public async Task PublishAsync<T>(string queue, T message)
    {
        await _channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false);
        
        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        await _channel.BasicPublishAsync(exchange: "", routingKey: queue, body: body);
    }

    public async void Consume<T>(string queue, Func<T, Task> handler)
    {
        await _channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var obj = JsonSerializer.Deserialize<T>(message);
            await handler(obj);
        };

        await _channel.BasicConsumeAsync(queue, autoAck: true, consumer);
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        GC.SuppressFinalize(this);
    }
}
