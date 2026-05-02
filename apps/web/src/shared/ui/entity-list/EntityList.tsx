import { NavLink } from "react-router-dom";

export interface EntityListRow {
  id: string;
  title: string;
  subtitle?: string;
  meta?: string;
  badge?: string;
  badgeColor?: string;
  badgeTextColor?: string;
  href: string;
}

interface EntityListProps {
  items: readonly EntityListRow[];
  emptyMessage?: string;
}

export function EntityList({ items, emptyMessage }: EntityListProps) {
  if (items.length === 0) {
    return (
      <p className="px-3.5 py-4 text-[13px] text-base-content/60">
        {emptyMessage ?? "Список пуст."}
      </p>
    );
  }

  return (
    <ul className="flex flex-col py-1" role="list">
      {items.map((row) => (
        <li key={row.id}>
          <NavLink
            to={row.href}
            className={({ isActive }) =>
              [
                "flex min-h-9 items-center gap-2 border-l-2 px-3.5 py-1.5 text-[13px] no-underline focus-visible:outline-2 focus-visible:outline-primary focus-visible:-outline-offset-2",
                isActive
                  ? "border-primary bg-primary/10 text-base-content"
                  : "border-transparent text-base-content hover:bg-base-200"
              ].join(" ")
            }
          >
            {row.badge ? (
              <span
                className="inline-flex h-[18px] flex-shrink-0 items-center rounded px-1.5 text-[10px] font-semibold"
                style={
                  row.badgeColor || row.badgeTextColor
                    ? {
                        background: row.badgeColor,
                        color: row.badgeTextColor
                      }
                    : { background: "var(--color-base-200)" }
                }
              >
                {row.badge}
              </span>
            ) : null}
            <span className="flex min-w-0 flex-1 flex-col gap-px">
              <span className="truncate font-medium leading-tight">
                {row.title}
              </span>
              {row.subtitle ? (
                <span className="truncate text-[11px] text-base-content/60">
                  {row.subtitle}
                </span>
              ) : null}
            </span>
            {row.meta ? (
              <span className="flex-shrink-0 text-[11px] tabular-nums text-base-content/60">
                {row.meta}
              </span>
            ) : null}
          </NavLink>
        </li>
      ))}
    </ul>
  );
}
