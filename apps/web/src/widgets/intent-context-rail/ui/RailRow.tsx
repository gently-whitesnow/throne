import type { ReactNode } from "react";

interface RailRowProps {
  label: string;
  icon: React.ReactNode;
  count: number | null;
  active: boolean;
  onSelect: () => void;
  muted?: boolean;
  nested?: boolean;
  action?: ReactNode;
}

export function RailRow({
  label,
  icon,
  count,
  active,
  onSelect,
  muted,
  nested,
  action
}: RailRowProps) {
  return (
    <div
      className={[
        "group relative flex w-full items-center border-l-[3px] transition-colors",
        active
          ? "border-primary bg-primary/10"
          : "border-transparent hover:bg-base-200"
      ].join(" ")}
    >
      <button
        type="button"
        onClick={onSelect}
        aria-current={active ? "true" : undefined}
        className={[
          "flex min-w-0 flex-1 items-center gap-2 py-1.5 pr-9 text-left text-[13px]",
          nested ? "pl-8" : "pl-3.5",
          active
            ? "font-semibold text-primary"
            : muted
              ? "text-base-content/70"
              : "text-base-content"
        ].join(" ")}
      >
        <span
          className={
            active
              ? "text-primary"
              : muted
                ? "text-base-content/50"
                : "text-base-content/70"
          }
        >
          {icon}
        </span>
        <span className="min-w-0 flex-1 truncate">{label}</span>
      </button>
      <div className="pointer-events-none absolute right-2 top-1/2 flex h-6 w-6 -translate-y-1/2 items-center justify-center">
        {count === null ? null : (
          <span
            aria-hidden={action ? "true" : undefined}
            className={[
              "tabular-nums text-[11px] transition-opacity",
              action ? "group-hover:opacity-0 group-focus-within:opacity-0" : "",
              active ? "text-primary/80" : "text-base-content/40"
            ].join(" ")}
          >
            {String(count)}
          </span>
        )}
        {action ? (
          <div className="pointer-events-auto absolute inset-0 opacity-0 transition-opacity group-hover:opacity-100 group-focus-within:opacity-100">
            {action}
          </div>
        ) : null}
      </div>
    </div>
  );
}
