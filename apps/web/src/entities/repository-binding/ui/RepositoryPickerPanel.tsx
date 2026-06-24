import { useState, type ReactNode } from "react";

import type { PickerSelection } from "../model/picker-types";
import { refKey } from "../model/repo-key";
import type { GitProvider, GitRepositoryRef } from "../model/types";
import {
  useRepositorySearch,
  type SearchScope
} from "../model/use-repository-search";
import { ManualSshUrlInput } from "./ManualSshUrlInput";
import { RepositorySearchControls } from "./RepositorySearchControls";
import { RepositorySearchList } from "./RepositorySearchList";
import { SimpleRepositoryChip } from "./SimpleRepositoryChip";

interface RepositoryPickerPanelProps<TSelection extends PickerSelection> {
  selections: TSelection[];
  disabled: boolean;
  /**
   * Authenticated providers, в порядке отображения табов. Параметр приходит от
   * потребителя, чтобы entity-слой `repository-binding` не зависел от
   * `git-provider-status` (FSD: запрет cross-slice импортов на одном слое).
   */
  selectableProviders: readonly GitProvider[];
  /** Hostname GitLab self-hosted из `git-providers/status` (для валидации manual SSH). */
  gitlabHost: string | null;
  onToggleSearch: (ref: GitRepositoryRef) => void;
  /** Returns an error message to display inline; `null` on success. */
  onAddManual: (ref: GitRepositoryRef) => string | null;
  onRemove: (key: string) => void;
  /** Per-selection renderer; defaults to a minimal chip with delete button. */
  renderSelection?: (selection: TSelection) => ReactNode;
  /** Label shown above the chip list. */
  selectedTitle?: (count: number) => string;
}

const DEFAULT_PROVIDER: GitProvider = "github";

/**
 * Search controls + result list + manual SSH paste + chips of выбранных
 * репозиториев. Полностью controlled: владение `selections` остаётся за
 * потребителем — каждый сам решает, какие дополнительные поля чипу нужны
 * (intent: branch/PR; tag: ничего).
 */
export function RepositoryPickerPanel<TSelection extends PickerSelection>({
  selections,
  disabled,
  selectableProviders,
  gitlabHost,
  onToggleSearch,
  onAddManual,
  onRemove,
  renderSelection,
  selectedTitle
}: RepositoryPickerPanelProps<TSelection>) {
  const [query, setQuery] = useState("");
  const [provider, setProvider] = useState<GitProvider>(
    selectableProviders[0] ?? DEFAULT_PROVIDER
  );
  const [scope, setScope] = useState<SearchScope>("mine");

  const {
    results,
    isLoading,
    error: searchError
  } = useRepositorySearch(query, scope, true, provider);

  const selectedKeys = new Set(selections.map((s) => s.key));

  function changeProvider(nextProvider: GitProvider) {
    setProvider(nextProvider);
    setQuery("");
  }

  return (
    <div className="flex flex-col gap-4">
      <RepositorySearchControls
        provider={provider}
        selectableProviders={selectableProviders}
        onProviderChange={changeProvider}
        query={query}
        onQueryChange={setQuery}
        scope={scope}
        onScopeChange={setScope}
        disabled={disabled}
      />
      <RepositorySearchList
        results={results}
        isLoading={isLoading}
        error={searchError}
        isSelected={(repo) => selectedKeys.has(refKey(repo))}
        onSelect={onToggleSearch}
      />
      <ManualSshUrlInput disabled={disabled} onAdd={onAddManual} />
      {selections.length > 0 ? (
        <div className="flex flex-col gap-2">
          <p className="m-0 text-xs font-semibold text-base-content/70">
            {(selectedTitle ?? defaultSelectedTitle)(selections.length)}
          </p>
          <ul
            className="m-0 flex list-none flex-col gap-2 p-0"
            data-testid="repository-picker-selected-list"
          >
            {selections.map((selection) => (
              <li key={selection.key} className="m-0 p-0">
                {renderSelection !== undefined ? (
                  renderSelection(selection)
                ) : (
                  <SimpleRepositoryChip
                    selection={selection}
                    gitlabHost={gitlabHost}
                    disabled={disabled}
                    onRemove={() => {
                      onRemove(selection.key);
                    }}
                  />
                )}
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  );
}

function defaultSelectedTitle(count: number): string {
  return `Выбрано репозиториев: ${String(count)}`;
}
