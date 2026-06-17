import { useState } from "react";

import type { PromptPartPatchDetail } from "@/entities/prompt-part-patch";
import { applyPromptPartPatch } from "@/features/apply-prompt-part-patch";
import { rejectPromptPartPatch } from "@/features/reject-prompt-part-patch";
import { Button } from "@/shared/ui";

import { ApplyEditModal } from "./ApplyEditModal";
import { PatchDiffPreview } from "./PatchDiffPreview";
import { PatchStatusBadge } from "./PatchStatusBadge";
import { RejectPatchModal } from "./RejectPatchModal";
import { StructuralPatchPreview } from "./StructuralPatchPreview";

export interface PatchDetailPanelProps {
  detail: PromptPartPatchDetail;
  onChanged: () => void;
}

type Action = "idle" | "apply" | "apply-edit" | "reject";

export function PatchDetailPanel({ detail, onChanged }: PatchDetailPanelProps) {
  const [busy, setBusy] = useState<Action>("idle");
  const [error, setError] = useState<string | null>(null);
  const [showEdit, setShowEdit] = useState(false);
  const [showReject, setShowReject] = useState(false);

  const isProposed = detail.patch.status === "proposed";
  const isTextOperation =
    detail.patch.operation === "replace_text" ||
    detail.patch.operation === "create";

  const runAction = async (action: Action, fn: () => Promise<unknown>) => {
    setBusy(action);
    setError(null);
    try {
      await fn();
      onChanged();
    } catch (e: unknown) {
      setError(
        e instanceof Error ? e.message : "Не удалось выполнить действие."
      );
    } finally {
      setBusy("idle");
    }
  };

  return (
    <section
      className="flex flex-col gap-4 rounded-lg border border-base-300 bg-base-50 p-4"
      aria-label={`PromptPartPatch ${detail.patch.id}`}
    >
      <header className="flex flex-wrap items-center gap-2">
        <PatchStatusBadge status={detail.patch.status} />
        <span className="badge badge-soft badge-neutral">
          target: {detail.patch.target_scope}/{detail.patch.target_key}
        </span>
        <span className="badge badge-soft badge-neutral">
          base v{String(detail.patch.base_version)}
        </span>
        <span className="badge badge-soft badge-neutral">
          {detail.patch.operation}
        </span>
        {!detail.base_version_matches_current ? (
          <span className="badge badge-soft badge-warning">needs rebase</span>
        ) : null}
        <span className="ml-auto text-xs text-base-content/60">
          {detail.patch.evidence_card_ids.length} evidence cards
        </span>
      </header>

      {detail.patch.rationale ? (
        <p className="m-0 text-sm leading-relaxed text-base-content/80">
          {detail.patch.rationale}
        </p>
      ) : null}

      {isTextOperation ? (
        <PatchDiffPreview detail={detail} />
      ) : (
        <StructuralPatchPreview detail={detail} />
      )}

      {detail.patch.applied_text &&
      detail.patch.applied_text !== detail.patch.patch_text ? (
        <details className="rounded border border-base-300 bg-base-100 p-2 text-xs">
          <summary className="cursor-pointer font-semibold">
            Что было применено (`applied_text` отличается от `patch_text`)
          </summary>
          <pre className="m-0 mt-2 whitespace-pre-wrap break-words leading-relaxed">
            {detail.patch.applied_text}
          </pre>
        </details>
      ) : null}
      {detail.patch.reject_comment ? (
        <p className="m-0 rounded border border-base-300 bg-base-100 p-2 text-xs leading-relaxed text-base-content/80">
          <span className="font-semibold">Reject:</span>{" "}
          {detail.patch.reject_comment}
        </p>
      ) : null}

      {isProposed ? (
        <footer className="flex flex-wrap items-center gap-2">
          <Button
            variant="primary"
            disabled={busy !== "idle"}
            onClick={() =>
              void runAction("apply", () =>
                applyPromptPartPatch({ patchId: detail.patch.id })
              )
            }
          >
            {busy === "apply" ? "Применяем..." : "Apply"}
          </Button>
          {isTextOperation ? (
            <Button
              disabled={busy !== "idle"}
              onClick={() => {
                setShowEdit(true);
              }}
            >
              Apply with edit
            </Button>
          ) : null}
          <Button
            disabled={busy !== "idle"}
            onClick={() => {
              setShowReject(true);
            }}
          >
            Reject
          </Button>
        </footer>
      ) : null}

      {error ? (
        <p
          role="alert"
          className="m-0 rounded border border-error/30 bg-error/10 p-2 text-xs text-error"
        >
          {error}
        </p>
      ) : null}

      <ApplyEditModal
        open={showEdit}
        initialText={detail.patch.patch_text}
        currentText={detail.base_part_text}
        busy={busy === "apply-edit"}
        onCancel={() => {
          setShowEdit(false);
        }}
        onConfirm={async (finalText) => {
          await runAction("apply-edit", () =>
            applyPromptPartPatch({ patchId: detail.patch.id, finalText })
          );
          setShowEdit(false);
        }}
      />
      <RejectPatchModal
        open={showReject}
        busy={busy === "reject"}
        onCancel={() => {
          setShowReject(false);
        }}
        onConfirm={async (comment) => {
          await runAction("reject", () =>
            rejectPromptPartPatch({ patchId: detail.patch.id, comment })
          );
          setShowReject(false);
        }}
      />
    </section>
  );
}
