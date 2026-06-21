import type { GitPullRequestRef } from "@/entities/repository-binding";

import type { RepoSelection } from "../model/selection";
import { BindRepositoryChip } from "./BindRepositoryChip";

interface SelectedRepositoriesListProps {
  selections: RepoSelection[];
  gitlabHost: string | null;
  disabled: boolean;
  onRemove: (key: string) => void;
  onBranch: (key: string, value: string) => void;
  onPrNumber: (key: string, value: string) => void;
  onSelectedPr: (key: string, pr: GitPullRequestRef | null) => void;
}

/** Persistent list of selected repos — survives new searches (slice spec). */
export function SelectedRepositoriesList({
  selections,
  gitlabHost,
  disabled,
  onRemove,
  onBranch,
  onPrNumber,
  onSelectedPr
}: SelectedRepositoriesListProps) {
  if (selections.length === 0) return null;

  return (
    <div className="flex flex-col gap-2">
      <p className="m-0 text-xs font-semibold text-base-content/70">
        Выбрано репозиториев: {selections.length}
      </p>
      <ul
        className="m-0 flex list-none flex-col gap-2 p-0"
        data-testid="bind-repository-selected-list"
      >
        {selections.map((selection) => (
          <BindRepositoryChip
            key={selection.key}
            selection={selection}
            gitlabHost={gitlabHost}
            disabled={disabled}
            onRemove={() => {
              onRemove(selection.key);
            }}
            onBranch={(value) => {
              onBranch(selection.key, value);
            }}
            onPrNumber={(value) => {
              onPrNumber(selection.key, value);
            }}
            onSelectedPr={(pr) => {
              onSelectedPr(selection.key, pr);
            }}
          />
        ))}
      </ul>
    </div>
  );
}
