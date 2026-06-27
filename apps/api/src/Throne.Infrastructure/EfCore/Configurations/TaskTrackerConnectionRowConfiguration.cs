using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class TaskTrackerConnectionRowConfiguration
    : IEntityTypeConfiguration<TaskTrackerConnectionRow>
{
    public void Configure(EntityTypeBuilder<TaskTrackerConnectionRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.TaskTrackers.Connections);
        builder.HasKey(x => x.Tracker);

        builder.Property(x => x.Tracker).HasColumnName("tracker");
        builder.Property(x => x.BaseUrl).HasColumnName("base_url").IsRequired();
        builder.Property(x => x.Token).HasColumnName("token").IsRequired();

        builder.Property(x => x.SelectedBoards)
            .HasColumnName("selected_boards")
            .HasConversion(
                v => JsonSerializer.Serialize(v, EfJson.Options),
                v => string.IsNullOrEmpty(v)
                    ? new List<TaskTrackerBoardSelectionRow>()
                    : JsonSerializer.Deserialize<List<TaskTrackerBoardSelectionRow>>(v, EfJson.Options)
                        ?? new List<TaskTrackerBoardSelectionRow>(),
                new ValueComparer<List<TaskTrackerBoardSelectionRow>>(
                    (l, r) => JsonSerializer.Serialize(l, EfJson.Options)
                        == JsonSerializer.Serialize(r, EfJson.Options),
                    v => JsonSerializer.Serialize(v, EfJson.Options).GetHashCode(StringComparison.Ordinal),
                    v => JsonSerializer.Deserialize<List<TaskTrackerBoardSelectionRow>>(
                        JsonSerializer.Serialize(v, EfJson.Options), EfJson.Options)
                        ?? new List<TaskTrackerBoardSelectionRow>()))
            .IsRequired();
    }
}
