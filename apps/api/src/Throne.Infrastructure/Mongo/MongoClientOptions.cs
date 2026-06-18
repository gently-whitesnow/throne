namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Стартовые таймауты и pool-параметры Mongo-клиента. Накладываются поверх
/// <c>MongoClientSettings.FromConnectionString</c>: всё, что не указано в URI,
/// берётся отсюда; кто хочет — может переопределить и URI, и эти поля через env.
/// Дефолты подобраны под профиль фоновых воркеров (fail-fast + backoff), а не
/// под HTTP-handler critical-path.
/// </summary>
public sealed class MongoClientOptions
{
    public const string SectionName = "Mongo:Client";

    public string ApplicationName { get; set; } = "throne-api";

    public int ServerSelectionTimeoutSeconds { get; set; } = 5;

    public int ConnectTimeoutSeconds { get; set; } = 10;

    public int SocketTimeoutSeconds { get; set; } = 60;

    public int HeartbeatFrequencySeconds { get; set; } = 10;

    public int MaxConnectionPoolSize { get; set; } = 200;

    public int MinConnectionPoolSize { get; set; } = 10;

    public int MaxConnectionIdleMinutes { get; set; } = 10;

    public int MaxConnectionLifeMinutes { get; set; } = 30;

    public int WaitQueueTimeoutSeconds { get; set; } = 15;
}
