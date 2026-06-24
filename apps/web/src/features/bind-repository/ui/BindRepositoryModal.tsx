import { X } from "lucide-react";
import { useId, useMemo } from "react";

import {
  findGitProviderStatus,
  gitProviderEntries,
  useGitProvidersStatus
} from "@/entities/git-provider-status";
import {
  RepositoryPickerPanel,
  type GitProvider,
  type RepositoryBinding
} from "@/entities/repository-binding";
import { Button, Modal } from "@/shared/ui";

import { selectionIssue } from "../model/selection";
import { useBindSelections } from "../model/use-bind-selections";
import { BindRepositoryChip } from "./BindRepositoryChip";

interface BindRepositoryModalProps {
  intentId: string;
  open: boolean;
  onClose: () => void;
  onBound?: (binding: RepositoryBinding) => void;
}

/**
 * Multi-select picker for `POST /intents/{id}/repositories`.
 *
 *  - Search a repo, click to add it as a chip; previously-added chips persist
 *    across new searches. Each chip keeps its own branch + optional PR.
 *  - SSH-URL paste adds a repo the autocomplete cannot find (no membership but
 *    git access). It is bound through the same `gh`/`glab` path — host/provider
 *    compatibility is validated on the chip (the GitLab clone uses the
 *    configured host, so a mismatch would otherwise fail silently in clone).
 *  - One submit fires N independent binds in parallel. Succeeded chips drop;
 *    failed ones keep their error so the rest are not blocked. Rows appear via
 *    the realtime `intent.repository_bound` event, same as the single-bind flow.
 */
export function BindRepositoryModal({
  intentId,
  open,
  onClose,
  onBound
}: BindRepositoryModalProps) {
  // Mount the body only while open: unmounting on close resets all state for a
  // clean next open and avoids the `git-providers/status` probe (gh/glab auth)
  // firing on every intent page that renders the trigger button.
  if (!open) return null;
  return (
    <BindRepositoryModalBody
      intentId={intentId}
      onClose={onClose}
      onBound={onBound}
    />
  );
}

function BindRepositoryModalBody({
  intentId,
  onClose,
  onBound
}: Omit<BindRepositoryModalProps, "open">) {
  const titleId = useId();

  const {
    selections,
    busy,
    toggleSearch,
    addManual,
    remove,
    setBranch,
    setPrNumber,
    setSelectedPr,
    submit
  } = useBindSelections(intentId);

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

  const closeIfIdle = () => {
    if (!busy) onClose();
  };

  const bindableCount = selections.filter(
    (s) => selectionIssue(s, gitlabHost) === null
  ).length;
  const canSubmit = !busy && bindableCount > 0;

  async function handleSubmit() {
    const done = await submit(gitlabHost, onBound);
    if (done) onClose();
  }

  return (
    <Modal
      onClose={closeIfIdle}
      labelledBy={titleId}
      boxClassName="max-h-[min(960px,calc(100vh-32px))] w-full max-w-2xl"
    >
      <div className="mb-4 flex items-start justify-between gap-4">
        <div className="flex flex-col gap-1">
          <p className="m-0 text-xs font-bold uppercase tracking-wider text-primary">
            Привязать репозитории
          </p>
          <h3 id={titleId} className="m-0 text-lg font-semibold leading-tight">
            Выберите несколько репозиториев и привяжите разом
          </h3>
        </div>
        <button
          type="button"
          className="btn btn-sm btn-circle btn-ghost"
          onClick={onClose}
          disabled={busy}
          aria-label="Закрыть"
        >
          <X aria-hidden size={16} strokeWidth={2} />
        </button>
      </div>

      <form
        onSubmit={(e) => {
          e.preventDefault();
          void handleSubmit();
        }}
        className="flex flex-col gap-4"
      >
        <RepositoryPickerPanel
          selections={selections}
          disabled={busy}
          selectableProviders={selectableProviders}
          gitlabHost={gitlabHost}
          onToggleSearch={toggleSearch}
          onAddManual={(ref) => {
            const result = addManual(ref);
            return result.ok ? null : (result.reason ?? null);
          }}
          onRemove={remove}
          renderSelection={(selection) => (
            <BindRepositoryChip
              selection={selection}
              gitlabHost={gitlabHost}
              disabled={busy}
              onRemove={() => {
                remove(selection.key);
              }}
              onBranch={(value) => {
                setBranch(selection.key, value);
              }}
              onPrNumber={(value) => {
                setPrNumber(selection.key, value);
              }}
              onSelectedPr={(pr) => {
                setSelectedPr(selection.key, pr);
              }}
            />
          )}
        />

        <div className="flex justify-end gap-2">
          <Button type="button" onClick={onClose} disabled={busy}>
            Отмена
          </Button>
          <Button
            type="submit"
            variant="primary"
            disabled={!canSubmit}
            data-testid="bind-repository-submit"
          >
            {busy
              ? "Привязываем…"
              : bindableCount > 0
                ? `Привязать (${String(bindableCount)})`
                : "Привязать"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
