import { FileText, Hash, KeyRound, Sparkles, Sprout } from "lucide-react";
import { NavLink, Outlet } from "react-router-dom";

import { useDreamPendingCount } from "../model/use-dream-pending-count";

const NAV_ITEMS = [
  { to: "/intents", label: "Intents", icon: Sparkles },
  { to: "/tags", label: "Tags", icon: Hash },
  { to: "/instructions", label: "Instructions", icon: FileText },
  { to: "/dream", label: "Dream", icon: Sprout },
  { to: "/me/mcp-token", label: "MCP Token", icon: KeyRound }
] as const;

export function AppShell() {
  const dreamPending = useDreamPendingCount();
  return (
    <div className="grid min-h-screen grid-rows-[auto_1fr] md:grid-cols-[56px_1fr] md:grid-rows-1">
      <aside
        className="flex gap-2 overflow-x-auto border-b border-base-300 bg-base-200 px-2 py-2 md:flex-col md:items-center md:gap-3 md:overflow-visible md:border-b-0 md:border-r md:px-2 md:py-3"
        aria-label="Основная навигация"
      >
        <div
          className="flex h-9 w-9 items-center justify-center rounded-md bg-primary/10 text-[15px] font-bold text-primary"
          title="Throne"
          aria-label="Throne"
        >
          T
        </div>
        <nav className="flex gap-1 md:flex-col md:gap-1" aria-label="Разделы">
          {NAV_ITEMS.map(({ to, label, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              className={navItemClass}
              title={label}
              aria-label={label}
            >
              <Icon aria-hidden size={18} strokeWidth={2} />
              {to === "/dream" && dreamPending > 0 ? (
                <span
                  aria-label={`${String(dreamPending)} pending dream proposals`}
                  className="absolute right-0.5 top-0.5 inline-flex h-[16px] min-w-[16px] items-center justify-center rounded-full bg-error px-1 text-[9px] font-bold leading-none text-error-content"
                >
                  {String(dreamPending)}
                </span>
              ) : null}
            </NavLink>
          ))}
        </nav>
      </aside>
      <main className="min-h-screen min-w-0 bg-base-100">
        <Outlet />
      </main>
    </div>
  );
}

function navItemClass({ isActive }: { isActive: boolean }): string {
  const base =
    "relative flex h-10 w-10 items-center justify-center rounded-md transition-colors";
  return isActive
    ? `${base} bg-primary/10 text-primary`
    : `${base} text-base-content/60 hover:bg-base-300/60 hover:text-base-content`;
}
