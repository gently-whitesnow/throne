import { useEffect, useRef, type ReactNode } from "react";
import { createPortal } from "react-dom";

interface ModalProps {
  onClose: () => void;
  children: ReactNode;
  /** id элемента-заголовка для aria-labelledby. */
  labelledBy?: string;
  /** id описания для aria-describedby. */
  describedBy?: string;
  /** aria-label для вариантов без видимого заголовка (fullscreen). */
  ariaLabel?: string;
  /** "dialog" — центрированный modal-box на затемнении; "fullscreen" — surface во весь экран. */
  variant?: "dialog" | "fullscreen";
  /** Доп. классы для modal-box (dialog) или контейнера (fullscreen). */
  boxClassName?: string;
  /** Классы позиционирования overlay (только dialog). По умолчанию daisyUI bottom→middle. */
  overlayClassName?: string;
  /** Закрывать по клику вне бокса (только dialog). По умолчанию true. */
  closeOnOverlayClick?: boolean;
  /** Закрывать по Escape. По умолчанию true. */
  closeOnEscape?: boolean;
}

const DIALOG_BOX_BASE = "modal-box border border-base-300 bg-base-100";

/**
 * Единственная точка с createPortal в проекте (ESLint запрещает createPortal вне
 * shared/ui). Централизует поведение, которое раньше копировалось в 6 модалках:
 * портал в document.body, закрытие по Escape, блокировку скролла фона, перехват и
 * возврат фокуса, overlay-click и aria-разметку диалога.
 */
export function Modal({
  onClose,
  children,
  labelledBy,
  describedBy,
  ariaLabel,
  variant = "dialog",
  boxClassName,
  overlayClassName = "modal-bottom sm:modal-middle",
  closeOnOverlayClick = true,
  closeOnEscape = true
}: ModalProps) {
  const boxRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handler = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        onClose();
      }
    };
    if (closeOnEscape) window.addEventListener("keydown", handler);
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      if (closeOnEscape) window.removeEventListener("keydown", handler);
      document.body.style.overflow = previousOverflow;
    };
  }, [closeOnEscape, onClose]);

  // Захват/возврат фокуса делаем один раз за жизнь модалки, чтобы смена identity
  // onClose не дёргала фокус на каждый ре-рендер. Если внутри уже есть autoFocus —
  // не перехватываем, иначе крадём фокус у поля ввода.
  useEffect(() => {
    const previouslyFocused = document.activeElement as HTMLElement | null;
    const box = boxRef.current;
    if (box && !box.contains(document.activeElement)) box.focus();
    return () => {
      previouslyFocused?.focus();
    };
  }, []);

  const dialog = (
    <div
      ref={boxRef}
      tabIndex={-1}
      role="dialog"
      aria-modal="true"
      aria-labelledby={labelledBy}
      aria-describedby={describedBy}
      aria-label={ariaLabel}
      className={
        variant === "fullscreen"
          ? `fixed inset-0 z-50 flex flex-col bg-base-100 outline-none${boxClassName ? ` ${boxClassName}` : ""}`
          : `${DIALOG_BOX_BASE} outline-none${boxClassName ? ` ${boxClassName}` : ""}`
      }
      onClick={
        variant === "dialog"
          ? (event) => {
              event.stopPropagation();
            }
          : undefined
      }
    >
      {children}
    </div>
  );

  if (variant === "fullscreen") {
    return createPortal(dialog, document.body);
  }

  return createPortal(
    <div
      className={`modal modal-open${overlayClassName ? ` ${overlayClassName}` : ""}`}
      role="presentation"
      onClick={closeOnOverlayClick ? onClose : undefined}
    >
      {dialog}
    </div>,
    document.body
  );
}
