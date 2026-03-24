namespace Domain.Contracts;

public interface IMessageBus
{
    Task PublishAsync<T>(string queue, T message);
    void Consume<T>(string queue, Func<T, Task> handler);
}
