interface SessionLiveBadgeProps {
  /** Видимая подпись справа от точки. Без неё рендерим только точку (компактный режим для списка/канваса). */
  label?: string;
  /** Текст подсказки/скринридера; по умолчанию «Сессия запущена». */
  title?: string;
  className?: string;
  testId?: string;
}

const DEFAULT_TITLE = "Сессия запущена";

/**
 * Единый индикатор live-сессии терминала: пульсирующая зелёная точка с
 * опциональной подписью. Одна семантика на трёх площадках — шапка панели
 * терминала, строка списка интентов, карточка на канвасе.
 */
export function SessionLiveBadge({
  label,
  title,
  className,
  testId
}: SessionLiveBadgeProps) {
  const tooltip = title ?? DEFAULT_TITLE;
  return (
    <span
      data-testid={testId}
      className={["inline-flex items-center gap-1.5", className]
        .filter(Boolean)
        .join(" ")}
      title={tooltip}
    >
      <span aria-hidden className="relative flex h-2 w-2 shrink-0">
        <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-success/60" />
        <span className="relative inline-flex h-2 w-2 rounded-full bg-success" />
      </span>
      {label ? (
        <span>{label}</span>
      ) : (
        <span className="sr-only">{tooltip}</span>
      )}
    </span>
  );
}
