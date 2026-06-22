import { SkillModeDefaultsCard } from "@/widgets/skill-mode-defaults-card";

/**
 * `/launch-skills` — глобальные defaults скилов запуска для каждого режима. Окно
 * запуска интента позволяет переопределить выбор на конкретную сессию.
 */
export function LaunchSkillsPage() {
  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-8 px-5 py-8">
      <header className="flex flex-col gap-1.5">
        <p className="m-0 text-xs font-bold uppercase tracking-wider text-primary">
          Запуск
        </p>
        <h1 className="m-0 text-2xl font-bold leading-tight">Скилы запуска</h1>
        <p className="m-0 max-w-[64ch] text-sm leading-relaxed text-base-content/70">
          Глобальные defaults скилов для каждого режима. Окно запуска интента
          позволяет переопределить выбор на конкретную сессию.
        </p>
      </header>

      <SkillModeDefaultsCard />
    </div>
  );
}
