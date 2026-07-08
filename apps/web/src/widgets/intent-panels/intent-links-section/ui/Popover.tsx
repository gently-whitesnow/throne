import { useEffect, useRef, type ReactNode } from "react";

interface PopoverProps {
  open: boolean;
  onClose: () => void;
  children: ReactNode;
  label: string;
}

/**
 * Простой inline-поповер: backdrop ловит клик-снаружи, Escape закрывает,
 * содержимое центрируется по экрану. Используется для «+» бакетов и
 * type-picker'а при drop на секцию. Без портала и floating-ui — упрощённо,
 * хватит для текущего UX.
 */
export function Popover({ open, onClose, children, label }: PopoverProps) {
  const cardRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("keydown", onKey);
    };
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={label}
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 p-4"
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div
        ref={cardRef}
        className="w-full max-w-sm rounded-lg border border-base-300 bg-base-100 p-3 shadow-lg"
      >
        {children}
      </div>
    </div>
  );
}
