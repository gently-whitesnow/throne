import { Search } from "lucide-react";
import { useEffect } from "react";

import { useCapabilityEnabled } from "@/entities/capability";
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
  // GitLab is gated behind the `gitlab` capability (ADR-0032 § 8): hide the
  // option entirely until the operator enabled it and `glab` was detected.
  const gitlabEnabled = useCapabilityEnabled("gitlab");

  // Capability flipped off while GitLab was selected → fall back to GitHub so
  // the search does not keep hitting a provider the user can no longer pick.
  useEffect(() => {
    if (!gitlabEnabled && provider === "gitlab") {
      onProviderChange("github");
    }
  }, [gitlabEnabled, provider, onProviderChange]);

  return (
    <div className="flex flex-col gap-2">
      <div className="join w-fit" role="radiogroup" aria-label="Git provider">
        <button
          type="button"
          className={`btn join-item btn-xs ${
            provider === "github" ? "btn-primary" : "btn-ghost"
          }`}
          aria-pressed={provider === "github"}
          onClick={() => {
            onProviderChange("github");
          }}
          disabled={disabled}
          data-testid="bind-repository-provider-github"
        >
          GitHub
        </button>
        {gitlabEnabled ? (
          <button
            type="button"
            className={`btn join-item btn-xs ${
              provider === "gitlab" ? "btn-primary" : "btn-ghost"
            }`}
            aria-pressed={provider === "gitlab"}
            onClick={() => {
              onProviderChange("gitlab");
            }}
            disabled={disabled}
            data-testid="bind-repository-provider-gitlab"
          >
            GitLab
          </button>
        ) : null}
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
