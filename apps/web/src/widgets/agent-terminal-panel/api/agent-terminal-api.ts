import { httpGet, httpPost, terminalEndpoints } from "@/shared/api";

import type {
  RunIntentTerminalResponse,
  TerminalRunMode
} from "../model/types";

export function runIntentTerminal(
  intentId: string,
  mode: TerminalRunMode,
  signal?: AbortSignal
): Promise<RunIntentTerminalResponse> {
  return httpPost<RunIntentTerminalResponse>(
    terminalEndpoints.runIntentTerminal(intentId),
    { mode },
    signal
  );
}

export function getIntentTerminalSession(
  intentId: string,
  signal?: AbortSignal
): Promise<RunIntentTerminalResponse> {
  return httpGet<RunIntentTerminalResponse>(
    terminalEndpoints.getIntentTerminalSession(intentId),
    signal
  );
}

export function restartIntentTerminal(
  intentId: string,
  mode: TerminalRunMode,
  signal?: AbortSignal
): Promise<RunIntentTerminalResponse> {
  return httpPost<RunIntentTerminalResponse>(
    terminalEndpoints.restartIntentTerminal(intentId),
    { mode },
    signal
  );
}

export function killIntentTerminal(
  intentId: string,
  signal?: AbortSignal
): Promise<RunIntentTerminalResponse> {
  return httpPost<RunIntentTerminalResponse>(
    terminalEndpoints.killIntentTerminal(intentId),
    undefined,
    signal
  );
}
