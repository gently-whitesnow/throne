import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import { previewIntentTerminal } from "../api/agent-terminal-api";

import type {
  AvailableSessionSkill,
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
  skills: AvailableSessionSkill[];
  systemPrompt: string;
  body: string;
  freeInput: string;
  saveIntentText: boolean;
  bodyDirty: boolean;
  setPartText: (partId: string, text: string) => void;
  setBody: (value: string) => void;
  setFreeInput: (value: string) => void;
  setSaveIntentText: (value: boolean) => void;
  togglePart: (partId: string) => void;
  toggleSkill: (skillId: string) => void;
  buildPayload: (
    launch: TerminalLaunchArgs,
    reviewBindingId: string | null
  ) => TerminalRunPayload;
}

const MANDATORY = "mandatory";

function selectedOptionalIds(parts: PromptPartPreview[]): string[] {
  return parts
    .filter((p) => p.role !== MANDATORY && p.selected)
    .map((p) => p.part_id);
}

function selectedSkillIds(skills: AvailableSessionSkill[]): string[] {
  return skills
    .filter((s) => s.materializable && s.selected)
    .map((s) => s.skill_id);
}

/**
 * Собирает system-блок ровно как backend (PromptCompositionResolver): включённые
 * части в порядке бандла, обрезанные и склеенные через пустую строку. Делаем это
 * на клиенте, чтобы итог пересобирался живьём на правку рамки или тумблер части,
 * не затирая сессионные правки соседних частей перезапросом preview.
 */
function assembleSystemPrompt(parts: PromptPartPreview[]): string {
  return parts
    .filter((p) => p.selected)
    .map((p) => p.text.trim())
    .filter((t) => t.length > 0)
    .join("\n\n");
}

function composeUserPrompt(body: string, freeInput: string): string {
  const extra = freeInput.trim();
  return extra.length > 0 ? `${body}\n\n${extra}` : body;
}

/**
 * Управляет состоянием preflight-модалки: один раз тянет backend-preview, затем
 * держит выбор и редактируемый текст частей локально (session-only override) и
 * зону задачи. Тумблер и правка рамки меняют только клиентское состояние —
 * system-блок пересобирается из текущих частей (ADR-0036), backend получает
 * собранный system_prompt verbatim при запуске (ADR-0030/0035).
 */
export function usePreflightPreview(
  intentId: string,
  mode: TerminalRunMode,
  open: boolean
): PreflightPreview {
  const [status, setStatus] = useState<PreviewStatus>("idle");
  const [error, setError] = useState<string | null>(null);
  const [parts, setParts] = useState<PromptPartPreview[]>([]);
  const [skills, setSkills] = useState<AvailableSessionSkill[]>([]);
  const [body, setBodyState] = useState("");
  const [freeInput, setFreeInput] = useState("");
  const [intentVersion, setIntentVersion] = useState(0);
  const [saveIntentText, setSaveIntentTextState] = useState(false);

  const originalBodyRef = useRef("");
  const saveTouchedRef = useRef(false);

  const applyResponse = useCallback(
    (response: IntentTerminalPreviewResponse) => {
      setParts(response.parts);
      setSkills(response.available_skills_for_mode);
      originalBodyRef.current = response.user_prompt;
      saveTouchedRef.current = false;
      setBodyState(response.user_prompt);
      setFreeInput("");
      setIntentVersion(response.intent_version);
      setSaveIntentTextState(false);
    },
    []
  );

  useEffect(() => {
    if (!open) {
      setStatus("idle");
      return;
    }
    const abort = new AbortController();
    setStatus("loading");
    setError(null);
    previewIntentTerminal(intentId, mode, null, abort.signal)
      .then((response) => {
        if (abort.signal.aborted) return;
        applyResponse(response);
        setStatus("ready");
      })
      .catch(() => {
        if (abort.signal.aborted) return;
        setStatus("error");
        setError("Не удалось собрать предпросмотр запуска.");
      });
    return () => {
      abort.abort();
    };
  }, [open, intentId, mode, applyResponse]);

  const togglePart = useCallback((partId: string) => {
    setParts((prev) =>
      prev.map((p) =>
        p.part_id === partId && p.role !== MANDATORY
          ? { ...p, selected: !p.selected }
          : p
      )
    );
  }, []);

  const setPartText = useCallback((partId: string, text: string) => {
    setParts((prev) =>
      prev.map((p) => (p.part_id === partId ? { ...p, text } : p))
    );
  }, []);

  const toggleSkill = useCallback((skillId: string) => {
    setSkills((prev) =>
      prev.map((skill) =>
        skill.skill_id === skillId && skill.materializable
          ? { ...skill, selected: !skill.selected }
          : skill
      )
    );
  }, []);

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

  const systemPrompt = useMemo(() => assembleSystemPrompt(parts), [parts]);

  const buildPayload = useCallback(
    (
      launch: TerminalLaunchArgs,
      reviewBindingId: string | null
    ): TerminalRunPayload => {
      const bodyDirty = body !== originalBodyRef.current;
      return {
        launch,
        reviewBindingId,
        selectedPartIds: selectedOptionalIds(parts),
        selectedSkillIds: selectedSkillIds(skills),
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
    [
      parts,
      skills,
      systemPrompt,
      body,
      freeInput,
      saveIntentText,
      intentVersion
    ]
  );

  return {
    status,
    error,
    parts,
    skills,
    systemPrompt,
    body,
    freeInput,
    saveIntentText,
    bodyDirty: body !== originalBodyRef.current,
    setPartText,
    setBody,
    setFreeInput,
    setSaveIntentText,
    togglePart,
    toggleSkill,
    buildPayload
  };
}
