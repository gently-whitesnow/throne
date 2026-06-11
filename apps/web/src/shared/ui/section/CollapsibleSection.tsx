import { ChevronRight } from "lucide-react";
import { useId, useState, type HTMLAttributes, type ReactNode } from "react";

import { SectionCount } from "./SectionHeading";

interface CollapsibleSectionProps extends Omit<
  HTMLAttributes<HTMLElement>,
  "title"
> {
  /** lucide icon for the section header. */
  icon: ReactNode;
  title: string;
  count?: number;
  defaultOpen?: boolean;
  /** Inline meta after the count — stays visible while collapsed (e.g. blockers). */
  meta?: ReactNode;
  /** Right-aligned controls outside the toggle button. */
  actions?: ReactNode;
  children: ReactNode;
}

/**
 * Сворачиваемая второстепенная секция: заголовок-кнопка (иконка + название +
 * счётчик) раскрывает тело. Свёрнута по умолчанию, чтобы Связи / Активность /
 * PR comments не съедали вертикаль, пока на них не смотрят. Тело монтируется
 * только в раскрытом состоянии — пустой блок не занимает места.
 */
export function CollapsibleSection({
  icon,
  title,
  count,
  defaultOpen = false,
  meta,
  actions,
  children,
  className,
  ...rest
}: CollapsibleSectionProps) {
  const [open, setOpen] = useState(defaultOpen);
  const bodyId = useId();

  return (
    <section
      className={["flex flex-col gap-2", className].filter(Boolean).join(" ")}
      {...rest}
    >
      <div className="flex items-center justify-between gap-3">
        <button
          type="button"
          onClick={() => {
            setOpen((value) => !value);
          }}
          aria-expanded={open}
          aria-controls={bodyId}
          className="group flex min-w-0 items-center gap-2 rounded text-left focus-visible:outline-2 focus-visible:outline-primary focus-visible:outline-offset-2"
        >
          <ChevronRight
            aria-hidden
            size={14}
            strokeWidth={2.5}
            className={`flex-shrink-0 text-base-content/40 transition-transform ${
              open ? "rotate-90" : ""
            }`}
          />
          <span aria-hidden className="flex-shrink-0 text-base-content/45">
            {icon}
          </span>
          <span className="truncate text-xs font-semibold uppercase tracking-wide text-base-content/55">
            {title}
          </span>
          {count !== undefined ? <SectionCount value={count} /> : null}
          {meta}
        </button>
        {actions ? (
          <div className="flex flex-shrink-0 items-center gap-2">{actions}</div>
        ) : null}
      </div>
      {open ? <div id={bodyId}>{children}</div> : null}
    </section>
  );
}
