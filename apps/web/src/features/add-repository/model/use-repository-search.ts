import { useEffect, useRef, useState } from "react";

import {
  listGitProviderRepositories,
  searchGitProviderRepositories,
  type GitProvider,
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
 * Drives the "add repository" autocomplete (reuses the provider search).
 * `mine` + empty query lists the user's repos on open; otherwise we hit the
 * substring search. Each keystroke aborts the previous fetch.
 */
export function useRepositorySearch(
  query: string,
  scope: RepositorySearchScope,
  enabled: boolean,
  provider: GitProvider
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
          ? listGitProviderRepositories(
              provider,
              DEFAULT_LIMIT,
              controller.signal
            )
          : searchGitProviderRepositories(
              {
                provider,
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
  }, [query, scope, enabled, provider]);

  useEffect(() => {
    return () => {
      controllerRef.current?.abort();
    };
  }, []);

  return { results, isLoading, error };
}
