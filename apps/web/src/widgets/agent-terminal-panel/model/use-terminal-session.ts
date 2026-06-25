import { useCallback, useEffect, useState } from "react";

import { errorMessage } from "@/shared/lib";
import { useRealtimeEvent } from "@/shared/realtime";

import {
  attachIntentTerminalSkills,
  getIntentTerminalSession,
  killIntentTerminal,
  openNativeIntentTerminal,
  runIntentTerminal
} from "../api/agent-terminal-api";

import type {
  RunIntentTerminalResponse,
  TerminalRunPayload,
  TerminalSessionState
} from "./types";

export interface TerminalSessionStartedAt {
  /** Per-mount nonce — bumped on every successful spawn so xterm reattaches. */
  attempt: number;
  /** Session name returned by the server (deterministic `throne-{intent_id}`). */
  sessionName: string;
}

export interface TerminalSessionView {
  state: TerminalSessionState | "idle";
  /** Response payload from the most recent /run call or status probe. */
  lastResponse: RunIntentTerminalResponse | null;
  /** True once the initial status probe has resolved (success or error) — gates axis prefill. */
  probeSettled: boolean;
  /** User-facing error from the most recent action. */
  error: string | null;
  /**
   * Soft hint: the background prompt delivery could not confirm the initial prompt was submitted
   * (the session is alive — not an error). Set from the realtime event, cleared on the next run.
   */
  submitUnconfirmed: boolean;
  isStarting: boolean;
  isStopping: boolean;
  isAttachingSkills: boolean;
  isOpeningNative: boolean;
  startedAt: TerminalSessionStartedAt | null;
  start: (payload: TerminalRunPayload) => Promise<void>;
  /** Kill the live tmux session without respawning. */
  kill: () => Promise<void>;
  /** Open the live tmux session in the selected native terminal viewer. */
  openNative: () => Promise<void>;
  /** Hot-attach extra session skills into the live tmux session. */
  attachSkills: (skillIds: string[]) => Promise<void>;
  /** Local-only signal that the WebSocket bridge observed a clean close. */
  markSessionEnded: () => void;
}

interface InternalState {
  state: TerminalSessionState | "idle";
  lastResponse: RunIntentTerminalResponse | null;
  error: string | null;
  submitUnconfirmed: boolean;
  startedAt: TerminalSessionStartedAt | null;
}

const INITIAL: InternalState = {
  state: "idle",
  lastResponse: null,
  error: null,
  submitUnconfirmed: false,
  startedAt: null
};

