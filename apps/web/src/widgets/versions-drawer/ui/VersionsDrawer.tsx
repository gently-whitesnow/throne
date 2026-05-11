import { X } from "lucide-react";
import { useEffect } from "react";

import { TextVersionList } from "@/entities/text-version";

interface VersionsDrawerProps {
  open: boolean;
  endpoint: string;
  reloadKey?: number;
  onClose: () => void;
  title?: string;
}

export function VersionsDrawer({
  open,
  endpoint,
  reloadKey,
  onClose,
  title = "История версий"
}: VersionsDrawerProps) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", onKey);
    return () => {
      window.removeEventListener("keydown", onKey);
    };
  }, [open, onClose]);

  return (
    <>
      <div
        className={`fixed inset-0 z-40 bg-neutral/40 transition-opacity duration-200 ${
          open ? "opacity-100" : "pointer-events-none opacity-0"
        }`}
        onClick={onClose}
        aria-hidden
      />
      <aside
        className={`fixed inset-y-0 right-0 z-50 flex w-[min(480px,100vw)] flex-col border-l border-base-300 bg-base-100 transition-transform duration-200 ${
          open ? "translate-x-0" : "translate-x-full"
        }`}
        aria-hidden={!open}
        aria-label={title}
      >
        <header className="flex flex-shrink-0 items-center justify-between gap-3 border-b border-base-300 px-4 py-3">
          <h3 className="m-0 text-sm font-semibold">{title}</h3>
          <button
            type="button"
            className="btn btn-sm btn-circle btn-ghost"
            onClick={onClose}
            aria-label="Закрыть"
          >
            <X aria-hidden size={16} strokeWidth={2} />
          </button>
        </header>
        <div className="min-h-0 flex-1 overflow-y-auto px-4 py-3">
          {open ? (
            <TextVersionList endpoint={endpoint} reloadKey={reloadKey} />
          ) : null}
        </div>
      </aside>
    </>
  );
}
