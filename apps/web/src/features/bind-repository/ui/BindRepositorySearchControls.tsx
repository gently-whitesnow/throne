import { Search } from "lucide-react";

import type { SearchScope } from "../model/use-repository-search";

interface BindRepositorySearchControlsProps {
  query: string;
  onQueryChange: (value: string) => void;
  scope: SearchScope;
  onScopeChange: (scope: SearchScope) => void;
  disabled: boolean;
}

/**
 * Search input + scope checkbox. The two modes follow parent slice decision:
 *
 *  - default (`mine`) — fast path via `listMyGithubRepositories` for empty
 *    query, otherwise `searchGithubRepositories?scope=mine`;
 *  - `involved` — explicitly opted-in via the checkbox so the operator
 *    accepts the wider `gh api /user/repos?affiliation=...` round-trip.
 */
export function BindRepositorySearchControls({
  query,
  onQueryChange,
  scope,
  onScopeChange,
  disabled
}: BindRepositorySearchControlsProps) {
  return (
    <div className="flex flex-col gap-2">
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
