import { CheckCircle2, ExternalLink, Loader2, XCircle } from "lucide-react";

import {
  useThroneReadiness,
  type ReadinessItem
} from "@/features/throne-readiness";

/**
 * Верхний блок `/settings` — агрегированный статус «Throne готов» плюс чеклист
 * из четырёх критериев. Источник правды — `useThroneReadiness` (feature),
 * чтобы AppShell-бейдж и эта панель считали готовность одинаково.
 */
export function ReadinessPanel() {
  const { ready, items, isLoading } = useThroneReadiness();

  if (isLoading) {
    return (
      <section
        data-testid="readiness-panel"
        className="flex items-center gap-2 rounded-xl border border-base-300 bg-base-100 px-5 py-4 text-sm text-base-content/60"
      >
        <Loader2
          aria-hidden
          size={16}
          className="animate-spin"
          strokeWidth={2}
        />
        Проверяем готовность Throne…
      </section>
    );
  }

  const okCount = items.filter((i) => i.ok).length;
  const total = items.length;

  return (
    <section
      data-testid="readiness-panel"
      data-ready={ready}
      aria-label="Готовность Throne"
      className={
        ready
          ? "flex flex-col gap-4 rounded-xl border border-success/30 bg-success/10 px-5 py-4"
          : "flex flex-col gap-4 rounded-xl border border-warning/40 bg-warning/10 px-5 py-4"
      }
    >
      <header className="flex items-center gap-3">
        {ready ? (
          <CheckCircle2
            aria-hidden
            size={28}
            strokeWidth={2}
            className="text-success"
          />
        ) : (
          <XCircle
            aria-hidden
            size={28}
            strokeWidth={2}
            className="text-warning"
          />
        )}
        <h2
          className={
            ready
              ? "m-0 text-xl font-bold leading-tight text-success"
              : "m-0 text-xl font-bold leading-tight text-warning"
          }
        >
          {ready
            ? "Throne готов"
            : `Не готов: ${String(okCount)} из ${String(total)}`}
        </h2>
      </header>

      <ul className="m-0 flex list-none flex-col gap-2 p-0">
        {items.map((item) => (
          <ReadinessRow key={item.key} item={item} />
        ))}
      </ul>
    </section>
  );
}

function ReadinessRow({ item }: { item: ReadinessItem }) {
  return (
    <li
      data-testid={`readiness-item-${item.key}`}
      data-ok={item.ok}
      className="flex items-start gap-3 rounded-lg bg-base-100/60 px-3 py-2"
    >
      {item.ok ? (
        <CheckCircle2
          aria-hidden
          size={18}
          strokeWidth={2}
          className="mt-0.5 shrink-0 text-success"
        />
      ) : (
        <XCircle
          aria-hidden
          size={18}
          strokeWidth={2}
          className="mt-0.5 shrink-0 text-error"
        />
      )}
      <div className="flex min-w-0 flex-col gap-0.5">
        <span className="text-sm font-semibold leading-tight">
          {item.label}
        </span>
        <span
          className={
            item.ok
              ? "text-xs leading-relaxed text-base-content/70"
              : "text-xs leading-relaxed text-error"
          }
        >
          {item.detail}
        </span>
        {!item.ok && item.hintHref !== undefined ? (
          <a
            href={item.hintHref}
            target="_blank"
            rel="noopener noreferrer"
            className="mt-1 inline-flex items-center gap-1 text-xs font-medium text-primary hover:underline"
          >
            Как установить
            <ExternalLink aria-hidden size={12} strokeWidth={2} />
          </a>
        ) : null}
      </div>
    </li>
  );
}
