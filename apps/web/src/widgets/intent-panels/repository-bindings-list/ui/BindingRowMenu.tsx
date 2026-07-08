import { MoreHorizontal } from "lucide-react";
import { useEffect, useRef, useState, type ReactNode } from "react";

interface BindingRowMenuProps {
  /** Accessible label for the trigger, e.g. the repo full name. */
  label: string;
  children: ReactNode;
}

/**
 * Overflow-меню («⋯») второстепенных действий строки репозитория. Триггер +
 * панель, закрывается по клику вне и Esc. Содержимое (пункты меню) передаётся
 * как `children`, чтобы строка сама решала, что прятать под overflow.
 */
export function BindingRowMenu({ label, children }: BindingRowMenuProps) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!open) return;
    const onDocClick = (event: MouseEvent) => {
      if (!containerRef.current?.contains(event.target as Node)) {
        setOpen(false);
      }
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

  return (
    <div ref={containerRef} className="relative">
      <button
        type="button"
        onClick={() => {
          setOpen((value) => !value);
        }}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={`Ещё действия — ${label}`}
        className="btn btn-sm btn-soft btn-square"
      >
        <MoreHorizontal aria-hidden size={16} strokeWidth={2} />
      </button>
      {open ? (
        <div
          role="menu"
          className="absolute right-0 top-[calc(100%+6px)] z-20 w-56 rounded-md border border-base-300 bg-base-100 p-1 shadow-lg"
        >
          {children}
        </div>
      ) : null}
    </div>
  );
}
