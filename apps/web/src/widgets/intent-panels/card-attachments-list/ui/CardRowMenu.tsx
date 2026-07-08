import { MoreHorizontal } from "lucide-react";
import { useEffect, useRef, useState, type ReactNode } from "react";

interface CardRowMenuProps {
  /** Accessible label for the trigger, e.g. the card coordinate. */
  label: string;
  children: ReactNode;
}

/**
 * Overflow-меню («⋯») второстепенных действий строки карточки. Триггер +
 * панель, закрывается по клику вне и Esc. Форма донорится из
 * `repository-bindings-list/BindingRowMenu`; вынесено локально, потому что
 * FSD-слой запрещает виджету импортировать другой виджет.
 */
export function CardRowMenu({ label, children }: CardRowMenuProps) {
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
