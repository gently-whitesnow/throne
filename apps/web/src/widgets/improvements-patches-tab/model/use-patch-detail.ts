import { useCallback, useEffect, useState } from "react";

import {
  type PromptPartPatchDetail,
  getPromptPartPatch
} from "@/entities/prompt-part-patch";

type LoadState =
  | { kind: "idle" }
  | { kind: "loading" }
  | { kind: "ready"; detail: PromptPartPatchDetail }
  | { kind: "error"; message: string };

export function usePatchDetail(patchId: string | null): {
  state: LoadState;
  reload: () => void;
} {
  const [state, setState] = useState<LoadState>({ kind: "idle" });
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    if (patchId === null) {
      setState({ kind: "idle" });
      return;
    }
    setState({ kind: "loading" });
    const controller = new AbortController();
    getPromptPartPatch(patchId, controller.signal)
      .then((detail) => {
        setState({ kind: "ready", detail });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        setState({
          kind: "error",
          message:
            err instanceof Error
              ? err.message
              : "Не удалось загрузить детали патча."
        });
      });
    return () => {
      controller.abort();
    };
  }, [patchId, reloadKey]);

  const reload = useCallback(() => {
    setReloadKey((v) => v + 1);
  }, []);
  return { state, reload };
}
