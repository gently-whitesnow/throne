import { useEffect, useRef, useState } from "react";

import {
  listMyGithubRepositories,
  searchGithubRepositories,
  type GitRepositoryRef,
  type RepositorySearchScope
} from "@/entities/repository-binding";

const DEBOUNCE_MS = 350;
const DEFAULT_LIMIT = 50;

export interface RepositorySearchState {
  results: GitRepositoryRef[];
  isLoading: boolean;
  error: Error | null;
}

/**
 * Drives the "add repository" autocomplete (reuses the Slice 1 `gh` search).
 * `mine` + empty query lists the user's repos on open; otherwise we hit the
 * substring search. Each keystroke aborts the previous fetch.
 */
export function useRepositorySearch(
  query: string,
  scope: RepositorySearchScope,
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
