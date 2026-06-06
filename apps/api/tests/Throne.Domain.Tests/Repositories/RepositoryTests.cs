using FluentAssertions;
using Throne.Domain.Repositories;

namespace Throne.Domain.Tests.Repositories;

public class RepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Create заполняет координату и ставит created_at == updated_at")]
    public void Create_sets_coordinate_and_timestamps()
    {
        var coordinate = new RepoCoordinate(GitProviderNames.GitHub, "octo", "throne");

        var repository = Repository.Create(RepositoryId.New(), coordinate, Now);

        repository.Coordinate.Should().Be(coordinate);
        repository.CreatedAt.Should().Be(Now);
        repository.UpdatedAt.Should().Be(Now);
    }

    [Fact(DisplayName = "Create отвергает null-координату")]
    public void Create_rejects_null_coordinate()
    {
        var act = () => Repository.Create(RepositoryId.New(), null!, Now);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Restore восстанавливает поля как есть")]
    public void Restore_rehydrates_fields()
    {
        var id = RepositoryId.New();
        var coordinate = new RepoCoordinate(GitProviderNames.GitHub, "octo", "throne");
        var created = Now;
        var updated = Now.AddMinutes(5);

        var repository = Repository.Restore(id, coordinate, created, updated);

        repository.Id.Should().Be(id);
        repository.Coordinate.Should().Be(coordinate);
        repository.CreatedAt.Should().Be(created);
        repository.UpdatedAt.Should().Be(updated);
    }
}
