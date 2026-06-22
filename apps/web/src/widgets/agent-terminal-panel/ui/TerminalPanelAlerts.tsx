import { AlertCircle, AlertTriangle } from "lucide-react";

import type { RepositoryBinding } from "@/entities/repository-binding";

import { PreflightProgress } from "./PreflightProgress";

interface TerminalPanelAlertsProps {
  metadataError: boolean;
  notReadyBindings: readonly RepositoryBinding[];
  sessionError: string | null;
  submitUnconfirmed: boolean;
}

export function TerminalPanelAlerts({
  metadataError,
  notReadyBindings,
  sessionError,
  submitUnconfirmed
}: TerminalPanelAlertsProps) {
  return (
    <>
      {metadataError ? (
        <p
          role="alert"
          className="m-0 flex items-start gap-2 rounded-md border border-error/30 bg-error/10 px-3 py-2 text-xs text-error"
        >
          <AlertCircle
            aria-hidden
            size={14}
            strokeWidth={2}
            className="mt-0.5"
          />
          <span>
            Не удалось загрузить список агентов. Обновите страницу, чтобы
            запустить сессию.
          </span>
        </p>
      ) : null}

      {notReadyBindings.length > 0 ? (
        <div className="flex flex-col gap-1">
          <p className="m-0 text-xs text-base-content/70">
            Готовим workspace: спавн агента начнётся, когда все репозитории
            будут клонированы.
          </p>
          <PreflightProgress bindings={notReadyBindings} />
        </div>
      ) : null}

      {sessionError !== null ? (
        <p
          role="alert"
          className="m-0 flex items-start gap-2 rounded-md border border-error/30 bg-error/10 px-3 py-2 text-xs text-error"
        >
          <AlertCircle
            aria-hidden
            size={14}
            strokeWidth={2}
            className="mt-0.5"
          />
          <span>{sessionError}</span>
        </p>
      ) : null}

      {submitUnconfirmed ? (
        <p
          role="status"
          className="m-0 flex items-start gap-2 rounded-md border border-warning/30 bg-warning/10 px-3 py-2 text-xs text-warning"
        >
          <AlertTriangle
            aria-hidden
            size={14}
            strokeWidth={2}
            className="mt-0.5"
          />
          <span>
            Похоже, стартовый промпт не отправился автоматически. Проверьте поле
            ввода в терминале ниже и при необходимости нажмите Enter.
          </span>
        </p>
      ) : null}
    </>
  );
}
