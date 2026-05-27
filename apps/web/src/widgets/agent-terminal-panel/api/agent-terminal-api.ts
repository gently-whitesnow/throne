import { httpGet, httpPost, terminalEndpoints } from "@/shared/api";
import { capabilitiesEndpoints } from "@/shared/api";

import type {
  CapabilityDto,
  RunIntentTerminalResponse,
  TerminalRunMode
} from "../model/types";

export function listCapabilities(
  signal?: AbortSignal
): Promise<CapabilityDto[]> {
  return httpGet<CapabilityDto[]>(
    capabilitiesEndpoints.listCapabilities(),
    signal
  );
}

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
