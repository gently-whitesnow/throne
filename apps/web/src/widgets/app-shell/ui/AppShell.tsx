import { FileText, Sparkles } from "lucide-react";
import { NavLink, Outlet } from "react-router-dom";

const NAV_ITEMS = [
  { to: "/intents", label: "Intents", icon: Sparkles },
  { to: "/instructions", label: "Instructions", icon: FileText }
] as const;

export function AppShell() {
  return (
    <div className="app-shell">
      <aside className="app-shell__sidebar" aria-label="Основная навигация">
        <div className="app-shell__brand">Throne</div>
        <nav className="app-shell__nav">
          {NAV_ITEMS.map(({ to, label, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) =>
                `app-shell__nav-item${isActive ? " app-shell__nav-item--active" : ""}`
              }
            >
              <Icon aria-hidden size={16} strokeWidth={2} />
              <span>{label}</span>
            </NavLink>
          ))}
        </nav>
      </aside>
      <main className="app-shell__main">
        <Outlet />
      </main>
    </div>
  );
}
