import { AlertCircle } from "lucide-react";
import { useCallback, useMemo, useState } from "react";

import { isCapabilityEnabled, useCapabilities } from "@/entities/capability";
import type { IntentStatus } from "@/entities/intent";
import {
  isCloneReady,
  useIntentRepositories
} from "@/entities/repository-binding";

import { buildAgentPrompt, buildClaudeCliCommand } from "../model/prompt";
import { useTerminalSession } from "../model/use-terminal-session";
import { defaultRunModeForStatus } from "../model/types";
import type { TerminalRunMode } from "../model/types";

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
 * - Dropdown режимов + Copy prompt всегда отображаются (хардкод-промпт,
 *   load-bearing для MiniRouter — см. родительский Slice 2 «Решения интервью v2 → Q8»).
 * - Run-кнопка и xterm-блок рендерятся только при `terminal`-capability == enabled.
 * - Pre-flight: Run disabled пока есть not-ready binding'и; per-binding
 *   прогресс на основе `useIntentRepositories`, который уже подписан на
 *   realtime-эвент `intent.repository_clone_progress`.
 * - Live-сессия (`tmux has-session` → true): dropdown и Copy prompt
 *   замораживаются, появляется Restart-кнопка.
 */
export function AgentTerminalPanel({
  intentId,
  intentStatus
}: AgentTerminalPanelProps) {
  const [mode, setMode] = useState<TerminalRunMode>(() =>
    defaultRunModeForStatus(intentStatus)
  );
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
    : hasBlockingBinding
      ? "Дождитесь готовности клонов всех репозиториев."
      : null;

  const promptText = useMemo(
    () => buildClaudeCliCommand(buildAgentPrompt(mode, intentId)),
    [mode, intentId]
  );

  // Берём стабильные функции хука напрямую: иначе колбэки зависели бы от
  // объекта `session`, который пересоздаётся каждый рендер, и onClosed менял
  // бы идентичность — это перезапускало бы эффект TerminalView и рвало живой
  // сокет на любом постороннем ре-рендере панели.
  const {
    start: startSession,
    restart: restartSession,
    markSessionEnded
  } = session;

  const handleRun = useCallback(() => {
    void startSession(mode);
  }, [startSession, mode]);

  const handleRestart = useCallback(() => {
    void restartSession(mode);
  }, [restartSession, mode]);

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
      className="mt-6 flex flex-col gap-3 rounded-lg border border-base-300 bg-base-100 px-4 py-3"
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
        promptText={promptText}
        onRun={handleRun}
        onRestart={handleRestart}
        runDisabled={!terminalEnabled || hasBlockingBinding}
        runDisabledReason={runDisabledReason}
        sessionLive={sessionLive}
        isStarting={session.isStarting}
        terminalEnabled={terminalEnabled}
      />

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
    </section>
  );
}
