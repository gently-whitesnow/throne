import type { ReactNode } from "react";

interface SectionHeadingProps {
  /** lucide icon, sized by the caller (14px is the section default). */
  icon: ReactNode;
  title: string;
  count?: number;
  /** Inline meta after the count (badges, warnings) — visible in the title row. */
  meta?: ReactNode;
  /** Right-aligned controls (primary action for the section). */
  actions?: ReactNode;
  id?: string;
}

/**
 * Единый заголовок секции детальной страницы интента: иконка + приглушённое
 * название (uppercase) + опциональный счётчик и мета. Держит секции одного
 * визуального веса, чтобы контент, а не хром, нёс акцент.
 */
export function SectionHeading({
  icon,
  title,
  count,
  meta,
  actions,
  id
}: SectionHeadingProps) {
  return (
    <div className="flex items-center justify-between gap-3">
      <div className="flex min-w-0 items-center gap-2">
        <span aria-hidden className="flex-shrink-0 text-base-content/45">
          {icon}
        </span>
        <h2
          id={id}
          className="m-0 truncate text-xs font-semibold uppercase tracking-wide text-base-content/55"
        >
          {title}
        </h2>
        {count !== undefined ? <SectionCount value={count} /> : null}
        {meta}
      </div>
      {actions ? (
        <div className="flex flex-shrink-0 items-center gap-2">{actions}</div>
      ) : null}
    </div>
  );
}

/** Нейтральный счётчик «· N» в заголовке секции. */
export function SectionCount({ value }: { value: number }) {
  return (
    <span className="rounded-full bg-base-200 px-1.5 py-0.5 text-[11px] font-semibold tabular-nums text-base-content/60">
      {value}
    </span>
  );
}
