import { Plus } from "lucide-react";
import { useState } from "react";

import type { Repository } from "@/entities/repository";

import { AddRepositoryModal } from "./AddRepositoryModal";

interface AddRepositoryButtonProps {
  onAdded: (repo: Repository) => void;
}

export function AddRepositoryButton({ onAdded }: AddRepositoryButtonProps) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <button
        type="button"
        className="btn btn-sm btn-primary"
        onClick={() => {
          setOpen(true);
        }}
      >
        <Plus aria-hidden size={16} strokeWidth={2} /> Добавить
      </button>
      {open ? (
        <AddRepositoryModal
          onClose={() => {
            setOpen(false);
          }}
          onAdded={(repo) => {
            setOpen(false);
            onAdded(repo);
          }}
        />
      ) : null}
    </>
  );
}
