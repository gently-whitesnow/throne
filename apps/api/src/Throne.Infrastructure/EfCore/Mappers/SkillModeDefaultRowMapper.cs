using Throne.Application.Terminals;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Mappers;

internal static class SkillModeDefaultRowMapper
{
    public static SkillModeDefaultRow ToRow(SkillModeDefault item) => new()
    {
        Id = IdOf(item.Mode, item.SkillId),
        Mode = item.Mode,
        SkillId = item.SkillId,
        Enabled = item.Enabled,
    };

    public static SkillModeDefault ToDomain(SkillModeDefaultRow row) =>
        new(row.Mode, row.SkillId, row.Enabled);

    // Matches MongoSkillModeDefaultStore.IdOf so a cross-backend export stays byte-stable.
    public static string IdOf(string mode, string skillId) => $"{mode}:{skillId}";
}
