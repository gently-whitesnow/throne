import { httpGet, httpPost, terminalEndpoints } from "@/shared/api";

import type {
  RunIntentTerminalResponse,
  TerminalLaunchArgs
} from "../model/types";

function toRequestBody(launch: TerminalLaunchArgs) {
  return {
    mode: launch.mode,
    vendor: launch.vendor,
    model: launch.model,
    effort: launch.effort
  };
}

export function runIntentTerminal(
  intentId: string,
  launch: TerminalLaunchArgs,
  signal?: AbortSignal
): Promise<RunIntentTerminalResponse> {
  return httpPost<RunIntentTerminalResponse>(
    terminalEndpoints.runIntentTerminal(intentId),
    toRequestBody(launch),
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
  launch: TerminalLaunchArgs,
  signal?: AbortSignal
): Promise<RunIntentTerminalResponse> {
  return httpPost<RunIntentTerminalResponse>(
    terminalEndpoints.restartIntentTerminal(intentId),
    toRequestBody(launch),
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
