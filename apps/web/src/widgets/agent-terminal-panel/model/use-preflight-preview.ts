import { useCallback, useEffect, useRef, useState } from "react";

import { previewIntentTerminal } from "../api/agent-terminal-api";

import type {
  IntentTerminalPreviewResponse,
  PromptPartPreview,
  TerminalLaunchArgs,
  TerminalRunMode,
  TerminalRunPayload
} from "./types";

type PreviewStatus = "idle" | "loading" | "ready" | "error";

export interface PreflightPreview {
  status: PreviewStatus;
  error: string | null;
  parts: PromptPartPreview[];
  systemPrompt: string;
  body: string;
  freeInput: string;
  saveIntentText: boolean;
  bodyDirty: boolean;
  setSystemPrompt: (value: string) => void;
  setBody: (value: string) => void;
  setFreeInput: (value: string) => void;
  setSaveIntentText: (value: boolean) => void;
  togglePart: (partId: string) => void;
  buildPayload: (launch: TerminalLaunchArgs) => TerminalRunPayload;
}

const MANDATORY = "mandatory";

function selectedOptionalIds(parts: PromptPartPreview[]): string[] {
  return parts
    .filter((p) => p.role !== MANDATORY && p.selected)
    .map((p) => p.part_id);
}

function composeUserPrompt(body: string, freeInput: string): string {
  const extra = freeInput.trim();
  return extra.length > 0 ? `${body}\n\n${extra}` : body;
}

/**
 * Управляет состоянием preflight-модалки: тянет backend-preview, держит выбор
 * частей, редактируемый system-блок (session-only override) и зону задачи. На
 * переключение части перезапрашивает preview — пересборку system_prompt делает
 * backend (ADR-0030/0035), фронт лишь показывает и точечно правит итог.
 */
export function usePreflightPreview(
  intentId: string,
  mode: TerminalRunMode,
  open: boolean
): PreflightPreview {
  const [status, setStatus] = useState<PreviewStatus>("idle");
  const [error, setError] = useState<string | null>(null);
  const [parts, setParts] = useState<PromptPartPreview[]>([]);
  const [systemPrompt, setSystemPrompt] = useState("");
  const [body, setBodyState] = useState("");
  const [freeInput, setFreeInput] = useState("");
  const [intentVersion, setIntentVersion] = useState(0);
  const [saveIntentText, setSaveIntentTextState] = useState(false);

  const originalBodyRef = useRef("");
  const saveTouchedRef = useRef(false);

  const applyResponse = useCallback(
    (response: IntentTerminalPreviewResponse, resetTask: boolean) => {
      setParts(response.parts);
      setSystemPrompt(response.system_prompt);
      if (resetTask) {
        originalBodyRef.current = response.user_prompt;
        saveTouchedRef.current = false;
        setBodyState(response.user_prompt);
        setFreeInput("");
        setIntentVersion(response.intent_version);
        setSaveIntentTextState(false);
      }
    },
    []
  );

  const fetchPreview = useCallback(
    async (
      selectedIds: string[] | null,
      resetTask: boolean,
      signal?: AbortSignal
    ) => {
      setStatus("loading");
      setError(null);
      try {
        const response = await previewIntentTerminal(
          intentId,
          mode,
          selectedIds,
          signal
        );
        if (signal?.aborted) return;
        applyResponse(response, resetTask);
        setStatus("ready");
      } catch {
        if (signal?.aborted) return;
        setStatus("error");
        setError("Не удалось собрать предпросмотр запуска.");
      }
    },
    [intentId, mode, applyResponse]
  );

  useEffect(() => {
    if (!open) {
      setStatus("idle");
      return;
    }
    const abort = new AbortController();
    void fetchPreview(null, true, abort.signal);
    return () => {
      abort.abort();
    };
  }, [open, fetchPreview]);

  const togglePart = useCallback(
    (partId: string) => {
      const current = new Set(selectedOptionalIds(parts));
      if (current.has(partId)) current.delete(partId);
      else current.add(partId);
      void fetchPreview([...current], false);
    },
    [parts, fetchPreview]
  );

  const setBody = useCallback((value: string) => {
    setBodyState(value);
    if (!saveTouchedRef.current) {
      setSaveIntentTextState(value !== originalBodyRef.current);
    }
  }, []);

  const setSaveIntentText = useCallback((value: boolean) => {
    saveTouchedRef.current = true;
    setSaveIntentTextState(value);
  }, []);

  const buildPayload = useCallback(
    (launch: TerminalLaunchArgs): TerminalRunPayload => {
      const bodyDirty = body !== originalBodyRef.current;
      return {
        launch,
        selectedPartIds: selectedOptionalIds(parts),
        systemPrompt,
        userPrompt: composeUserPrompt(body, freeInput),
        intentTextUpdate:
          saveIntentText && bodyDirty
            ? {
                expected_version: intentVersion,
                old_text: originalBodyRef.current,
                new_text: body
              }
            : null
      };
    },
    [parts, systemPrompt, body, freeInput, saveIntentText, intentVersion]
  );

  return {
    status,
    error,
    parts,
    systemPrompt,
    body,
    freeInput,
    saveIntentText,
    bodyDirty: body !== originalBodyRef.current,
    setSystemPrompt,
    setBody,
    setFreeInput,
    setSaveIntentText,
    togglePart,
    buildPayload
  };
}
