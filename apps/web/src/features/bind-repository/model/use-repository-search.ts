import { useEffect, useRef, useState } from "react";

import {
  listMyGithubRepositories,
  searchGithubRepositories,
  type GitRepositoryRef
} from "@/entities/repository-binding";

/** UX wants typing to feel reactive but not hammer `gh` on every keystroke. */
const DEBOUNCE_MS = 350;
/** Default result cap — `gh repo list --json` is paginated but for the picker
 *  we don't want a wall of rows. Matches the limit used in /settings. */
const DEFAULT_LIMIT = 50;

export type SearchScope = "mine" | "involved";

export interface RepositorySearchState {
  results: GitRepositoryRef[];
  isLoading: boolean;
  error: Error | null;
}

/**
 * Drives the two-mode picker.
 *
 *  - `mine` (default) + empty query — calls `listMyGithubRepositories` so the
 *    user sees something useful on open without typing.
 *  - `mine` + non-empty query — `searchGithubRepositories` with `scope=mine`
 *    so the server applies its client-side substring filter consistently.
 *  - `involved` — always `searchGithubRepositories` with `scope=involved`,
 *    even for empty query (matches the parent slice decision: the checkbox
 *    explicitly asks the user to widen scope, so we fetch right away).
 *
 * Each search aborts the previous fetch — keeps the UI honest while the
 * operator keeps typing.
 */
export function useRepositorySearch(
  query: string,
  scope: SearchScope,
  enabled: boolean
): RepositorySearchState {
  const [results, setResults] = useState<GitRepositoryRef[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const controllerRef = useRef<AbortController | null>(null);

  useEffect(() => {
    if (!enabled) {
      controllerRef.current?.abort();
      controllerRef.current = null;
      setResults([]);
      setIsLoading(false);
      setError(null);
      return;
    }

    const trimmed = query.trim();
    const timer = window.setTimeout(() => {
      controllerRef.current?.abort();
      const controller = new AbortController();
      controllerRef.current = controller;
      setIsLoading(true);
      setError(null);

      const promise =
        scope === "mine" && trimmed.length === 0
          ? listMyGithubRepositories(DEFAULT_LIMIT, controller.signal)
          : searchGithubRepositories(
              {
                q: trimmed.length > 0 ? trimmed : undefined,
                scope,
                limit: DEFAULT_LIMIT
              },
              controller.signal
            );

      promise
        .then((list) => {
          if (controller.signal.aborted) return;
          setResults(list);
          setIsLoading(false);
        })
        .catch((err: unknown) => {
          if (controller.signal.aborted) return;
          setError(err instanceof Error ? err : new Error(String(err)));
          setIsLoading(false);
        });
    }, DEBOUNCE_MS);

    return () => {
      window.clearTimeout(timer);
    };
  }, [query, scope, enabled]);

  useEffect(() => {
    return () => {
      controllerRef.current?.abort();
    };
  }, []);

  return { results, isLoading, error };
}
