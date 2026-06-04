import { AlertTriangle, Loader2 } from "lucide-react";

import {
  cloneStatusMeta,
  isCloneTransient,
  repositoryFullName,
  type RepositoryBinding
} from "@/entities/repository-binding";

interface PreflightProgressProps {
  bindings: readonly RepositoryBinding[];
}

/**
 * Per-binding строка с текущим clone_status. Источник — список биндингов из
 * `useIntentRepositories`, который сам подписан на realtime-эвент
 * `intent.repository_clone_progress`, так что отдельная подписка не нужна.
 */
export function PreflightProgress({ bindings }: PreflightProgressProps) {
  if (bindings.length === 0) {
    return null;
  }
  return (
    <ul
      data-testid="agent-terminal-preflight"
      className="m-0 flex list-none flex-col gap-1 p-0 text-xs"
    >
      {bindings.map((binding) => {
        const status = cloneStatusMeta[binding.clone_status];
        const pending = isCloneTransient(binding.clone_status);
        const fullName = repositoryFullName(binding);
        return (
          <li
            key={binding.id}
            data-testid={`agent-terminal-preflight-${binding.id}`}
            data-status={binding.clone_status}
            className="flex items-center gap-2"
          >
            <span
              aria-hidden
              className="inline-flex h-4 w-4 items-center justify-center text-base-content/70"
            >
              {pending ? (
                <Loader2
                  size={12}
                  strokeWidth={2.25}
                  className="animate-spin"
                />
              ) : binding.clone_status === "ready" ? (
                <span className="inline-block h-2 w-2 rounded-full bg-success" />
              ) : (
                <AlertTriangle size={12} strokeWidth={2.25} />
              )}
            </span>
            <span className="font-mono text-base-content">{fullName}</span>
            <span
              className="rounded-full px-2 py-0.5 font-semibold"
              style={{ backgroundColor: status.surface, color: status.ink }}
            >
              {status.label}
            </span>
            {binding.clone_error !== undefined &&
            binding.clone_error !== null &&
            binding.clone_error.length > 0 ? (
              <span className="text-error">{binding.clone_error}</span>
            ) : null}
          </li>
        );
      })}
    </ul>
  );
}
