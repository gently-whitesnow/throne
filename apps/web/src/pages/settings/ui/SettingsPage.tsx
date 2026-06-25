import {
  Bot,
  FolderCog,
  GitBranch,
  ShieldCheck,
  ToggleRight
} from "lucide-react";

import { CapabilitiesCard } from "@/widgets/capabilities-card";
import { GitProvidersCard } from "@/widgets/git-providers-card";
import { AgentVendorsCard } from "@/widgets/settings-cards/agent-vendors-card";
import { ReadinessPanel } from "@/widgets/settings-cards/readiness-panel";
import { WorkspaceCard } from "@/widgets/workspace-card";

import { TerminalDefaultsCard } from "./TerminalDefaultsCard";

/**
 * `/settings` — единая страница настроек профиля, организованная вокруг
 * критерия «Throne готов».
 *
 * Секции:
 *   * «Готовность» — агрегированный чеклист (вендор/git/workspace).
 *   * «Агенты» — дефолтный вендор и статус логина каждого агента.
 *   * «Git-провайдеры» — статус gh/glab + GitLab host.
 *   * «Workspace» — корень клонов и размер на диске.
 *   * «Фичи» — carrier-фичи: выбор провайдера (например, Open in IDE).
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
          Готовность Throne к работе, агенты, провайдеры Git, workspace и
          опциональные фичи в одном месте.
        </p>
      </header>

      <section aria-label="Готовность Throne">
        <ReadinessPanel />
      </section>

      <SettingsSection
        id="agents"
        title="Агенты"
        icon={Bot}
        description="Каким агентом (claude или codex) предзаполнять запуск новых сессий и статус логина каждого CLI. Модель и усилие выбираются per-сессия на странице интента."
      >
        <TerminalDefaultsCard />
        <AgentVendorsCard />
      </SettingsSection>

      <SettingsSection
        id="git-providers"
        title="Git-провайдеры"
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
        id="features"
        title="Фичи"
        icon={ToggleRight}
        description="Carrier-фичи: выберите конкретного провайдера или оставьте «Авто», чтобы Throne подобрал доступного локально."
      >
        <CapabilitiesCard only={["open_in_ide", "open_in_terminal"]} />
        <p className="m-0 flex items-center gap-1.5 text-xs leading-relaxed text-base-content/60">
          <ShieldCheck aria-hidden size={13} strokeWidth={2} />
          Новые провайдеры (JetBrains и др.) появятся в этом списке по мере
          добавления — provider-neutral задел.
        </p>
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
