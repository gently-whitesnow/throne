import {
  Cpu,
  FolderCog,
  GitBranch,
  TerminalSquare,
  ToggleRight
} from "lucide-react";

import { CapabilitiesCard } from "@/widgets/capabilities-card";
import { GitProvidersCard } from "@/widgets/git-providers-card";
import { WorkspaceCard } from "@/widgets/workspace-card";

import { LocalModelCard } from "./LocalModelCard";
import { TerminalDefaultsCard } from "./TerminalDefaultsCard";

/**
 * `/settings` — единая страница настроек профиля.
 *
 * Секции:
 *   * «Возможности» — capability-gating (Slice 2): repositories, terminal, vscode.
 *   * «Терминал» — дефолтный вендор агента (claude | codex) для новых сессий.
 *   * «Провайдеры Git» — статус `gh auth status`.
 *   * «Workspace» — корень `Throne:Workspace:Root` и агрегированный размер на диске.
 */
export function SettingsPage() {
  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-8 px-5 py-8">
      <header className="flex flex-col gap-1.5">
        <p className="m-0 text-xs font-bold uppercase tracking-wider text-primary">
          Профиль
        </p>
        <h1 className="m-0 text-2xl font-bold leading-tight">Настройки</h1>
        <p className="m-0 max-w-[64ch] text-sm leading-relaxed text-base-content/70">
          Возможности, провайдеры Git и параметры workspace в одном месте.
        </p>
      </header>

      <SettingsSection
        id="capabilities"
        title="Возможности"
        icon={ToggleRight}
        description="Фичи Throne с внешними зависимостями (gh, tmux, code). Default OFF: включите тогл осознанно после того, как установлен соответствующий CLI. Терминал, Run, «Open in VS Code» и репозитории требуют доступа к хосту: бэкенд надо запускать нативно на хосте (профиль «только web+db», docker-compose.host.yml) — в контейнерном режиме они не детектятся и остаются выключены."
      >
        <CapabilitiesCard />
      </SettingsSection>

      <SettingsSection
        id="terminal"
        title="Терминал"
        icon={TerminalSquare}
        description="Какой агент (claude или codex) предлагать по умолчанию при запуске встроенного терминала. Модель и уровень усилия выбираются per-сессия на странице интента."
      >
        <TerminalDefaultsCard />
      </SettingsSection>

      <SettingsSection
        id="git-providers"
        title="Провайдеры Git"
        icon={GitBranch}
        description="Привязка локальных Git-провайдеров: статус gh CLI, авторизация и scopes."
      >
        <GitProvidersCard />
      </SettingsSection>

      <SettingsSection
        id="local-model"
        title="Локальные модели"
        icon={Cpu}
        description="Адрес локального OpenAI-совместимого endpoint (Throne:LocalModel:BaseUrl) и модели, прочитанные через /v1/models. Пустой или недоступный endpoint показывается как состояние, а не ошибка."
      >
        <LocalModelCard />
      </SettingsSection>

      <SettingsSection
        id="workspace"
        title="Workspace"
        icon={FolderCog}
        description="Корневая директория клонов репозиториев и её агрегированный размер на диске. Per-intent размеры появятся в отдельных проходах."
      >
        <WorkspaceCard />
      </SettingsSection>
    </div>
  );
}

type LucideIcon = typeof ToggleRight;

interface SettingsSectionProps {
  id: string;
  title: string;
  icon: LucideIcon;
  description: string;
  children: React.ReactNode;
}

function SettingsSection({
  id,
  title,
  icon: Icon,
  description,
  children
}: SettingsSectionProps) {
  const headingId = `settings-${id}-title`;
  return (
    <section
      aria-labelledby={headingId}
      className="flex flex-col gap-3"
      id={id}
    >
      <div className="flex flex-col gap-1">
        <div className="flex items-center gap-2">
          <Icon
            aria-hidden
            size={18}
            strokeWidth={2}
            className="text-base-content/70"
          />
          <h2
            id={headingId}
            className="m-0 text-lg font-semibold leading-tight"
          >
            {title}
          </h2>
        </div>
        <p className="m-0 max-w-[64ch] text-sm leading-relaxed text-base-content/70">
          {description}
        </p>
      </div>
      {children}
    </section>
  );
}
