import { useEffect, useRef, useState } from "react";

const DEBOUNCE_MS = 250;
const DEFAULT_LIMIT = 10;

export interface RefSuggestionsState<T> {
  results: T[];
  isLoading: boolean;
  error: Error | null;
}

export type RefFetcher<T> = (
  owner: string,
  repo: string,
  params: { q?: string; limit?: number },
  signal: AbortSignal
) => Promise<T[]>;

/**
 * Shared driver for the bind-modal typeahead pickers (branches, open PRs).
 *
 *  - When the modal is open and a repo is selected, an empty query immediately
 *    fetches the first page (default state — operator sees suggestions before
 *    typing).
 *  - Subsequent keystrokes are debounced (250ms) and each new fetch aborts the
 *    previous one, mirroring the repo-search hook in this slice.
 *  - Clearing the repo or closing the modal resets state.
 */
export function useRefSuggestions<T>(
  owner: string | null,
  repo: string | null,
  query: string,
  enabled: boolean,
  fetcher: RefFetcher<T>,
  limit: number = DEFAULT_LIMIT
): RefSuggestionsState<T> {
  const [results, setResults] = useState<T[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const controllerRef = useRef<AbortController | null>(null);

  useEffect(() => {
    if (!enabled || owner === null || repo === null) {
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

      fetcher(
        owner,
        repo,
        { q: trimmed.length > 0 ? trimmed : undefined, limit },
        controller.signal
      )
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
  }, [owner, repo, query, enabled, fetcher, limit]);

  useEffect(() => {
    return () => {
      controllerRef.current?.abort();
    };
  }, []);

  return { results, isLoading, error };
}
