import { Search } from "lucide-react";
import { useEffect, useMemo } from "react";

import {
  gitProviderEntries,
  useGitProvidersStatus
} from "@/entities/git-provider-status";
import type { GitProvider } from "@/entities/repository-binding";

import type { SearchScope } from "../model/use-repository-search";

interface BindRepositorySearchControlsProps {
  provider: GitProvider;
  onProviderChange: (provider: GitProvider) => void;
  query: string;
  onQueryChange: (value: string) => void;
  scope: SearchScope;
  onScopeChange: (scope: SearchScope) => void;
  disabled: boolean;
}

/**
 * Search input + scope checkbox. The two modes follow parent slice decision:
 *
 *  - default (`mine`) — fast path via provider-specific "my repositories" for
 *    empty query, otherwise provider search with `scope=mine`;
 *  - `involved` — explicitly opted-in via the checkbox so the operator
 *    accepts the wider `gh api /user/repos?affiliation=...` round-trip.
 */
export function BindRepositorySearchControls({
  provider,
  onProviderChange,
  query,
  onQueryChange,
  scope,
  onScopeChange,
  disabled
}: BindRepositorySearchControlsProps) {
  // GitLab availability is detection-only: hide the option until `glab` is
  // authenticated against the configured host.
  const { status } = useGitProvidersStatus();
  const selectableProviders = useMemo(
    () =>
      status === null
        ? ["github"]
        : gitProviderEntries(status)
            .filter((entry) => entry.status.authenticated)
            .map((entry) => entry.provider),
    [status]
  );

  // Provider logout / host change while selected → fall back to the first
  // authenticated provider so search does not keep hitting an unavailable key.
  useEffect(() => {
    if (
      selectableProviders.length > 0 &&
      !selectableProviders.includes(provider)
    ) {
      onProviderChange(selectableProviders[0]);
    }
  }, [selectableProviders, provider, onProviderChange]);

  return (
    <div className="flex flex-col gap-2">
      <div className="join w-fit" role="radiogroup" aria-label="Git provider">
        {selectableProviders.map((entryProvider) => (
          <button
            key={entryProvider}
            type="button"
            className={`btn join-item btn-xs ${
              provider === entryProvider ? "btn-primary" : "btn-ghost"
            }`}
            aria-pressed={provider === entryProvider}
            onClick={() => {
              onProviderChange(entryProvider);
            }}
            disabled={disabled}
            data-testid={`bind-repository-provider-${entryProvider}`}
          >
            {providerLabel(entryProvider)}
          </button>
        ))}
      </div>
      <label className="flex items-center gap-2 rounded-md border border-base-300 bg-base-100 px-3 py-2 focus-within:border-primary">
        <Search aria-hidden size={14} className="text-base-content/50" />
        <input
          type="search"
          className="grow border-0 bg-transparent text-sm outline-none placeholder:text-base-content/40"
          placeholder="owner/repo или часть имени"
          value={query}
          onChange={(e) => {
            onQueryChange(e.target.value);
          }}
          disabled={disabled}
          aria-label="Поиск репозитория"
          autoFocus
        />
      </label>
      <label className="flex cursor-pointer items-center gap-2 text-xs text-base-content/70">
        <input
          type="checkbox"
          className="checkbox checkbox-xs"
          checked={scope === "involved"}
          onChange={(e) => {
            onScopeChange(e.target.checked ? "involved" : "mine");
          }}
          disabled={disabled}
          data-testid="bind-repository-scope-involved"
        />
        Где я участвовал (collaborator / org-member)
      </label>
    </div>
  );
}

function providerLabel(provider: string): string {
  if (provider === "github") return "GitHub";
  if (provider === "gitlab") return "GitLab";
  return provider;
}
