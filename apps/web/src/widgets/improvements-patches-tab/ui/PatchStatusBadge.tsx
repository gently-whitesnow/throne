import type { PromptPartPatchStatus } from "@/entities/prompt-part-patch";

const STATUS_LABELS: Record<PromptPartPatchStatus, string> = {
  proposed: "Proposed",
  applied: "Applied",
  applied_edited: "Applied (edited)",
  rejected: "Rejected",
  superseded: "Superseded"
};

const STATUS_CLASSES: Record<PromptPartPatchStatus, string> = {
  proposed: "badge badge-soft badge-info",
  applied: "badge badge-soft badge-success",
  applied_edited: "badge badge-soft badge-success",
  rejected: "badge badge-soft badge-error",
  superseded: "badge badge-soft badge-neutral"
};

/**
 * Semantic patch-status pill. Light-first, daisy semantic tokens (DESIGN.md):
 * proposed = info, applied/applied_edited = success, rejected = error,
 * superseded = neutral.
 */
export function PatchStatusBadge({
  status
}: {
  status: PromptPartPatchStatus;
}) {
  return (
    <span className={STATUS_CLASSES[status]}>{STATUS_LABELS[status]}</span>
  );
}
