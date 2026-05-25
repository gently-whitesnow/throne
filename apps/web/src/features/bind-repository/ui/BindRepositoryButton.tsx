import { Plus } from "lucide-react";
import { useState } from "react";

import type { RepositoryBinding } from "@/entities/repository-binding";
import { Button } from "@/shared/ui";

import { BindRepositoryModal } from "./BindRepositoryModal";

interface BindRepositoryButtonProps {
  intentId: string;
  /**
   * Fired immediately after a successful POST. The bindings list updates via
   * realtime `intent.repository_bound`, so this callback is just for local
   * side-effects (e.g. focus / toast) — the row will appear regardless.
   */
  onBound?: (binding: RepositoryBinding) => void;
}

/**
 * Trigger button that mounts the `BindRepositoryModal` on click. Kept as a
 * thin wrapper so widgets can compose it without owning the open/close state
 * — the modal handles its own lifecycle (search, selection, submit).
 */
export function BindRepositoryButton({
  intentId,
  onBound
}: BindRepositoryButtonProps) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <Button
        aria-label="Привязать репозиторий"
        icon={<Plus aria-hidden size={14} strokeWidth={2} />}
        variant="primary"
        onClick={() => {
          setOpen(true);
        }}
        data-testid="bind-repository-open"
      >
        Привязать
      </Button>
      <BindRepositoryModal
        intentId={intentId}
        open={open}
        onClose={() => {
          setOpen(false);
        }}
        onBound={onBound}
      />
    </>
  );
}
