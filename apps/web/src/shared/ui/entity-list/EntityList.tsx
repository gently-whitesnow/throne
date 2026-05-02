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
      <p className="entity-list__empty">{emptyMessage ?? "Список пуст."}</p>
    );
  }

  return (
    <ul className="entity-list" role="list">
      {items.map((row) => (
        <li key={row.id}>
          <NavLink
            to={row.href}
            className={({ isActive }) =>
              `entity-list__row${isActive ? " entity-list__row--active" : ""}`
            }
          >
            {row.badge ? (
              <span
                className="entity-list__badge"
                style={
                  row.badgeColor || row.badgeTextColor
                    ? {
                        background: row.badgeColor,
                        color: row.badgeTextColor
                      }
                    : undefined
                }
              >
                {row.badge}
              </span>
            ) : null}
            <span className="entity-list__main">
              <span className="entity-list__title">{row.title}</span>
              {row.subtitle ? (
                <span className="entity-list__subtitle">{row.subtitle}</span>
              ) : null}
            </span>
            {row.meta ? (
              <span className="entity-list__meta">{row.meta}</span>
            ) : null}
          </NavLink>
        </li>
      ))}
    </ul>
  );
}
