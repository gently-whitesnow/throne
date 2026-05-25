import { X } from "lucide-react";
import { useEffect, useId, useState } from "react";
import { createPortal } from "react-dom";

import {
  bindIntentRepository,
  type GitRepositoryRef,
  type RepositoryBinding
} from "@/entities/repository-binding";
import { HttpError } from "@/shared/api";
import { Button } from "@/shared/ui";

import { parsePrNumber } from "../model/pr-number";
import {
  useRepositorySearch,
  type SearchScope
} from "../model/use-repository-search";
import { BindRepositorySearchControls } from "./BindRepositorySearchControls";
import {
  BindRepositorySelectionForm,
  type BindRepositoryFormState
} from "./BindRepositorySelectionForm";
import { RepositorySearchList } from "./RepositorySearchList";

interface BindRepositoryModalProps {
  intentId: string;
  open: boolean;
  onClose: () => void;
  onBound?: (binding: RepositoryBinding) => void;
}

const EMPTY_FORM: BindRepositoryFormState = {
  branch: "",
  prNumber: "",
  selectedPr: null
};

/**
 * Modal for `POST /intents/{id}/repositories`.
 *
 *  - Search: «Только мои» (default) + checkbox «Где я участвовал»
 *    (`scope=involved`). See parent slice decision on the two scopes.
 *  - Selection: clicking a row pre-fills `branch` with the upstream default
 *    branch — operator may override before submit. `pull_request_number` is
 *    optional; empty input means "don't track a PR" and the server treats
 *    the binding as repo-only.
 *  - 409 → human-readable "репозиторий уже привязан" message. On success the
 *    modal closes immediately; the realtime `intent.repository_bound` event
 *    (handled by `useIntentRepositories`) inserts the row.
 */
export function BindRepositoryModal({
  intentId,
  open,
  onClose,
  onBound
}: BindRepositoryModalProps) {
  const [query, setQuery] = useState("");
  const [scope, setScope] = useState<SearchScope>("mine");
  const [selected, setSelected] = useState<GitRepositoryRef | null>(null);
  const [form, setForm] = useState<BindRepositoryFormState>(EMPTY_FORM);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const titleId = useId();

  const {
    results,
    isLoading,
    error: searchError
  } = useRepositorySearch(query, scope, open);

  // Reset on close so the next open is a clean slate.
  useEffect(() => {
    if (open) return;
    setQuery("");
    setScope("mine");
    setSelected(null);
    setForm(EMPTY_FORM);
    setBusy(false);
    setError(null);
  }, [open]);

  // Escape closes — matches CreateIntentButton convention.
  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === "Escape" && !busy) {
        e.preventDefault();
        onClose();
      }
    };
    window.addEventListener("keydown", handler);
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      window.removeEventListener("keydown", handler);
      document.body.style.overflow = previousOverflow;
    };
  }, [open, busy, onClose]);

  const prNumberParsed = parsePrNumber(form.prNumber);
  const canSubmit =
    selected !== null &&
    form.branch.trim().length > 0 &&
    prNumberParsed.kind !== "invalid" &&
    !busy;

  function pickRepository(repo: GitRepositoryRef) {
    setSelected(repo);
    // Switching repos resets PR + branch — the previous values are no longer
    // meaningful for the new repository. Branch defaults to upstream default.
    setForm({
      branch: repo.default_branch,
      prNumber: "",
      selectedPr: null
    });
  }

  async function handleSubmit() {
    if (selected === null) return;
    if (prNumberParsed.kind === "invalid") return;
    setBusy(true);
    setError(null);
    try {
      const binding = await bindIntentRepository(intentId, {
        provider: selected.provider,
        owner: selected.owner,
        repo: selected.repo,
        default_branch: form.branch.trim(),
        pull_request_number:
          prNumberParsed.kind === "value" ? prNumberParsed.value : undefined
      });
      onBound?.(binding);
      onClose();
    } catch (err: unknown) {
      setError(formatBindError(err));
      setBusy(false);
    }
  }

  if (!open) return null;

  return createPortal(
    <div
      className="modal modal-open modal-bottom sm:modal-middle"
      role="presentation"
      onClick={() => {
        if (!busy) onClose();
      }}
    >
      <div
        className="modal-box max-h-[min(960px,calc(100vh-32px))] w-full max-w-2xl border border-base-300 bg-base-100"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onClick={(e) => {
          e.stopPropagation();
        }}
      >
        <div className="mb-4 flex items-start justify-between gap-4">
          <div className="flex flex-col gap-1">
            <p className="m-0 text-xs font-bold uppercase tracking-wider text-primary">
              Привязать репозиторий
            </p>
            <h3
              id={titleId}
              className="m-0 text-lg font-semibold leading-tight"
            >
              Выберите репозиторий и опционально укажите PR
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
          <BindRepositorySearchControls
            query={query}
            onQueryChange={setQuery}
            scope={scope}
            onScopeChange={setScope}
            disabled={busy}
          />

          <RepositorySearchList
            results={results}
            isLoading={isLoading}
            error={searchError}
            selectedFullName={selected?.full_name ?? null}
            onSelect={pickRepository}
          />

          <BindRepositorySelectionForm
            selected={selected}
            form={form}
            onChange={setForm}
            disabled={busy}
          />

          {error ? (
            <p
              role="alert"
              className="m-0 text-sm text-error"
              data-testid="bind-repository-error"
            >
              {error}
            </p>
          ) : null}

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
              {busy ? "Привязываем…" : "Привязать"}
            </Button>
          </div>
        </form>
      </div>
    </div>,
    document.body
  );
}

function formatBindError(err: unknown): string {
  if (err instanceof HttpError) {
    if (err.status === 409) {
      return "Этот репозиторий уже привязан к интенту. Чтобы изменить PR — сначала отвяжите.";
    }
    if (err.status === 422) {
      return "Не удалось привязать: сервер отклонил параметры (422). Проверьте провайдера и owner/repo.";
    }
    if (err.status === 404) {
      return "Интент не найден (404).";
    }
    return `Не удалось привязать (${String(err.status)}).`;
  }
  return "Не удалось привязать репозиторий.";
}