export function useTerminalSession(
  intentId: string,
  terminalEnabled: boolean
): TerminalSessionView {
  const [internal, setInternal] = useState<InternalState>(INITIAL);
  const [isStarting, setIsStarting] = useState(false);
  const [isStopping, setIsStopping] = useState(false);
  const [isAttachingSkills, setIsAttachingSkills] = useState(false);
  const [isOpeningNative, setIsOpeningNative] = useState(false);
  // The axis prefill must wait for the probe so it seeds from the intent's persisted launch
  // rather than racing it with the catalog default. Tracks only the enabled-path probe: it
  // flips to true when the probe resolves. The capability-off case is handled by the caller
  // (no probe runs), so this stays false there — flipping it true on the disabled→enabled
  // transition would briefly read «settled» with no launch yet and seed the wrong default.
  const [probeSettled, setProbeSettled] = useState(false);

  const apply = useCallback(
    (response: RunIntentTerminalResponse, fromProbe = false) => {
      setInternal((prev) => {
        const hasLiveSession =
          response.session_state === "running" ||
          response.session_state === "spawning";
        const nextAttempt = hasLiveSession
          ? (prev.startedAt?.attempt ?? 0) + 1
          : (prev.startedAt?.attempt ?? 0);
        return {
          state:
            fromProbe && response.session_state === "exited"
              ? "idle"
              : response.session_state,
          lastResponse: response,
          error:
            response.session_state === "blocked"
              ? "Клон части репозиториев не готов — спавн агента невозможен."
              : null,
          // A fresh run/probe response resets the soft submit hint; the realtime event re-raises it
          // a beat later only if the background delivery actually failed to confirm.
          submitUnconfirmed: false,
          startedAt: hasLiveSession
            ? { attempt: nextAttempt, sessionName: response.session_name }
            : null
        };
      });
    },
    []
  );

  useEffect(() => {
    if (!terminalEnabled) return;
    setProbeSettled(false);
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await getIntentTerminalSession(intentId, abort.signal);
        apply(response, true);
      } catch (err) {
        if (abort.signal.aborted) return;
        setInternal((prev) => ({ ...prev, error: deriveErrorMessage(err) }));
      } finally {
        if (!abort.signal.aborted) setProbeSettled(true);
      }
    })();
    return () => {
      abort.abort();
    };
  }, [intentId, terminalEnabled, apply]);

  const onSubmitUnconfirmed = useCallback(
    (payload: { intent_id: string }) => {
      if (payload.intent_id !== intentId) return;
      setInternal((prev) =>
        prev.state === "running" || prev.state === "spawning"
          ? { ...prev, submitUnconfirmed: true }
          : prev
      );
    },
    [intentId]
  );
  useRealtimeEvent("terminal.prompt_submit_unconfirmed", onSubmitUnconfirmed);

  const runImpl = useCallback(
    async (payload: TerminalRunPayload) => {
      setIsStarting(true);
      try {
        const response = await runIntentTerminal(intentId, payload);
        apply(response);
      } catch (err) {
        setInternal((prev) => ({
          ...prev,
          error: deriveErrorMessage(err)
        }));
      } finally {
        setIsStarting(false);
      }
    },
    [intentId, apply]
  );

  const start = useCallback(
    (payload: TerminalRunPayload) => runImpl(payload),
    [runImpl]
  );

  const kill = useCallback(async () => {
    setIsStopping(true);
    try {
      const response = await killIntentTerminal(intentId);
      apply(response);
    } catch (err) {
      setInternal((prev) => ({ ...prev, error: deriveErrorMessage(err) }));
    } finally {
      setIsStopping(false);
    }
  }, [intentId, apply]);

  const attachSkills = useCallback(
    async (skillIds: string[]) => {
      if (skillIds.length === 0) return;
      setIsAttachingSkills(true);
      try {
        const response = await attachIntentTerminalSkills(intentId, skillIds);
        setInternal((prev) => {
          if (prev.lastResponse === null) return prev;
          const launch = prev.lastResponse.launch
            ? {
                ...prev.lastResponse.launch,
                attached_skill_ids: response.attached_skill_ids
              }
            : prev.lastResponse.launch;
          return {
            ...prev,
            error: null,
            lastResponse: { ...prev.lastResponse, launch }
          };
        });
      } catch (err) {
        setInternal((prev) => ({
          ...prev,
          error: errorMessage(err, {
            base: "Не удалось догрузить скилы в сессию",
            byStatus: {
              409: "Сессия не запущена — догрузка возможна только в живую сессию.",
              422: "Скил недоступен для текущего интента или вендор не поддерживается."
            }
          })
        }));
      } finally {
        setIsAttachingSkills(false);
      }
    },
    [intentId]
  );

  const openNative = useCallback(async () => {
    setIsOpeningNative(true);
    try {
      await openNativeIntentTerminal(intentId);
      setInternal((prev) => ({ ...prev, error: null }));
    } catch (err) {
      setInternal((prev) => ({
        ...prev,
        error: errorMessage(err, {
          base: "Не удалось открыть нативный терминал",
          byStatus: {
            409: "Сессия не запущена — нативный терминал можно открыть только для живой сессии.",
            422: "Нативный терминал не настроен или не найден на этом хосте.",
            404: "Интент не найден."
          }
        })
      }));
    } finally {
      setIsOpeningNative(false);
    }
  }, [intentId]);

  const markSessionEnded = useCallback(() => {
    setInternal((prev) =>
      prev.state === "idle"
        ? prev
        : { ...prev, state: "exited", startedAt: null }
    );
  }, []);

  return {
    state: internal.state,
    lastResponse: internal.lastResponse,
    probeSettled,
    error: internal.error,
    submitUnconfirmed: internal.submitUnconfirmed,
    startedAt: internal.startedAt,
    isStarting,
    isStopping,
    isAttachingSkills,
    isOpeningNative,
    start,
    kill,
    openNative,
    attachSkills,
    markSessionEnded
  };
}

function deriveErrorMessage(err: unknown): string {
  return errorMessage(err, {
    base: "Не удалось запустить сессию",
    byStatus: {
      // 409 covers two pre-flight aborts: a live session already exists, or the
      // intent body changed since preview (stale expected_version). Both resolve
      // by reopening the modal — the agent did not start.
      409: "Запуск отклонён: сессия уже запущена или тело интента изменилось с момента предпросмотра. Откройте модалку заново.",
      400: "Недопустимая комбинация вендора, модели и усилия.",
      422: "Возможность «Терминал агента» выключена или tmux недоступен.",
      404: "Интент не найден."
    }
  });
}
