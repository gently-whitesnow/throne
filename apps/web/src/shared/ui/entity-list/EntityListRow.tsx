import { Pin } from "lucide-react";
import { NavLink } from "react-router-dom";

import type { EntityListRow as EntityListRowData } from "./EntityList";

interface EntityListRowProps {
  row: EntityListRowData;
}

/**
 * Inline-контент строки списка (бейдж, заголовок, мета, warning, tint). Вынесено
 * из EntityList отдельным компонентом, чтобы переиспользовать в виртуальном
 * варианте (`VirtualEntityList`) без копипасты JSX.
 */
export function EntityListRowContent({ row }: EntityListRowProps) {
  return (
    <>
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
          <span className="flex items-center gap-1.5 truncate font-medium leading-tight">
            {row.pinned ? (
              <Pin
                aria-label="Запинено"
                size={12}
                strokeWidth={2.5}
                className="flex-shrink-0 text-primary"
              />
            ) : null}
            <span className="truncate">{row.title}</span>
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
        {row.warning ? (
          <span
            className="flex-shrink-0 rounded border border-warning/40 bg-warning/10 px-1.5 py-px text-[10px] font-semibold text-warning"
            title={row.warningTitle ?? row.warning}
          >
            {row.warning}
          </span>
        ) : null}
        {row.trailing ?? null}
      </NavLink>
      {row.tint ? (
        // Тонкая вертикальная полоска у левого края — маркер «семьи»
        // карточек (общий derived_from-предок). Лежит поверх NavLink,
        // чтобы hover-bg её не перекрашивал; pointer-events отключены,
        // чтобы клик уходил на ссылку.
        <span
          aria-hidden
          className="pointer-events-none absolute bottom-0 left-0 top-0 w-[3px]"
          style={{ backgroundColor: row.tint }}
        />
      ) : null}
    </>
  );
}
