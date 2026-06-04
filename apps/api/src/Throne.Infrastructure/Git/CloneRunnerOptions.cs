namespace Throne.Infrastructure.Git;

/// <summary>
/// Bind target for the <c>Throne:Clone</c> configuration section
/// (ADR-0026 §5). Default — 4 параллельных <c>gh repo clone</c> процессов.
/// Лишние задачи в очереди ждут освобождения слота на <see cref="SemaphoreSlim"/>
/// внутри <see cref="RepositoryCloneService"/>.
/// </summary>
public sealed class CloneRunnerOptions
{
    public const string SectionName = "Throne:Clone";

    /// <summary>
    /// Максимум одновременно идущих clone-задач. Значения &lt;= 0 нормализуются в 1
    /// (последовательная обработка) — это путь для тестов и для машин, где сетевой
    /// канал/диск не вытягивают четыре параллельных <c>gh repo clone</c>.
    /// </summary>
    public int MaxParallel { get; set; } = 4;
}
