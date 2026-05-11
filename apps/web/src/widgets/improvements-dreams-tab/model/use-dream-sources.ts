import { useEffect, useState } from "react";

import {
  type DreamSource,
  type DreamSourcePage,
  listDreamSources
} from "@/entities/dream-session";

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; items: DreamSource[] }
  | { kind: "error"; message: string };

export function useDreamSources(): { state: LoadState } {
  const [state, setState] = useState<LoadState>({ kind: "loading" });

  useEffect(() => {
    const controller = new AbortController();
    listDreamSources(controller.signal)
      .then((page: DreamSourcePage) => {
        setState({ kind: "ready", items: page.items });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        setState({
          kind: "error",
          message:
            err instanceof Error
              ? err.message
              : "Не удалось загрузить dream sources."
        });
      });
    return () => {
      controller.abort();
    };
  }, []);

  return { state };
}
