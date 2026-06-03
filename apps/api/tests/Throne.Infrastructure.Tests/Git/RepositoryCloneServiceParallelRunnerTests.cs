using FluentAssertions;

namespace Throne.Infrastructure.Tests.Git;

/// <summary>
/// Параллельный runner внутри <c>RepositoryCloneService</c> — ADR-0026 §5.
/// Harness и in-memory binding-store вынесены в отдельные файлы; здесь только
/// сценарии лимита параллелизма.
/// </summary>
public class RepositoryCloneServiceParallelRunnerTests
{
    [Fact(DisplayName = "Параллельный runner соблюдает MaxParallel=2 для четырёх pending-binding'ов")]
    public async Task Runner_respects_max_parallel_limit()
    {
        await using var harness = await CloneRunnerHarnessStarter.StartAsync(maxParallel: 2, pendingCount: 4);

        // 4 binding'а в очереди + лимит 2 → должно подняться ровно два одновременно.
        await harness.WaitForInFlightAsync(2);
        harness.MaxObservedConcurrency.Should().Be(2);

        // Освобождаем по одному и убеждаемся, что освобождённый слот тут же берёт следующий.
        await harness.ReleaseOneAsync();
        await harness.WaitForCompletedAsync(1);
        await harness.WaitForInFlightAsync(2);
        await harness.ReleaseOneAsync();
        await harness.WaitForCompletedAsync(2);
        await harness.WaitForInFlightAsync(2);
        harness.ReleaseRemaining();
        await harness.WaitForAllCompletedAsync();

        harness.MaxObservedConcurrency.Should().Be(2);
    }

    [Fact(DisplayName = "MaxParallel=1 даёт последовательную обработку")]
    public async Task Runner_serialises_when_max_parallel_is_one()
    {
        await using var harness = await CloneRunnerHarnessStarter.StartAsync(maxParallel: 1, pendingCount: 3);

        // Каждый раз ждём, когда runner поднимет следующий клон, отпускаем его,
        // повторяем три раза. Это и проверяет лимит 1, и гарантирует drain очереди.
        for (var i = 0; i < 3; i++)
        {
            await harness.WaitForInFlightAsync(1);
            harness.MaxObservedConcurrency.Should().Be(1);
            await harness.ReleaseOneAsync();
        }
        await harness.WaitForAllCompletedAsync();

        harness.MaxObservedConcurrency.Should().Be(1);
    }
}
