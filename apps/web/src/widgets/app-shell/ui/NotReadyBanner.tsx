import { AlertTriangle, ArrowRight } from "lucide-react";
import { Link, useLocation } from "react-router-dom";

import { useThroneReadiness } from "@/features/throne-readiness";

/**
 * Sticky-полоса в кокпите, пока Throne не готов — страховка для тех, кто ушёл с
 * `/start` недонастроенным. Сама на `/start` не показывается (там уже есть полный
 * чеклист) и исчезает по достижении зелёного.
 */
export function NotReadyBanner() {
  const { ready, items, isLoading } = useThroneReadiness();
  const location = useLocation();

  if (isLoading || ready || location.pathname === "/start") {
    return null;
  }

  const okCount = items.filter((i) => i.ok).length;
  const total = items.length;

  return (
    <Link
      to="/start"
      data-testid="not-ready-banner"
      className="sticky top-0 z-10 flex items-center gap-2.5 border-b border-warning/40 bg-warning/15 px-4 py-2 text-sm text-warning-content/90 transition-colors hover:bg-warning/25"
    >
      <AlertTriangle
        aria-hidden
        size={16}
        strokeWidth={2}
        className="text-warning"
      />
      <span className="font-medium">
        Throne не готов к запуску агента: {String(okCount)} из {String(total)}.
      </span>
      <span className="inline-flex items-center gap-1 font-semibold text-warning">
        Завершить настройку
        <ArrowRight aria-hidden size={14} strokeWidth={2} />
      </span>
    </Link>
  );
}
