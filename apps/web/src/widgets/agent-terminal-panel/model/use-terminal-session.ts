import { useCallback, useEffect, useState } from "react";

import { HttpError } from "@/shared/api";

import {
  getIntentTerminalSession,
  killIntentTerminal,
  restartIntentTerminal,
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
  /** Response payload from the most recent /run or /restart call. */
  lastResponse: RunIntentTerminalResponse | null;
  /** User-facing error from the most recent action. */
  error: string | null;
  isStarting: boolean;
  isStopping: boolean;
  startedAt: TerminalSessionStartedAt | null;
  start: (payload: TerminalRunPayload) => Promise<void>;
  restart: (payload: TerminalRunPayload) => Promise<void>;
  /** Kill the live tmux session without respawning. */
  kill: () => Promise<void>;
  /** Local-only signal that the WebSocket bridge observed a clean close. */
  markSessionEnded: () => void;
}

interface InternalState {
  state: TerminalSessionState | "idle";
  lastResponse: RunIntentTerminalResponse | null;
  error: string | null;
  startedAt: TerminalSessionStartedAt | null;
}

const INITIAL: InternalState = {
  state: "idle",
  lastResponse: null,
  error: null,
  startedAt: null
};

export function useTerminalSession(
  intentId: string,
  terminalEnabled: boolean
): TerminalSessionView {
  const [internal, setInternal] = useState<InternalState>(INITIAL);
  const [isStarting, setIsStarting] = useState(false);
  const [isStopping, setIsStopping] = useState(false);

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
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await getIntentTerminalSession(intentId, abort.signal);
        apply(response, true);
      } catch (err) {
        if (abort.signal.aborted) return;
        setInternal((prev) => ({ ...prev, error: deriveErrorMessage(err) }));
      }
    })();
    return () => {
      abort.abort();
    };
  }, [intentId, terminalEnabled, apply]);

  const runImpl = useCallback(
    async (payload: TerminalRunPayload, restart: boolean) => {
      setIsStarting(true);
      try {
        const response = restart
          ? await restartIntentTerminal(intentId, payload)
          : await runIntentTerminal(intentId, payload);
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
    (payload: TerminalRunPayload) => runImpl(payload, false),
    [runImpl]
  );
  const restart = useCallback(
    (payload: TerminalRunPayload) => runImpl(payload, true),
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
    error: internal.error,
    startedAt: internal.startedAt,
    isStarting,
    isStopping,
    start,
    restart,
    kill,
    markSessionEnded
  };
}

function deriveErrorMessage(err: unknown): string {
  if (err instanceof HttpError) {
    if (err.status === 409) {
      // 409 covers two pre-flight aborts: a live session already exists, or the
      // intent body changed since preview (stale expected_version). Both resolve
      // by reopening the modal — the agent did not start.
      return "Запуск отклонён: сессия уже запущена или тело интента изменилось с момента предпросмотра. Откройте модалку заново.";
    }
    if (err.status === 400) {
      return "Недопустимая комбинация вендора, модели и усилия.";
    }
    if (err.status === 422) {
      return "Возможность «Терминал агента» выключена или tmux недоступен.";
    }
    if (err.status === 404) {
      return "Интент не найден.";
    }
    return `Не удалось запустить сессию (${String(err.status)}).`;
  }
  return "Не удалось запустить сессию.";
}
