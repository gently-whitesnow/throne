import { EFFORT_LABEL } from "@/entities/terminal-setting";

import type { PersistedLaunchArgs } from "../model/types";
import { RUN_MODE_LABEL } from "../model/types";

interface LaunchAxisBadgesProps {
  /** Фактическая ось живой сессии. */
  launch: PersistedLaunchArgs;
  /** Человекочитаемый label вендора из каталога (fallback — сам id). */
  vendorLabel: string;
}

/**
 * Компактное read-only представление оси живой сессии (режим/vendor/model/effort).
 * Параметры запущенной сессии менять нельзя — поэтому бейджи, а не селекторы;
 * сами селекторы оператор видит только в preflight-модалке перед запуском.
 */
export function LaunchAxisBadges({
  launch,
  vendorLabel
}: LaunchAxisBadgesProps) {
  const items = [
    RUN_MODE_LABEL[launch.mode],
    vendorLabel,
    launch.model,
    launch.effort ? EFFORT_LABEL[launch.effort] : null
  ].filter((v): v is string => Boolean(v));

  return (
    <div
      data-testid="agent-terminal-axis-badges"
      className="flex flex-wrap items-center gap-1.5"
    >
      {items.map((label) => (
        <span
          key={label}
          className="badge badge-sm badge-ghost text-[11px] font-medium"
        >
          {label}
        </span>
      ))}
    </div>
  );
}
