import { AlertCircle, PackageCheck } from "lucide-react";

import {
  SKILL_MODE_LABELS,
  useSetSkillModeDefaults,
  useSkillModeDefaultsQuery,
  type SkillModeDefaultEntry,
  type SkillModeDefaults
} from "@/entities/session-skill";

export function SkillModeDefaultsCard() {
  const query = useSkillModeDefaultsQuery();
  const mutation = useSetSkillModeDefaults();

  if (query.error !== null) {
    return (
      <div
        role="alert"
        className="flex items-start gap-2 rounded-lg border border-error/30 bg-error/10 px-4 py-3 text-sm text-error"
      >
        <AlertCircle aria-hidden size={16} strokeWidth={2} className="mt-0.5" />
        <span>Не удалось загрузить матрицу скилов.</span>
      </div>
    );
  }

  const data = query.data;
  return (
    <section
      data-testid="skill-mode-defaults-card"
      className="flex flex-col gap-3 rounded-lg border border-base-300 bg-base-100 p-4"
    >
      <header className="flex min-w-0 items-start gap-3">
        <span
          aria-hidden
          className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary"
        >
          <PackageCheck size={18} strokeWidth={2} />
        </span>
        <div className="flex min-w-0 flex-col gap-1">
          <h3 className="m-0 text-base font-semibold leading-tight">
            Скилы по умолчанию
          </h3>
          <p className="m-0 text-sm leading-relaxed text-base-content/70">
            Стартовая матрица для окна запуска. В конкретной сессии её можно
            переопределить перед спавном.
          </p>
        </div>
      </header>

      {data === undefined ? (
        <p className="m-0 text-sm text-base-content/60">Загружаем…</p>
      ) : (
        <SkillModeDefaultsTable
          data={data}
          disabled={mutation.isPending}
          onToggle={(mode, skillId, enabled) => {
            mutation.mutate({
              defaults: buildEntries(data, mode, skillId, enabled)
            });
          }}
        />
      )}

      {mutation.error !== null ? (
        <p role="alert" className="m-0 text-xs text-error">
          Не удалось сохранить матрицу скилов.
        </p>
      ) : null}
    </section>
  );
}

interface SkillModeDefaultsTableProps {
  data: SkillModeDefaults;
  disabled: boolean;
  onToggle: (mode: string, skillId: string, enabled: boolean) => void;
}

function SkillModeDefaultsTable({
  data,
  disabled,
  onToggle
}: SkillModeDefaultsTableProps) {
  return (
    <div className="overflow-x-auto rounded-md border border-base-300">
      <table className="table table-zebra table-sm m-0">
        <thead>
          <tr>
            <th className="w-28">Режим</th>
            {data.skills.map((skill) => (
              <th key={skill.skill_id} title={skill.description}>
                {skill.title}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {data.modes.map((mode) => (
            <tr key={mode.mode}>
              <th className="font-mono text-xs">
                {SKILL_MODE_LABELS[mode.mode] ?? mode.mode}
              </th>
              {data.skills.map((skill) => {
                const item = mode.defaults.find(
                  (d) => d.skill_id === skill.skill_id
                );
                const checked = item?.enabled ?? false;
                return (
                  <td key={skill.skill_id}>
                    <input
                      type="checkbox"
                      className="checkbox checkbox-sm"
                      aria-label={`${mode.mode} ${skill.skill_id}`}
                      checked={checked}
                      disabled={disabled}
                      onChange={(event) => {
                        onToggle(
                          mode.mode,
                          skill.skill_id,
                          event.target.checked
                        );
                      }}
                    />
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function buildEntries(
  data: SkillModeDefaults,
  mode: string,
  skillId: string,
  enabled: boolean
): SkillModeDefaultEntry[] {
  return data.modes.flatMap((row) =>
    row.defaults.map((item) => ({
      mode: row.mode,
      skill_id: item.skill_id,
      enabled:
        row.mode === mode && item.skill_id === skillId ? enabled : item.enabled
    }))
  );
}
