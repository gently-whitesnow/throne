import { MoreVertical, Trash2 } from "lucide-react";
import { useEffect, useRef, useState } from "react";

import { httpDelete, intentsEndpoints } from "@/shared/api";
import { errorMessage } from "@/shared/lib";

interface DeleteIntentButtonProps {
  intentId: string;
  onDeleted: () => void;
}

export function DeleteIntentButton({
  intentId,
  onDeleted
}: DeleteIntentButtonProps) {
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const ref = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!open) return;
    const onDocClick = (event: MouseEvent) => {
      if (!ref.current) return;
      if (!ref.current.contains(event.target as Node)) setOpen(false);
    };
    const onEsc = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    document.addEventListener("mousedown", onDocClick);
    document.addEventListener("keydown", onEsc);
    return () => {
      document.removeEventListener("mousedown", onDocClick);
      document.removeEventListener("keydown", onEsc);
    };
  }, [open]);

  const handleDelete = async () => {
    if (busy) return;
    if (!window.confirm("Удалить Intent и всю историю версий?")) return;
    setBusy(true);
    setError(null);
    try {
      await httpDelete(intentsEndpoints.deleteIntent(intentId));
      onDeleted();
    } catch (err: unknown) {
      const message = errorMessage(err, { base: "Не удалось удалить" });
      setError(message);
      setBusy(false);
      setOpen(false);
    }
  };

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        onClick={() => {
          setOpen((value) => !value);
        }}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label="Дополнительные действия"
        disabled={busy}
        className="inline-flex h-8 w-8 items-center justify-center rounded-md text-base-content/70 transition-colors hover:bg-base-200 hover:text-base-content"
      >
        <MoreVertical aria-hidden size={16} strokeWidth={2} />
      </button>
      {open ? (
        <div
          role="menu"
          className="absolute right-0 top-[calc(100%+4px)] z-20 w-44 rounded-md border border-base-300 bg-base-100 p-1 shadow-lg"
        >
          <button
            role="menuitem"
            type="button"
            onClick={() => {
              void handleDelete();
            }}
            disabled={busy}
            className="flex w-full items-center gap-2 rounded px-2 py-1.5 text-left text-sm text-error hover:bg-error/10 disabled:opacity-60"
          >
            <Trash2 aria-hidden size={14} strokeWidth={2} />
            <span>{busy ? "Удаляем…" : "Удалить intent"}</span>
          </button>
        </div>
      ) : null}
      {error ? (
        <p
          role="alert"
          className="absolute right-0 top-[calc(100%+4px)] z-10 m-0 whitespace-nowrap rounded border border-error/30 bg-error/10 px-2 py-1 text-xs text-error"
        >
          {error}
        </p>
      ) : null}
    </div>
  );
}
