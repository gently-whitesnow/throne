import {
  AlertCircle,
  AlertTriangle,
  CheckCircle2,
  ExternalLink,
  FolderGit2,
  PlugZap,
  TerminalSquare,
  XCircle,
  type LucideIcon
} from "lucide-react";

import {
  useCapabilities,
  useSetCapabilityEnabled,
  type Capability,
  type CapabilityName
} from "@/entities/capability";

const CAPABILITY_VISUALS: Record<
  CapabilityName,
  { icon: LucideIcon; setupHref: string; setupLabel: string }
> = {
  repositories: {
    icon: FolderGit2,
    setupHref: "https://docs.github.com/en/github-cli/github-cli/quickstart",
    setupLabel: "Установить и авторизовать gh CLI"
  },
  terminal: {
    icon: TerminalSquare,
    setupHref:
      "https://github.com/tmux/tmux/wiki/Installing#installing-the-latest-stable-release",
    setupLabel: "Установить tmux"
  },
  vscode: {
    icon: PlugZap,
    setupHref:
      "https://code.visualstudio.com/docs/setup/mac#_launching-from-the-command-line",
    setupLabel: "Установить команду code в PATH"
  }
};

/**
 * Settings → «Возможности».
 *
 * Карточки для каждой capability из `/api/v1/settings/capabilities`. Тогл
 * включения разрешён даже при `detected=false` — оператор может включить
 * заранее, UI пометит «требуется prerequisite». Default OFF для всех ключей
 * (read-time materialization на бэке), retrofit Slice 1: ключ `repositories`
 * тоже стартует выключённым.
 */
export function CapabilitiesCard() {
  const { capabilities, isLoading, error } = useCapabilities();

  if (error !== null) {
    return (
      <div
        role="alert"
        className="flex items-start gap-2 rounded-lg border border-error/30 bg-error/10 px-4 py-3 text-sm text-error"
      >
        <AlertCircle aria-hidden size={16} strokeWidth={2} className="mt-0.5" />
        <span>Не удалось загрузить список возможностей: {error.message}</span>
      </div>
    );
  }

  if (isLoading && capabilities.length === 0) {
    return (
      <p className="m-0 text-sm text-base-content/60">Загружаем возможности…</p>
    );
  }

  if (capabilities.length === 0) {
    return (
      <p className="m-0 text-sm text-base-content/60">
        На сервере нет ни одной возможности — обновите бэкенд.
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-3" data-testid="capabilities-list">
      {capabilities.map((capability) => (
        <CapabilityRow key={capability.name} capability={capability} />
      ))}
    </div>
  );
}

function CapabilityRow({ capability }: { capability: Capability }) {
  const visuals = CAPABILITY_VISUALS[capability.name];
  const Icon = visuals.icon;
  const toggle = useSetCapabilityEnabled();

  const handleToggle = (next: boolean) => {
    toggle.mutate({ name: capability.name, enabled: next });
  };

  return (
    <section
      data-testid={`capability-card-${capability.name}`}
      data-enabled={capability.enabled}
      data-detected={capability.detected}
      className="flex flex-col gap-3 rounded-lg border border-base-300 bg-base-100 p-4"
    >
      <header className="flex items-start justify-between gap-4">
        <div className="flex min-w-0 items-start gap-3">
          <span
            aria-hidden
            className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary"
          >
            <Icon size={18} strokeWidth={2} />
          </span>
          <div className="flex min-w-0 flex-col gap-1">
            <h3 className="m-0 text-base font-semibold leading-tight">
              {capability.title}
            </h3>
            <p className="m-0 max-w-[60ch] text-sm leading-relaxed text-base-content/70">
              {capability.description}
            </p>
          </div>
        </div>
        <ToggleSwitch
          name={capability.name}
          title={capability.title}
          checked={capability.enabled}
          disabled={toggle.isPending}
          onChange={handleToggle}
        />
      </header>

      <PrerequisiteRow capability={capability} />

      <footer className="flex flex-wrap items-center justify-between gap-3">
        <a
          className="inline-flex items-center gap-1.5 text-sm font-medium text-primary hover:underline"
          href={visuals.setupHref}
          rel="noopener noreferrer"
          target="_blank"
        >
          {visuals.setupLabel}
          <ExternalLink aria-hidden size={14} strokeWidth={2} />
        </a>
        {toggle.error instanceof Error ? (
          <span
            role="alert"
            className="text-xs text-error"
            data-testid={`capability-toggle-error-${capability.name}`}
          >
            Не удалось сохранить тогл: {toggle.error.message}
          </span>
        ) : null}
      </footer>
    </section>
  );
}

function PrerequisiteRow({ capability }: { capability: Capability }) {
  const detected = capability.detected;
  const enabledWithoutPrereq = capability.enabled && !detected;
  const detail =
    capability.detection_detail && capability.detection_detail.length > 0
      ? capability.detection_detail
      : capability.prerequisite_hint;

  return (
    <div className="flex flex-wrap items-center gap-2 text-xs">
      <span
        data-testid={`capability-prereq-${capability.name}`}
        data-detected={detected}
        className={
          detected
            ? "inline-flex items-center gap-1.5 rounded-full bg-success/15 px-2.5 py-0.5 font-semibold text-success"
            : "inline-flex items-center gap-1.5 rounded-full bg-error/10 px-2.5 py-0.5 font-semibold text-error"
        }
      >
        {detected ? (
          <CheckCircle2 aria-hidden size={12} strokeWidth={2.25} />
        ) : (
          <XCircle aria-hidden size={12} strokeWidth={2.25} />
        )}
        {detected ? "Prerequisite OK" : "Prerequisite отсутствует"}
      </span>
      <span className="text-base-content/70">{detail}</span>
      {enabledWithoutPrereq ? (
        <span className="inline-flex items-center gap-1 text-error">
          <AlertTriangle aria-hidden size={12} strokeWidth={2.25} />
          включено, но prerequisite не задетектился
        </span>
      ) : null}
    </div>
  );
}

interface ToggleSwitchProps {
  name: CapabilityName;
  title: string;
  checked: boolean;
  disabled: boolean;
  onChange: (next: boolean) => void;
}

function ToggleSwitch({
  name,
  title,
  checked,
  disabled,
  onChange
}: ToggleSwitchProps) {
  return (
    <label
      className="flex shrink-0 cursor-pointer select-none items-center gap-2"
      htmlFor={`capability-toggle-${name}`}
    >
      <span className="sr-only">{`${title} — включить возможность`}</span>
      <span
        aria-hidden
        className={
          checked
            ? "text-xs font-semibold uppercase tracking-wide text-primary"
            : "text-xs font-semibold uppercase tracking-wide text-base-content/50"
        }
      >
        {checked ? "Вкл" : "Выкл"}
      </span>
      <input
        id={`capability-toggle-${name}`}
        data-testid={`capability-toggle-${name}`}
        type="checkbox"
        className="toggle toggle-primary"
        checked={checked}
        disabled={disabled}
        onChange={(event) => {
          onChange(event.target.checked);
        }}
      />
    </label>
  );
}
