import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import type { IntentStatus } from "@/entities/intent";
import {
  isCloneReady,
  useIntentRepositories
} from "@/entities/repository-binding";

import type { TerminalReasoningEffort } from "@/entities/terminal-setting";

import { useAttachableSkills } from "../model/use-attachable-skills";
import { useLaunchAxis } from "../model/use-launch-axis";
import { useReviewTargetSelection } from "../model/use-review-target-selection";
import { useTerminalSession } from "../model/use-terminal-session";
import { TERMINAL_RUN_MODES, defaultRunModeForStatus } from "../model/types";
import type { TerminalRunMode, TerminalRunPayload } from "../model/types";

import { PreflightModal } from "./PreflightModal";
import { ReviewTargetSelect } from "./ReviewTargetSelect";
import { RunControls } from "./RunControls";
import { SkillsAttachControl } from "./SkillsAttachControl";
import { TerminalPanelAlerts } from "./TerminalPanelAlerts";
import { TerminalView } from "./TerminalView";

interface AgentTerminalPanelProps {
  intentId: string;
  intentStatus: IntentStatus;
}

export function AgentTerminalPanel({
  intentId,
  intentStatus
}: AgentTerminalPanelProps) {
  const [mode, setMode] = useState<TerminalRunMode>(() =>
    defaultRunModeForStatus(intentStatus)
  );

  const { bindings } = useIntentRepositories(intentId);

  const session = useTerminalSession(intentId, true);
  const sessionLive =
    session.state === "running" || session.state === "spawning";
  const sessionLaunch = session.lastResponse?.launch ?? null;

  const prefillReady = session.probeSettled;

  const axis = useLaunchAxis({
    sessionLaunch,
    sessionLive,
    ready: prefillReady
  });

  const notReady = useMemo(
    () => bindings.filter((b) => !isCloneReady(b.clone_status)),
    [bindings]
  );
  const reviewSelection = useReviewTargetSelection(bindings);
  const availableModes = useMemo(
    () =>
      reviewSelection.hasReviewTarget
        ? TERMINAL_RUN_MODES
        : TERMINAL_RUN_MODES.filter((m) => m !== "review"),
    [reviewSelection.hasReviewTarget]
  );

  // Pre-fill mode from the intent's persisted launch once the probe settles (mirrors the axis
  // prefill), then fall back to the status default. One-shot — the operator's later picks win.
  const modeSeeded = useRef(false);
  useEffect(() => {
    if (modeSeeded.current || !prefillReady) return;
    modeSeeded.current = true;
    if (sessionLaunch !== null && availableModes.includes(sessionLaunch.mode)) {
      setMode(sessionLaunch.mode);
    }
  }, [prefillReady, sessionLaunch, availableModes]);

  useEffect(() => {
    if (!availableModes.includes(mode)) {
      setMode(defaultRunModeForStatus(intentStatus));
    }
  }, [availableModes, intentStatus, mode]);

  // While live the mode control shows the running session's real mode, not the draft (frozen
  // anyway); a restart with a different mode is reflected immediately.
  const effectiveMode =
    sessionLive && sessionLaunch !== null ? sessionLaunch.mode : mode;

  const hasBlockingBinding = notReady.length > 0;
  const reviewBindingId =
    effectiveMode === "review" && reviewSelection.reviewTargets.length > 1
      ? reviewSelection.selectedReviewTargetId
      : null;
  const hasMissingReviewTarget =
    effectiveMode === "review" &&
    reviewSelection.reviewTargets.length > 1 &&
    reviewSelection.selectedReviewTargetId === "";

  const runDisabledReason = axis.metadataError
    ? "Не удалось загрузить список агентов. Обновите страницу."
    : !axis.launchReady
      ? "Загружаем список агентов…"
      : hasBlockingBinding
        ? "Дождитесь готовности клонов всех репозиториев."
        : hasMissingReviewTarget
          ? "Выберите PR/MR для review."
          : null;

  const [preflight, setPreflight] = useState<"run" | "restart" | null>(null);

  const launchArgs = axis.launchArgs(effectiveMode);

  // Берём стабильные функции хука напрямую: иначе колбэки зависели бы от
  // объекта `session`, который пересоздаётся каждый рендер, и onClosed менял
  // бы идентичность — это перезапускало бы эффект TerminalView и рвало живой
  // сокет на любом постороннем ре-рендере панели.
  const {
    start: startSession,
    restart: restartSession,
    attachSkills: attachSessionSkills,
    markSessionEnded
  } = session;

  const attachedSkillIds = sessionLaunch?.attached_skill_ids ?? [];
  const attachable = useAttachableSkills(intentId, effectiveMode, sessionLive);

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
      </header>

      <div className="flex flex-wrap items-center gap-3">
        <RunControls
          mode={effectiveMode}
          modes={availableModes}
          onModeChange={setMode}
          vendors={axis.vendors}
          vendor={axis.vendor ?? ""}
          onVendorChange={axis.onVendorChange}
          models={axis.selectedMeta?.models ?? []}
          model={axis.model ?? ""}
          onModelChange={axis.setModel}
          efforts={
            (axis.selectedMeta?.efforts ??
              []) as readonly TerminalReasoningEffort[]
          }
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
            !axis.launchReady || hasBlockingBinding || hasMissingReviewTarget
          }
          runDisabledReason={runDisabledReason}
          sessionLive={sessionLive}
          isStarting={session.isStarting}
          isStopping={session.isStopping}
        />
        <SkillsAttachControl
          attachedSkillIds={attachedSkillIds}
          available={attachable.skills}
          sessionLive={sessionLive}
          isLoadingAvailable={attachable.status === "loading"}
          isAttaching={session.isAttachingSkills}
          onAttach={(ids) => {
            void attachSessionSkills(ids);
          }}
        />
      </div>

      {effectiveMode === "review" ? (
        <ReviewTargetSelect
          targets={reviewSelection.reviewTargets}
          value={reviewSelection.selectedReviewTargetId}
          disabled={session.isStarting || session.isStopping}
          onChange={reviewSelection.setSelectedReviewBindingId}
        />
      ) : null}

      <TerminalPanelAlerts
        metadataError={axis.metadataError}
        notReadyBindings={notReady}
        sessionError={session.error}
      />

      {session.startedAt !== null ? (
        <TerminalView
          intentId={intentId}
          attempt={session.startedAt.attempt}
          onClosed={handleTerminalClosed}
        />
      ) : null}

      {session.state === "exited" && session.startedAt === null ? (
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
          reviewBindingId={reviewBindingId}
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
