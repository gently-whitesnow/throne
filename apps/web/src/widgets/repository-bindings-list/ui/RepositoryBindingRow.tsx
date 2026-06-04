import {
  AlertTriangle,
  GitBranch,
  GitPullRequest,
  Loader2,
  RefreshCw,
  Trash2
} from "lucide-react";
import { useState } from "react";

import {
  cloneStatusMeta,
  hasPullRequest,
  isCloneTransient,
  pullRequestStateMeta,
  repositoryFullName,
  unbindIntentRepository,
  type RepositoryBinding
} from "@/entities/repository-binding";
import { OpenBindingInVscodeButton } from "@/features/open-in-vscode";
import { HttpError } from "@/shared/api";
import { Button } from "@/shared/ui";

interface RepositoryBindingRowProps {
  intentId: string;
  binding: RepositoryBinding;
  /** Re-fetches the binding list — used for the per-row manual refresh button. */
  onRefresh: () => void;
  /** Optimistic removal hook fired after a successful DELETE. */
  onUnbound: (bindingId: string) => void;
}

/**
 * One row of the repository bindings list. Renders provider / owner / repo /
 * branch, the clone-status pill (with a spinner for transient states), the
 * PR-state pill (when a PR is tracked), and per-row actions:
 *
 *  - Manual refresh — re-fetches the bindings list so a stuck or just-recovered
 *    clone surfaces without a page reload. This is intentionally cheap; the
 *    PR-comments `POST .../sync` lives in the comments view, not here.
 *  - Delete — removes the binding AND its on-disk workspace folder via DELETE,
 *    after a confirm that spells out the data loss. The realtime
 *    `intent.repository_unbound` event keeps other clients in sync; the local
 *    list also drops the row optimistically via `onUnbound` so the user sees
 *    immediate feedback even before the SSE round-trip.
 */
export function RepositoryBindingRow({
  intentId,
  binding,
  onRefresh,
  onUnbound
}: RepositoryBindingRowProps) {
  const [unbinding, setUnbinding] = useState(false);
  const [unbindError, setUnbindError] = useState<string | null>(null);

  const status = cloneStatusMeta[binding.clone_status];
  const pending = isCloneTransient(binding.clone_status);
  const fullName = repositoryFullName(binding);
  const hasPr = hasPullRequest(binding);

  async function handleUnbind() {
    const confirmed = window.confirm(
      `Удалить репозиторий ${fullName}?\n\n` +
        "Рабочая папка этого репозитория будет удалена с диска без возможности " +
        "восстановления. Несохранённые изменения, локальные ветки и файлы, " +
        "которых нет на GitHub, пропадут.\n\nПродолжить удаление?"
    );
    if (!confirmed) return;
    setUnbinding(true);
    setUnbindError(null);
    try {
      await unbindIntentRepository(intentId, binding.id);
      onUnbound(binding.id);
    } catch (err) {
      setUnbindError(
        err instanceof HttpError
          ? `Не удалось удалить (${String(err.status)}). Папка могла быть занята — закройте процессы и повторите.`
          : "Не удалось удалить репозиторий."
      );
      setUnbinding(false);
    }
  }

  return (
    <li
      data-testid={`binding-row-${binding.id}`}
      className="flex flex-col gap-2 rounded-lg border border-base-300 bg-base-100 px-4 py-3"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex min-w-0 flex-col gap-1">
          <div className="flex min-w-0 items-center gap-2">
            <span
              aria-hidden
              className="inline-flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary"
            >
              <GitBranch size={14} strokeWidth={2} />
            </span>
            <span className="truncate font-mono text-sm font-semibold text-base-content">
              {fullName}
            </span>
            <span className="text-base-content/30">·</span>
            <span className="font-mono text-xs text-base-content/60">
              {binding.default_branch}
            </span>
          </div>
          <div className="flex flex-wrap items-center gap-2 text-xs">
            <span
              data-testid={`binding-clone-status-${binding.id}`}
              data-status={binding.clone_status}
              className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 font-semibold"
              style={{ backgroundColor: status.surface, color: status.ink }}
            >
              {pending ? (
                <Loader2
                  aria-hidden
                  size={12}
                  strokeWidth={2.25}
                  className="animate-spin"
                />
              ) : binding.clone_status === "ready" ? null : (
                <AlertTriangle aria-hidden size={12} strokeWidth={2.25} />
              )}
              {status.label}
            </span>
            {hasPr ? <PullRequestChip binding={binding} /> : null}
          </div>
          {binding.clone_status === "failed" ||
          binding.clone_status === "broken" ? (
            <p
              role="alert"
              className="m-0 break-words text-xs text-error"
              data-testid={`binding-error-${binding.id}`}
            >
              {binding.clone_error ?? "Клон не удался."}
            </p>
          ) : null}
        </div>

        <div className="flex flex-shrink-0 items-center gap-2">
          <OpenBindingInVscodeButton
            intentId={intentId}
            bindingId={binding.id}
            fullName={fullName}
            disabled={binding.clone_status !== "ready"}
          />
          <Button
            aria-label={`Обновить статус ${fullName}`}
            disabled={unbinding}
            icon={<RefreshCw aria-hidden size={14} strokeWidth={2} />}
            onClick={onRefresh}
          >
            Обновить
          </Button>
          <Button
            aria-label={`Удалить ${fullName}`}
            disabled={unbinding}
            icon={<Trash2 aria-hidden size={14} strokeWidth={2} />}
            onClick={() => {
              void handleUnbind();
            }}
          >
            {unbinding ? "Удаляем…" : "Удалить"}
          </Button>
        </div>
      </div>

      {unbindError ? (
        <p role="alert" className="m-0 text-xs text-error">
          {unbindError}
        </p>
      ) : null}
    </li>
  );
}

function PullRequestChip({ binding }: { binding: RepositoryBinding }) {
  const number = binding.pull_request_number;
  if (number === undefined) return null;
  const meta = binding.pull_request_state
    ? pullRequestStateMeta[binding.pull_request_state]
    : null;
  return (
    <span
      className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 font-semibold"
      style={
        meta
          ? { backgroundColor: meta.surface, color: meta.ink }
          : {
              backgroundColor: "#F6F7FB",
              color: "#4C5567"
            }
      }
      data-testid={`binding-pr-${binding.id}`}
    >
      <GitPullRequest aria-hidden size={12} strokeWidth={2.25} />
      PR #{number}
      {meta ? <span className="text-[10px]">· {meta.label}</span> : null}
    </span>
  );
}
