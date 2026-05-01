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
        className={`drawer-scrim${open ? " drawer-scrim--open" : ""}`}
        onClick={onClose}
        aria-hidden
      />
      <aside
        className={`drawer${open ? " drawer--open" : ""}`}
        aria-hidden={!open}
        aria-label={title}
      >
        <header className="drawer__header">
          <h3 className="drawer__title">{title}</h3>
          <button
            type="button"
            className="drawer__close"
            onClick={onClose}
            aria-label="Закрыть"
          >
            <X aria-hidden size={16} strokeWidth={2} />
          </button>
        </header>
        <div className="drawer__body">
          {open ? (
            <TextVersionList endpoint={endpoint} reloadKey={reloadKey} />
          ) : null}
        </div>
      </aside>
    </>
  );
}
