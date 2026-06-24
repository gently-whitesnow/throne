import { X } from "lucide-react";
import { useId, useMemo } from "react";

import {
  findGitProviderStatus,
  gitProviderEntries,
  useGitProvidersStatus
} from "@/entities/git-provider-status";
import {
  manualHostError,
  RepositoryPickerPanel,
  usePickerSelections,
  type GitProvider,
  type PickerSelection
} from "@/entities/repository-binding";
import type { TagDefaultRepository } from "@/entities/tag";
import { Button, Modal } from "@/shared/ui";

interface AddDefaultRepositoryModalProps {
  open: boolean;
  onClose: () => void;
  /**
   * Append-режим: вызывается со списком выбранных репозиториев одним подтверждением.
   * Виджет сам делает dedup относительно текущих default-репозиториев тега.
   */
  onConfirm: (picks: TagDefaultRepository[]) => void;
  /** Lock the confirm button during the parent PUT. */
  submitting?: boolean;
}

/**
 * Multi-select модалка добавления репозиториев в `Tag.default_repositories`.
 * Общий picker (`RepositoryPickerPanel`) с табами GitHub/GitLab, поиском, manual
 * SSH-вводом и chip'ами. На подтверждение отдаёт массив координат
 * `TagDefaultRepository` родителю — тот делает append + PUT с `expected_version`.
 */
export function AddDefaultRepositoryModal({
  open,
  onClose,
  onConfirm,
  submitting = false
}: AddDefaultRepositoryModalProps) {
  if (!open) return null;
  return (
    <AddDefaultRepositoryModalBody
      onClose={onClose}
      onConfirm={onConfirm}
      submitting={submitting}
    />
  );
}

function AddDefaultRepositoryModalBody({
  onClose,
  onConfirm,
  submitting
}: Omit<AddDefaultRepositoryModalProps, "open"> & { submitting: boolean }) {
  const titleId = useId();
  const { selections, toggleSearch, addManual, remove } = usePickerSelections();
  const { status: providerStatus } = useGitProvidersStatus();
  const gitlabHost =
    findGitProviderStatus(providerStatus, "gitlab")?.host ?? null;
  const selectableProviders = useMemo<readonly GitProvider[]>(
    () =>
      gitProviderEntries(providerStatus)
        .filter((entry) => entry.status.authenticated)
        .map((entry) => entry.provider),
    [providerStatus]
  );

  const validCount = selections.filter(
    (s) => selectionIssue(s, gitlabHost) === null
  ).length;
  const canSubmit = !submitting && validCount > 0;

  function handleSubmit() {
    const picks = selections
      .filter((s) => selectionIssue(s, gitlabHost) === null)
      .map(toDefaultRepository);
    onConfirm(picks);
  }

  return (
    <Modal
      onClose={submitting ? () => undefined : onClose}
      labelledBy={titleId}
      boxClassName="max-h-[min(960px,calc(100vh-32px))] w-full max-w-2xl"
    >
      <div className="mb-4 flex items-start justify-between gap-4">
        <div className="flex flex-col gap-1">
          <p className="m-0 text-xs font-bold uppercase tracking-wider text-primary">
            Default repository
          </p>
          <h3 id={titleId} className="m-0 text-lg font-semibold leading-tight">
            Выберите репозитории
          </h3>
          <p className="m-0 text-xs text-base-content/60">
            Будут добавлены в default-список тега. На Run pre-flight'е каждый
            интент с этим тегом получит binding к этим репозиториям.
          </p>
        </div>
        <button
          type="button"
          className="btn btn-sm btn-circle btn-ghost"
          onClick={onClose}
          disabled={submitting}
          aria-label="Закрыть"
        >
          <X aria-hidden size={16} strokeWidth={2} />
        </button>
      </div>

      <form
        onSubmit={(e) => {
          e.preventDefault();
          handleSubmit();
        }}
        className="flex flex-col gap-4"
      >
        <RepositoryPickerPanel
          selections={selections}
          disabled={submitting}
          selectableProviders={selectableProviders}
          gitlabHost={gitlabHost}
          onToggleSearch={toggleSearch}
          onAddManual={(ref) => {
            const result = addManual(ref);
            return result.ok ? null : (result.reason ?? null);
          }}
          onRemove={remove}
        />

        <div className="flex justify-end gap-2">
          <Button type="button" onClick={onClose} disabled={submitting}>
            Отмена
          </Button>
          <Button
            type="submit"
            variant="primary"
            disabled={!canSubmit}
            data-testid="tag-default-repositories-submit"
          >
            {submitting
              ? "Сохраняем…"
              : validCount > 0
                ? `Добавить (${String(validCount)})`
                : "Добавить"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

function selectionIssue(
  selection: PickerSelection,
  gitlabHost: string | null
): string | null {
  if (selection.source === "manual") {
    return manualHostError(selection.ref, gitlabHost);
  }
  return null;
}

function toDefaultRepository(selection: PickerSelection): TagDefaultRepository {
  const { ref } = selection;
  return {
    provider: ref.provider,
    host: ref.host,
    owner: ref.owner,
    repo: ref.repo,
    project_id: ref.project_id,
    default_branch: ref.default_branch.length > 0 ? ref.default_branch : null
  };
}
