import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import { useDetectedTerminalProviders } from "@/entities/capability";
import type { IntentStatus } from "@/entities/intent";
import {
  isCloneReady,
  useIntentRepositories
} from "@/entities/repository-binding";

import type { TerminalReasoningEffort } from "@/entities/terminal-setting";
import { SessionLiveBadge } from "@/shared/ui";

import { useAttachableSkills } from "../model/use-attachable-skills";
import { useLaunchAxis } from "../model/use-launch-axis";
import { useReviewTargetSelection } from "../model/use-review-target-selection";
import { useTerminalSession } from "../model/use-terminal-session";
import { TERMINAL_RUN_MODES, defaultRunModeForStatus } from "../model/types";
import type { TerminalRunPayload } from "../model/types";

import { LaunchAxisBadges } from "./LaunchAxisBadges";
import { LaunchAxisControls } from "./LaunchAxisControls";
import { PreflightModal } from "./PreflightModal";
import { RunControls } from "./RunControls";
import { SkillsAttachControl } from "./SkillsAttachControl";
import { TerminalLiveViewers } from "./TerminalLiveViewers";
import { TerminalPanelAlerts } from "./TerminalPanelAlerts";

interface AgentTerminalPanelProps {
  intentId: string;
  intentStatus: IntentStatus;
}

export function AgentTerminalPanel({
  intentId,
  intentStatus
}: AgentTerminalPanelProps) {
  const [mode, setMode] = useState(() => defaultRunModeForStatus(intentStatus));

  const { bindings } = useIntentRepositories(intentId);

  const session = useTerminalSession(intentId, true);
  const sessionLive =
    session.state === "running" || session.state === "spawning";
  const sessionLaunch = session.lastResponse?.launch ?? null;
  const liveLaunch = sessionLive ? sessionLaunch : null;

  const prefillReady = session.probeSettled;

  const axis = useLaunchAxis({ sessionLaunch, ready: prefillReady });

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

  const hasBlockingBinding = notReady.length > 0;
  // Launch path uses the draft `mode` (not the live session's mode): the «Начать работу» flow
  // launches `work` while an interview session is still live.
  const reviewActive =
    mode === "review" && reviewSelection.reviewTargets.length > 1;
  const reviewBindingId = reviewActive
    ? reviewSelection.selectedReviewTargetId
    : null;
  const modalLaunchBlockedReason =
    reviewActive && reviewSelection.selectedReviewTargetId === ""
      ? "Выберите PR/MR для review."
      : null;

  const runDisabledReason = axis.metadataError
    ? "Не удалось загрузить список агентов. Обновите страницу."
    : !axis.launchReady
      ? "Загружаем список агентов…"
      : hasBlockingBinding
        ? "Дождитесь готовности клонов всех репозиториев."
        : null;

  const [preflightOpen, setPreflightOpen] = useState(false);
  const terminalProviders = useDetectedTerminalProviders();

  const launchArgs = axis.launchArgs(mode);

  // Берём стабильные функции хука напрямую: иначе колбэки зависели бы от
  // объекта `session`, который пересоздаётся каждый рендер, и onClosed менял
  // бы идентичность — это перезапускало бы эффект TerminalView и рвало живой
  // сокет на любом постороннем ре-рендере панели.
  const {
    start: startSession,
    kill: killSession,
    openNative: openNativeSession,
    attachSkills: attachSessionSkills,
    markSessionEnded
  } = session;

  // Skills hot-attach to the running session, so it follows the live mode, not the draft.
  const skillsMode = liveLaunch ? liveLaunch.mode : mode;
  const attachable = useAttachableSkills(intentId, skillsMode, sessionLive);

  const handleLaunch = useCallback(
    (payload: TerminalRunPayload) => {
      setPreflightOpen(false);
      // Restart = kill + start (no dedicated endpoint, by design): when a session is live (the
      // «Начать работу» flow), tear it down before spawning the new one. Kill awaits the tmux
      // session being gone, so the spawn's session-slot guard passes.
      void (async () => {
        if (sessionLive) {
          await killSession();
        }
        await startSession(payload);
      })();
    },
    [sessionLive, killSession, startSession]
  );

  const handleKill = useCallback(() => {
    void killSession();
  }, [killSession]);

  const handleStartWork = useCallback(() => {
    setMode("work");
    setPreflightOpen(true);
  }, []);

  const showStartWork =
    sessionLive &&
    liveLaunch?.mode === "interview" &&
    intentStatus === "awaiting_operator";

  const vendorLabel = liveLaunch
    ? (axis.vendors.find((v) => v.vendor === liveLaunch.vendor)?.label ??
      liveLaunch.vendor)
    : "";

  return (
    <section
      aria-label="Запустить агента"
      data-testid="agent-terminal-panel"
      className="flex flex-col gap-3 rounded-lg border border-base-300 bg-base-100 px-4 py-3"
    >
      <div className="flex flex-wrap items-center gap-3">
        {sessionLive ? (
          <SessionLiveBadge testId="agent-terminal-live-badge" />
        ) : null}
        {liveLaunch ? (
          <LaunchAxisBadges launch={liveLaunch} vendorLabel={vendorLabel} />
        ) : null}
        <RunControls
          sessionLive={sessionLive}
          onRun={() => {
            setPreflightOpen(true);
          }}
          onKill={handleKill}
          showStartWork={showStartWork}
          onStartWork={handleStartWork}
          runDisabled={!axis.launchReady || hasBlockingBinding}
          runDisabledReason={runDisabledReason}
          isStarting={session.isStarting}
          isStopping={session.isStopping}
        />
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <SkillsAttachControl
          available={attachable.skills}
          sessionLive={sessionLive}
          isLoadingAvailable={attachable.status === "loading"}
          isAttaching={session.isAttachingSkills}
          onAttach={(ids) => {
            // After hot-attach the backend unions the skills into the live mode's
            // selection; reload the preview so `selected` (badges + checkboxes) refreshes.
            void attachSessionSkills(ids).then(() => {
              attachable.reload();
            });
          }}
        />
      </div>

      <TerminalPanelAlerts
        metadataError={axis.metadataError}
        notReadyBindings={notReady}
        sessionError={session.error}
        submitUnconfirmed={sessionLive && session.submitUnconfirmed}
      />

      <TerminalLiveViewers
        intentId={intentId}
        sessionLive={sessionLive}
        startedAt={session.startedAt}
        isOpeningNative={session.isOpeningNative}
        hasNativeProviders={terminalProviders.length > 0}
        openNative={openNativeSession}
        markSessionEnded={markSessionEnded}
      />

      {session.state === "exited" && session.startedAt === null ? (
        <p className="m-0 text-xs text-base-content/60">
          Сессия завершена. Нажмите «Запустить в терминале», чтобы начать
          заново.
        </p>
      ) : null}

      {launchArgs !== null ? (
        <PreflightModal
          open={preflightOpen}
          intentId={intentId}
          launch={launchArgs}
          reviewBindingId={reviewBindingId}
          actionLabel="Запустить"
          isSubmitting={session.isStarting}
          launchBlockedReason={modalLaunchBlockedReason}
          controls={
            <LaunchAxisControls
              mode={mode}
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
              reviewTargets={reviewSelection.reviewTargets}
              reviewBindingValue={reviewSelection.selectedReviewTargetId}
              onReviewBindingChange={reviewSelection.setSelectedReviewBindingId}
              reviewDisabled={session.isStarting || session.isStopping}
            />
          }
          onClose={() => {
            setPreflightOpen(false);
          }}
          onLaunch={handleLaunch}
        />
      ) : null}
    </section>
  );
}
