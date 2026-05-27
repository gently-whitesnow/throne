import {
  type DreamSource,
  useDreamSourcesList
} from "@/entities/dream-session";

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; items: DreamSource[] }
  | { kind: "error"; message: string };

export function useDreamSources(): { state: LoadState } {
  const query = useDreamSourcesList();

  const state: LoadState = query.isPending
    ? { kind: "loading" }
    : query.error
      ? { kind: "error", message: query.error.message }
      : { kind: "ready", items: query.data.items };

  return { state };
}
