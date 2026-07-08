import { useCallback, useEffect, useState } from "react";

import { previewIntentTerminal } from "../api/agent-terminal-api";

import type { AvailableSessionSkill, TerminalRunMode } from "./types";

interface AttachableSkillsView {
  status: "idle" | "loading" | "ready" | "error";
  skills: AvailableSessionSkill[];
  reload: () => void;
}

export function useAttachableSkills(
  intentId: string,
  mode: TerminalRunMode,
  enabled: boolean
): AttachableSkillsView {
  const [status, setStatus] = useState<"idle" | "loading" | "ready" | "error">(
    "idle"
  );
  const [skills, setSkills] = useState<AvailableSessionSkill[]>([]);
  const [nonce, setNonce] = useState(0);

  useEffect(() => {
    if (!enabled) {
      setStatus("idle");
      return;
    }
    const abort = new AbortController();
    setStatus("loading");
    previewIntentTerminal(intentId, mode, null, abort.signal)
      .then((response) => {
        if (abort.signal.aborted) return;
        setSkills(response.available_skills_for_mode);
        setStatus("ready");
      })
      .catch(() => {
        if (abort.signal.aborted) return;
        setStatus("error");
      });
    return () => {
      abort.abort();
    };
  }, [intentId, mode, enabled, nonce]);

  const reload = useCallback(() => {
    setNonce((n) => n + 1);
  }, []);

  return { status, skills, reload };
}
