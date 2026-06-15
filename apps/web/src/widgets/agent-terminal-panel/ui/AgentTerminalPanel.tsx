import { AlertCircle } from "lucide-react";
import { useCallback, useMemo, useState } from "react";

import { isCapabilityEnabled, useCapabilities } from "@/entities/capability";
import type { IntentStatus } from "@/entities/intent";
import {
  isCloneReady,
  useIntentRepositories
} from "@/entities/repository-binding";

import { useLaunchAxis } from "../model/use-launch-axis";
import { useTerminalSession } from "../model/use-terminal-session";
import { defaultRunModeForStatus } from "../model/types";
import type { TerminalRunMode, TerminalRunPayload } from "../model/types";

import { PreflightModal } from "./PreflightModal";
import { PreflightProgress } from "./PreflightProgress";
import { RunControls } from "./RunControls";
import { TerminalView } from "./TerminalView";

interface AgentTerminalPanelProps {
  intentId: string;
  intentStatus: IntentStatus;
}

/**
 * Виджет «Запустить агента» внизу страницы интента.
 *
 * - Dropdown режимов/вендора/модели/усилия задают ось запуска. Списки вендоров,
 *   моделей, усилий и дефолты приходят из backend-каталога (`useLaunchAxis` →
 *   `GET /terminal/vendors`) — фронт их не хардкодит. Сам стартовый контекст
 *   (правила + задача) оператор смотрит и правит в preflight-модалке (ADR-0034/0035).
 * - Run-кнопка и xterm-блок рендерятся только при `terminal`-capability == enabled.
 * - Pre-flight: Run disabled пока есть not-ready binding'и или ещё не пришли
 *   metadata; per-binding прогресс на основе `useIntentRepositories`.
 * - Live-сессия (`tmux has-session` → true): dropdown замораживаются,
 *   появляется Restart-кнопка.
 */
export function AgentTerminalPanel({
  intentId,
  intentStatus
}: AgentTerminalPanelProps) {
  const [mode, setMode] = useState<TerminalRunMode>(() =>
    defaultRunModeForStatus(intentStatus)
  );
  const axis = useLaunchAxis();

  const { capabilities, isLoading: capabilitiesLoading } = useCapabilities();
  const { bindings } = useIntentRepositories(intentId);

  const terminalEnabled = isCapabilityEnabled(capabilities, "terminal");
  const session = useTerminalSession(intentId, terminalEnabled);
  const sessionLive =
    session.state === "running" || session.state === "spawning";

  const notReady = useMemo(
    () => bindings.filter((b) => !isCloneReady(b.clone_status)),
    [bindings]
  );
  const hasBlockingBinding = notReady.length > 0;

  const runDisabledReason = !terminalEnabled
    ? "Включите «Терминал агента» в настройках, чтобы запускать сессии."
    : axis.metadataError
      ? "Не удалось загрузить список агентов. Обновите страницу."
      : !axis.launchReady
        ? "Загружаем список агентов…"
        : hasBlockingBinding
          ? "Дождитесь готовности клонов всех репозиториев."
          : null;

  const [preflight, setPreflight] = useState<"run" | "restart" | null>(null);

  const launchArgs = axis.launchArgs(mode);

  // Берём стабильные функции хука напрямую: иначе колбэки зависели бы от
  // объекта `session`, который пересоздаётся каждый рендер, и onClosed менял
  // бы идентичность — это перезапускало бы эффект TerminalView и рвало живой
  // сокет на любом постороннем ре-рендере панели.
  const {
    start: startSession,
    restart: restartSession,
    markSessionEnded
  } = session;

  const handleLaunch = useCallback(
    (payload: TerminalRunPayload) => {
      const action = preflight;
      setPreflight(null);
      if (action === "restart") {
        void restartSession(payload);
      } else {
        void startSession(payload);
      }
    },
    [preflight, startSession, restartSession]
  );

  const handleKill = useCallback(() => {
    void session.kill();
  }, [session]);

  const handleTerminalClosed = useCallback(
    (code: number) => {
      markSessionEnded();
      if (code === 1008 || code === 1011) {
        // Server-side rejection — keep error visible until next user action.
      }
    },
    [markSessionEnded]
  );

  return (
    <section
      aria-label="Запустить агента"
      data-testid="agent-terminal-panel"
      className="flex flex-col gap-3 rounded-lg border border-base-300 bg-base-100 px-4 py-3"
    >
      <header className="flex items-center justify-between gap-3">
        <h2 className="m-0 text-sm font-semibold text-base-content">
          Запустить агента
        </h2>
        {capabilitiesLoading ? (
          <span className="text-[11px] text-base-content/60">
            Загружаем возможности…
          </span>
        ) : null}
      </header>

      <RunControls
        mode={mode}
        onModeChange={setMode}
        vendors={axis.vendors}
        vendor={axis.vendor ?? ""}
        onVendorChange={axis.onVendorChange}
        models={axis.selectedMeta?.models ?? []}
        model={axis.model ?? ""}
        onModelChange={axis.setModel}
        efforts={axis.selectedMeta?.efforts ?? []}
        effort={axis.effort ?? ""}
        onEffortChange={axis.setEffort}
        supportsEffort={axis.selectedMeta?.supports_effort ?? false}
        metadataLoading={axis.metadataLoading}
        metadataError={axis.metadataError}
        onRun={() => {
          setPreflight("run");
        }}
        onRestart={() => {
          setPreflight("restart");
        }}
        onKill={handleKill}
        runDisabled={
          !terminalEnabled || !axis.launchReady || hasBlockingBinding
        }
        runDisabledReason={runDisabledReason}
        sessionLive={sessionLive}
        isStarting={session.isStarting}
        isStopping={session.isStopping}
        terminalEnabled={terminalEnabled}
      />

      {axis.metadataError ? (
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

      {hasBlockingBinding ? (
        <div className="flex flex-col gap-1">
          <p className="m-0 text-xs text-base-content/70">
            Готовим workspace: спавн агента начнётся, когда все репозитории
            будут клонированы.
          </p>
          <PreflightProgress bindings={notReady} />
        </div>
      ) : null}

      {session.error !== null ? (
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
          <span>{session.error}</span>
        </p>
      ) : null}

      {terminalEnabled && session.startedAt !== null ? (
        <TerminalView
          intentId={intentId}
          attempt={session.startedAt.attempt}
          onClosed={handleTerminalClosed}
        />
      ) : null}

      {terminalEnabled &&
      session.state === "exited" &&
      session.startedAt === null ? (
        <p className="m-0 text-xs text-base-content/60">
          Сессия завершена. Нажмите «Запустить в терминале», чтобы начать
          заново.
        </p>
      ) : null}

      {launchArgs !== null ? (
        <PreflightModal
          open={preflight !== null}
          intentId={intentId}
          launch={launchArgs}
          actionLabel={preflight === "restart" ? "Перезапустить" : "Запустить"}
          isSubmitting={session.isStarting}
          onClose={() => {
            setPreflight(null);
          }}
          onLaunch={handleLaunch}
        />
      ) : null}
    </section>
  );
}
