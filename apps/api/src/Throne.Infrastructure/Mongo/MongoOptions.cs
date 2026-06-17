namespace Throne.Infrastructure.Mongo;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    /// <summary>
    /// Mongo connection string. Должен указывать на replica set: write-tools используют
    /// multi-document transactions (см. ADR-0002 §6). Локально:
    /// <c>mongodb://localhost:27017/?replicaSet=rs0&amp;compressors=zstd</c>.
    /// <para>
    /// <c>directConnection=true</c> намеренно НЕ в дефолте: на single-node RS он отключает
    /// failover-логику SDAM и при первом heartbeat-сбое весь кластер сваливается в
    /// <c>Disconnected</c> — выходящих воркеров кладёт на 30 с (см. инцидент
    /// 2026-06-17). Для admin/maintenance-сценариев параметр можно дописать в env.
    /// </para>
    /// <para>
    /// <c>compressors=zstd</c> в дефолте: на любом не-локальном соединении сжатие
    /// wire-protocol заметно (порядок ×3-5) сокращает body тяжёлых list-эндпоинтов,
    /// на 127.0.0.1 — no-op. Mongo 7+ поддерживает zstd «из коробки»; если сервер
    /// старее и не понимает — driver сам спадёт в noop без ошибки.
    /// </para>
    /// </summary>
    public string ConnectionString { get; set; } = "mongodb://localhost:27017/?replicaSet=rs0&compressors=zstd";

    public string Database { get; set; } = "throne";
}
