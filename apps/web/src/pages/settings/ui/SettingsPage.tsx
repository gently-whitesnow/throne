import { FolderCog, GitBranch, KeyRound } from "lucide-react";

import { GitProvidersCard } from "@/widgets/git-providers-card";
import { McpTokenCard } from "@/widgets/mcp-token-card";
import { WorkspaceCard } from "@/widgets/workspace-card";

/**
 * `/settings` — единая страница настроек профиля.
 *
 * Три секции:
 *   * «Провайдеры Git» — статус `gh auth status`.
 *   * «Workspace» — корень `Throne:Workspace:Root` и агрегированный размер на диске.
 *   * «MCP-токен» — Personal Access Token для MCP-клиентов (нужен только при production-развёртывании на сервере).
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
          Провайдеры Git, параметры workspace и MCP-токен в одном месте.
        </p>
      </header>

      <SettingsSection
        id="git-providers"
        title="Провайдеры Git"
        icon={GitBranch}
        description="Привязка локальных Git-провайдеров: статус gh CLI, авторизация и scopes."
      >
        <GitProvidersCard />
      </SettingsSection>

      <SettingsSection
        id="workspace"
        title="Workspace"
        icon={FolderCog}
        description="Корневая директория клонов репозиториев и её агрегированный размер на диске. Per-intent размеры появятся в отдельных проходах."
      >
        <WorkspaceCard />
      </SettingsSection>

      <SettingsSection
        id="mcp-token"
        title="MCP-токен"
        icon={KeyRound}
        description="Нужен только при production-развёртывании Throne на сервере: MCP-клиенты подключаются по Bearer-токену. Локально выпускать токен смысла нет — MCP ходит в локальный процесс без авторизации."
      >
        <McpTokenCard />
      </SettingsSection>
    </div>
  );
}

type LucideIcon = typeof KeyRound;

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
