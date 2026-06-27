import {
  FileText,
  Hash,
  PackageCheck,
  Power,
  Settings,
  Sparkles,
  Sprout
} from "lucide-react";
import { NavLink, Outlet } from "react-router-dom";

import { useThroneReadiness } from "@/features/throne-readiness";

import { useProposedPatchesCount } from "../model/use-proposed-patches-count";
import { useRuntimeInstance } from "../model/use-runtime-instance";

const NAV_ITEMS = [
  { to: "/intents", label: "Intents", icon: Sparkles },
  { to: "/tags", label: "Tags", icon: Hash },
  { to: "/instructions", label: "Prompt parts", icon: FileText },
  { to: "/improvements", label: "Improvements", icon: Sprout },
  { to: "/launch-skills", label: "Launch skills", icon: PackageCheck },
  { to: "/settings", label: "Settings", icon: Settings }
] as const;

export function AppShell() {
  const proposedPatches = useProposedPatchesCount();
  const readiness = useThroneReadiness();
  const runtime = useRuntimeInstance();
  const showNotReadyBadge = !readiness.isLoading && !readiness.ready;
  const navItems = NAV_ITEMS;
  return (
    <div className="grid h-screen grid-rows-[auto_1fr] overflow-hidden md:grid-cols-[56px_1fr] md:grid-rows-1">
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
          {navItems.map(({ to, label, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              className={navItemClass}
              title={label}
              aria-label={label}
            >
              <Icon aria-hidden size={18} strokeWidth={2} />
              {to === "/improvements" && proposedPatches > 0 ? (
                <span
                  aria-label={`${String(proposedPatches)} proposed prompt part patches`}
                  className="absolute right-0.5 top-0.5 inline-flex h-[16px] min-w-[16px] items-center justify-center rounded-full bg-error px-1 text-[9px] font-bold leading-none text-error-content"
                >
                  {String(proposedPatches)}
                </span>
              ) : null}
              {to === "/settings" && showNotReadyBadge ? (
                <span
                  aria-label="Throne не готов к работе"
                  className="absolute right-0.5 top-0.5 inline-flex h-[16px] min-w-[16px] items-center justify-center rounded-full bg-error px-1 text-[9px] font-bold leading-none text-error-content"
                >
                  !
                </span>
              ) : null}
            </NavLink>
          ))}
        </nav>
        {runtime.isEphemeral ? (
          <button
            type="button"
            className="relative ml-auto flex h-10 w-10 items-center justify-center rounded-md text-error transition-[color,background-color,scale] hover:bg-error/10 focus-visible:outline-2 focus-visible:outline-error active:scale-[0.96] disabled:opacity-60 md:ml-0 md:mt-auto"
            title="Завершение"
            aria-label="Завершение"
            disabled={runtime.isStopping}
            onClick={() => {
              runtime.stop();
            }}
          >
            <Power aria-hidden size={18} strokeWidth={2} />
          </button>
        ) : null}
      </aside>
      <main className="min-h-0 min-w-0 overflow-y-auto bg-base-100">
        <Outlet />
      </main>
    </div>
  );
}

function navItemClass({ isActive }: { isActive: boolean }): string {
  const base =
    "relative flex h-10 w-10 items-center justify-center rounded-md transition-[color,background-color,scale] active:scale-[0.96]";
  return isActive
    ? `${base} bg-primary/10 text-primary`
    : `${base} text-base-content/60 hover:bg-base-300/60 hover:text-base-content`;
}
