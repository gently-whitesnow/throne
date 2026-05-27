import { useCallback, useState } from "react";

import { HttpError } from "@/shared/api";

import {
  restartIntentTerminal,
  runIntentTerminal
} from "../api/agent-terminal-api";

import type {
  RunIntentTerminalResponse,
  TerminalRunMode,
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
  startedAt: TerminalSessionStartedAt | null;
  start: (mode: TerminalRunMode) => Promise<void>;
  restart: (mode: TerminalRunMode) => Promise<void>;
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

export function useTerminalSession(intentId: string): TerminalSessionView {
  const [internal, setInternal] = useState<InternalState>(INITIAL);
  const [isStarting, setIsStarting] = useState(false);
  const [attempt, setAttempt] = useState(0);

  const apply = useCallback(
    (response: RunIntentTerminalResponse) => {
      const nextAttempt =
        response.session_state === "running" ||
        response.session_state === "spawning"
          ? attempt + 1
          : attempt;
      setAttempt(nextAttempt);
      setInternal({
        state: response.session_state,
        lastResponse: response,
        error:
          response.session_state === "blocked"
            ? "Клон части репозиториев не готов — спавн агента невозможен."
            : null,
        startedAt:
          response.session_state === "running" ||
          response.session_state === "spawning"
            ? { attempt: nextAttempt, sessionName: response.session_name }
            : null
      });
    },
    [attempt]
  );

  const runImpl = useCallback(
    async (mode: TerminalRunMode, restart: boolean) => {
      setIsStarting(true);
      try {
        const response = restart
          ? await restartIntentTerminal(intentId, mode)
          : await runIntentTerminal(intentId, mode);
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
    (mode: TerminalRunMode) => runImpl(mode, false),
    [runImpl]
  );
  const restart = useCallback(
    (mode: TerminalRunMode) => runImpl(mode, true),
    [runImpl]
  );

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
    start,
    restart,
    markSessionEnded
  };
}

function deriveErrorMessage(err: unknown): string {
  if (err instanceof HttpError) {
    if (err.status === 409) {
      // Server already has a live session — treat as guidance, not error.
      return "Сессия уже запущена — нажмите «Перезапустить», чтобы прервать и запустить заново.";
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
