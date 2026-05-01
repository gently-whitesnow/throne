namespace Throne.Application.Ports;

public interface IUnitOfWork
{
    Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct);

    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct);
}
