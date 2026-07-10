import type { TerminalVendorMetadata } from "@/entities/terminal-setting";

interface VendorQuotaBlockProps {
  vendor: TerminalVendorMetadata | undefined;
  isRefreshing: boolean;
  onRefresh: () => void;
}

/**
 * Компактный блок использованной квоты Pro/Max выбранного вендора (ADR-0054).
 * Скрыт, когда `quota=null` — backend вернул null (нет токена / ошибка пробы).
 * Кнопка «Обновить» инвалидирует каталог целиком — квоты подтянутся заново.
 */
export function VendorQuotaBlock({
  vendor,
  isRefreshing,
  onRefresh
}: VendorQuotaBlockProps) {
  const quota = vendor?.quota;

  return (
    <div className="flex flex-wrap items-center gap-2 text-[11px] text-base-content/70">
      {quota != null ? (
        <>
          <QuotaAxis label="5ч" percent={quota.five_hour.used_percent} />
          {quota.seven_day != null ? (
            <QuotaAxis label="Неделя" percent={quota.seven_day.used_percent} />
          ) : null}
          {quota.credits_balance != null ? (
            <span data-testid="agent-terminal-quota-credits">
              Кредиты:{" "}
              <span className="font-mono">
                {quota.credits_balance.toFixed(2)}
              </span>
            </span>
          ) : null}
          <ResetsAt window={quota.five_hour.resets_at ?? null} />
        </>
      ) : null}
      <button
        type="button"
        className="btn btn-xs btn-ghost"
        onClick={onRefresh}
        disabled={isRefreshing}
        data-testid="agent-terminal-quota-refresh"
      >
        {isRefreshing ? "Обновляем…" : "Обновить"}
      </button>
    </div>
  );
}

function QuotaAxis({ label, percent }: { label: string; percent: number }) {
  return (
    <span data-testid={`agent-terminal-quota-${label.toLowerCase()}`}>
      {label}: <span className="font-mono">{Math.round(percent)}%</span>
    </span>
  );
}

function ResetsAt({ window }: { window: string | null }) {
  if (window === null) return null;
  const parsed = new Date(window);
  if (Number.isNaN(parsed.getTime())) return null;
  const formatted = parsed.toLocaleString(undefined, {
    day: "2-digit",
    month: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  });
  return (
    <span
      className="text-base-content/50"
      data-testid="agent-terminal-quota-resets"
    >
      сброс {formatted}
    </span>
  );
}
