import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import { TerminalSquare } from "lucide-react";

import { useDetectedTerminalProviders } from "@/entities/capability";
import type { IntentStatus } from "@/entities/intent";
import {
  isCloneReady,
  useIntentRepositories
} from "@/entities/repository-binding";

import type { TerminalReasoningEffort } from "@/entities/terminal-setting";
import { Button, SessionLiveBadge } from "@/shared/ui";

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

  // While live the mode control shows the running session's real mode, not the draft.
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

  const [preflightOpen, setPreflightOpen] = useState(false);
  // Хэндофф во внешний терминал: встроенный гасим (StopPipe на бэке), сессия
  // живёт. Из этого состояния возвращаемся кнопкой «Вернуться во встроенный».
  const [nativeActive, setNativeActive] = useState(false);
  const nativeSwitchRef = useRef(false);
  const terminalProviders = useDetectedTerminalProviders();

  const launchArgs = axis.launchArgs(effectiveMode);

  // Берём стабильные функции хука напрямую: иначе колбэки зависели бы от
  // объекта `session`, который пересоздаётся каждый рендер, и onClosed менял
  // бы идентичность — это перезапускало бы эффект TerminalView и рвало живой
  // сокет на любом постороннем ре-рендере панели.
  const {
    start: startSession,
    openNative: openNativeSession,
    attachSkills: attachSessionSkills,
    markSessionEnded
  } = session;

  const attachable = useAttachableSkills(intentId, effectiveMode, sessionLive);

  const handleLaunch = useCallback(
    (payload: TerminalRunPayload) => {
      setPreflightOpen(false);
      void startSession(payload);
    },
    [startSession]
  );

  const handleKill = useCallback(() => {
    void session.kill();
  }, [session]);

  const handleOpenNative = useCallback(() => {
    nativeSwitchRef.current = true;
    setNativeActive(true);
    void openNativeSession();
  }, [openNativeSession]);

  // Возврат во встроенный: размонтирование/монтирование TerminalView само
  // переоткрывает WebSocket, а тот заново ставит pipe-pane (re-attach) — отдельный
  // бэкенд-вызов не нужен.
  const handleReturnEmbedded = useCallback(() => {
    // Снимаем «взвод» хэндоффа: после возврата любой close встроенного снова
    // значимый (иначе первый реальный обрыв проглотился бы как плановый).
    nativeSwitchRef.current = false;
    setNativeActive(false);
  }, []);

  useEffect(() => {
    if (!sessionLive) {
      setNativeActive(false);
    }
  }, [sessionLive]);

  // Встроенный открывается автоматически, пока сессия live и не отдана наружу.
  const embeddedVisible = sessionLive && !nativeActive;

  const handleTerminalClosed = useCallback(
    (code: number) => {
      if (nativeSwitchRef.current) {
        nativeSwitchRef.current = false;
        return;
      }
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
      {sessionLive ? (
        <SessionLiveBadge
          testId="agent-terminal-live-badge"
          label="Сессия запущена"
          className="text-[12px] font-medium text-success"
        />
      ) : null}

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
            setPreflightOpen(true);
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

      {sessionLive && terminalProviders.length > 0 ? (
        <div className="flex flex-wrap items-center gap-2">
          <Button
            aria-label="Открыть нативный терминал"
            data-testid="agent-terminal-open-native"
            disabled={session.isOpeningNative || nativeActive}
            icon={<TerminalSquare aria-hidden size={14} strokeWidth={2} />}
            onClick={handleOpenNative}
          >
            {session.isOpeningNative ? "Открываем…" : "Нативный терминал"}
          </Button>
        </div>
      ) : null}

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
        submitUnconfirmed={sessionLive && session.submitUnconfirmed}
      />

      {embeddedVisible && session.startedAt !== null ? (
        <TerminalView
          intentId={intentId}
          attempt={session.startedAt.attempt}
          onClosed={handleTerminalClosed}
        />
      ) : null}

      {sessionLive && nativeActive ? (
        <div
          data-testid="agent-terminal-native-active"
          className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-base-300 bg-base-200 px-3 py-2 text-[13px] text-base-content/70"
        >
          <span>Открыто во внешнем терминале.</span>
          <Button
            data-testid="agent-terminal-return-embedded"
            icon={<TerminalSquare aria-hidden size={14} strokeWidth={2} />}
            onClick={handleReturnEmbedded}
          >
            Вернуться во встроенный
          </Button>
        </div>
      ) : null}

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
          onClose={() => {
            setPreflightOpen(false);
          }}
          onLaunch={handleLaunch}
        />
      ) : null}
    </section>
  );
}
