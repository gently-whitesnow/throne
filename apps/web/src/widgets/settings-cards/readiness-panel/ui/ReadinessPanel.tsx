import {
  Check,
  CheckCircle2,
  Copy,
  ExternalLink,
  Loader2,
  RefreshCw,
  XCircle
} from "lucide-react";
import { useState } from "react";

import {
  useThroneReadiness,
  type ReadinessItem,
  type ReadinessRemedy
} from "@/features/throne-readiness";

/**
 * Агрегированный статус «Throne готов» + чеклист полного пути до Run (агент,
 * tmux, git, workspace). Источник правды — `useThroneReadiness` (feature), чтобы
 * AppShell-бейдж, эта панель и экран /start считали готовность одинаково. У
 * невыполненных пунктов — copy-paste команда фикса (паритетные провайдеры —
 * вкладками); кнопка «Перепроверить» перезапускает пробы вживую после правки.
 */
export function ReadinessPanel() {
  const { ready, items, isLoading, refresh } = useThroneReadiness();

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
        <button
          type="button"
          onClick={refresh}
          className="ml-auto inline-flex items-center gap-1.5 rounded-lg border border-base-300 bg-base-100 px-3 py-1.5 text-xs font-medium text-base-content/80 transition-colors hover:bg-base-200 focus-visible:outline-2 focus-visible:outline-primary active:scale-[0.97]"
          title="Перезапустить проверки"
        >
          <RefreshCw aria-hidden size={14} strokeWidth={2} />
          Перепроверить
        </button>
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
  const remedies = item.ok ? undefined : item.remedies;
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
      <div className="flex min-w-0 flex-1 flex-col gap-0.5">
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
        {remedies !== undefined && remedies.length > 0 ? (
          <Remediation remedies={remedies} />
        ) : null}
      </div>
    </li>
  );
}

function Remediation({ remedies }: { remedies: ReadinessRemedy[] }) {
  const [active, setActive] = useState(0);
  const current = remedies[active] ?? remedies[0];
  const tabbed = remedies.length > 1;

  return (
    <div className="mt-1.5 flex flex-col gap-1.5">
      {tabbed ? (
        <div role="tablist" aria-label="Чем закрыть" className="flex gap-1">
          {remedies.map((r, i) => (
            <button
              key={r.label}
              type="button"
              role="tab"
              aria-selected={i === active}
              onClick={() => {
                setActive(i);
              }}
              className={
                i === active
                  ? "rounded-md bg-base-content/10 px-2.5 py-1 text-xs font-semibold text-base-content"
                  : "rounded-md px-2.5 py-1 text-xs font-medium text-base-content/55 hover:text-base-content"
              }
            >
              {r.label}
            </button>
          ))}
        </div>
      ) : null}
      <CommandBlock command={current.command} />
      <a
        href={current.hintHref}
        target="_blank"
        rel="noopener noreferrer"
        className="inline-flex items-center gap-1 text-xs font-medium text-primary hover:underline"
      >
        Документация
        <ExternalLink aria-hidden size={12} strokeWidth={2} />
      </a>
    </div>
  );
}

function CommandBlock({ command }: { command: string }) {
  const [copied, setCopied] = useState(false);

  const copy = () => {
    void navigator.clipboard.writeText(command).then(() => {
      setCopied(true);
      window.setTimeout(() => {
        setCopied(false);
      }, 1500);
    });
  };

  return (
    <div className="flex items-center gap-2 rounded-md border border-base-300 bg-base-300/40 px-2.5 py-1.5">
      <code className="min-w-0 flex-1 overflow-x-auto whitespace-nowrap font-mono text-xs text-base-content/90">
        {command}
      </code>
      <button
        type="button"
        onClick={copy}
        className="inline-flex shrink-0 items-center gap-1 rounded px-1.5 py-1 text-[11px] font-medium text-base-content/70 transition-colors hover:bg-base-100 hover:text-base-content focus-visible:outline-2 focus-visible:outline-primary"
        title="Скопировать команду"
        aria-label="Скопировать команду"
      >
        {copied ? (
          <>
            <Check
              aria-hidden
              size={13}
              strokeWidth={2}
              className="text-success"
            />
            Скопировано
          </>
        ) : (
          <>
            <Copy aria-hidden size={13} strokeWidth={2} />
            Копировать
          </>
        )}
      </button>
    </div>
  );
}
