import { FileText, Hash, Sparkles } from "lucide-react";
import { NavLink, Outlet } from "react-router-dom";

const NAV_ITEMS = [
  { to: "/intents", label: "Intents", icon: Sparkles },
  { to: "/tags", label: "Tags", icon: Hash },
  { to: "/instructions", label: "Instructions", icon: FileText }
] as const;

export function AppShell() {
  return (
    <div className="grid min-h-screen grid-rows-[auto_1fr] md:grid-cols-[200px_1fr] md:grid-rows-1">
      <aside
        className="flex gap-2 overflow-x-auto border-b border-base-300 bg-base-200 px-3 py-2 md:flex-col md:gap-4 md:overflow-visible md:border-b-0 md:border-r md:px-3 md:py-4"
        aria-label="Основная навигация"
      >
        <div className="px-3 py-2 text-[15px] font-bold tracking-wide text-primary">
          Throne
        </div>
        <nav className="flex gap-0.5 md:flex-col">
          {NAV_ITEMS.map(({ to, label, icon: Icon }) => (
            <NavLink key={to} to={to} className={navItemClass}>
              <Icon aria-hidden size={16} strokeWidth={2} />
              <span>{label}</span>
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
    "flex h-8 items-center gap-2.5 rounded-md px-2.5 text-sm transition-colors";
  return isActive
    ? `${base} bg-primary/10 text-primary font-semibold`
    : `${base} font-medium text-base-content/70 hover:bg-base-300/60 hover:text-base-content`;
}
