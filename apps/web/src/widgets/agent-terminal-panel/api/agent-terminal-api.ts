import { httpGet, httpPost, terminalEndpoints } from "@/shared/api";

import type {
  AttachIntentTerminalSkillsResponse,
  IntentTerminalPreviewResponse,
  RunIntentTerminalResponse,
  TerminalRunMode,
  TerminalRunPayload
} from "../model/types";

function toRequestBody(payload: TerminalRunPayload) {
  return {
    mode: payload.launch.mode,
    vendor: payload.launch.vendor,
    model: payload.launch.model,
    effort: payload.launch.effort,
    review_binding_id: payload.reviewBindingId,
    selected_part_ids: payload.selectedPartIds,
    selected_skill_ids: payload.selectedSkillIds,
    system_prompt: payload.systemPrompt,
    user_prompt: payload.userPrompt,
    intent_text_update: payload.intentTextUpdate
  };
}

export function previewIntentTerminal(
  intentId: string,
  mode: TerminalRunMode,
  selectedPartIds: string[] | null,
  signal?: AbortSignal
): Promise<IntentTerminalPreviewResponse> {
  return httpPost<IntentTerminalPreviewResponse>(
    terminalEndpoints.previewIntentTerminal(intentId),
    { mode, selected_part_ids: selectedPartIds },
    signal
  );
}

export function runIntentTerminal(
  intentId: string,
  payload: TerminalRunPayload,
  signal?: AbortSignal
): Promise<RunIntentTerminalResponse> {
  return httpPost<RunIntentTerminalResponse>(
    terminalEndpoints.runIntentTerminal(intentId),
    toRequestBody(payload),
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
  payload: TerminalRunPayload,
  signal?: AbortSignal
): Promise<RunIntentTerminalResponse> {
  return httpPost<RunIntentTerminalResponse>(
    terminalEndpoints.restartIntentTerminal(intentId),
    toRequestBody(payload),
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

export function attachIntentTerminalSkills(
  intentId: string,
  skillIds: string[],
  signal?: AbortSignal
): Promise<AttachIntentTerminalSkillsResponse> {
  return httpPost<AttachIntentTerminalSkillsResponse>(
    terminalEndpoints.attachIntentTerminalSkills(intentId),
    { skill_ids: skillIds },
    signal
  );
}
