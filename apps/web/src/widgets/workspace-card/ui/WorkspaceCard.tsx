import {
  AlertCircle,
  FolderCog,
  HardDrive,
  Loader2,
  RefreshCw
} from "lucide-react";

import {
  formatWorkspaceSize,
  isWorkspaceCalculating,
  useWorkspaceSettings,
  type WorkspaceSettings
} from "@/entities/workspace-setting";
import { Button } from "@/shared/ui";

/**
 * Settings → «Workspace».
 *
 * Показывает `Throne:Workspace:Root` (моноширинно) и агрегированный размер.
 * Размер считается лениво на бэке: при `status=calculating` хук `useWorkspaceSettings`
 * сам опрашивает эндпоинт, пока статус не перейдёт в `ready` — здесь только UI.
 * Per-intent размер вне scope.
 */
export function WorkspaceCard() {
  const { settings, isLoading, error, refresh } = useWorkspaceSettings();

  return (
    <section
      aria-label="Workspace"
      className="flex flex-col gap-4 rounded-lg border border-base-300 bg-base-100 p-5"
    >
      <header className="flex items-start justify-between gap-3">
        <div className="flex items-start gap-3">
          <span
            aria-hidden
            className="inline-flex h-9 w-9 items-center justify-center rounded-md bg-primary/10 text-primary"
          >
            <FolderCog size={18} strokeWidth={2} />
          </span>
          <div className="flex flex-col gap-1">
            <h3 className="m-0 text-base font-bold leading-tight">
              Корень workspace
            </h3>
            <p className="m-0 max-w-[60ch] text-sm leading-relaxed text-base-content/70">
              Директория, в которую клонируются репозитории интентов (
              <code className="font-mono">Throne:Workspace:Root</code>), и её
              агрегированный размер на диске.
            </p>
          </div>
        </div>
        <Button
          aria-label="Перепроверить размер workspace"
          disabled={isLoading}
          icon={
            <RefreshCw
              aria-hidden
              size={16}
              strokeWidth={2}
              className={isLoading ? "animate-spin" : undefined}
            />
          }
          onClick={refresh}
        >
          {isLoading ? "Обновляем…" : "Обновить"}
        </Button>
      </header>

      <WorkspaceBody isLoading={isLoading} error={error} settings={settings} />
    </section>
  );
}

interface WorkspaceBodyProps {
  isLoading: boolean;
  error: Error | null;
  settings: WorkspaceSettings | null;
}

function WorkspaceBody({ isLoading, error, settings }: WorkspaceBodyProps) {
  if (error) {
    return (
      <p
        role="alert"
        className="m-0 flex items-start gap-2 rounded-md border border-error/30 bg-error/10 px-3 py-2 text-sm text-error"
      >
        <AlertCircle aria-hidden size={16} strokeWidth={2} className="mt-0.5" />
        <span>Не удалось получить параметры workspace: {error.message}</span>
      </p>
    );
  }

  if (!settings && isLoading) {
    return (
      <p className="m-0 text-sm text-base-content/60">Загружаем параметры…</p>
    );
  }

  if (!settings) {
    return <p className="m-0 text-sm text-base-content/60">Нет данных.</p>;
  }

  const calculating = isWorkspaceCalculating(settings.status);

  const hostRoot = settings.host_root?.trim();

  return (
    <dl className="grid grid-cols-[auto_1fr] items-baseline gap-x-3 gap-y-2 text-sm">
      <dt className="text-base-content/60">Путь</dt>
      <dd className="m-0 break-all font-mono text-xs leading-relaxed">
        {settings.root}
      </dd>
      {hostRoot ? (
        <>
          <dt className="text-base-content/60">На хосте</dt>
          <dd
            className="m-0 break-all font-mono text-xs leading-relaxed"
            data-testid="workspace-host-root"
          >
            {hostRoot}
          </dd>
        </>
      ) : null}
      <dt className="text-base-content/60">Размер</dt>
      <dd
        className="m-0 flex items-center gap-2"
        data-testid="workspace-size"
        data-status={settings.status}
      >
        <HardDrive
          aria-hidden
          size={14}
          strokeWidth={2}
          className="text-base-content/50"
        />
        {calculating ? (
          <span className="inline-flex items-center gap-1.5 text-base-content/60">
            <Loader2
              aria-hidden
              size={14}
              strokeWidth={2}
              className="animate-spin"
            />
            Считаем размер…
          </span>
        ) : (
          <span className="font-mono">
            {formatWorkspaceSize(settings.total_size_bytes ?? undefined)}
          </span>
        )}
      </dd>
    </dl>
  );
}
