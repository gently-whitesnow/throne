import { useEffect, useState } from "react";

/**
 * Returns `value` lagged by `delayMs`. Каждый новый input сбрасывает таймер,
 * поэтому быстрая печать не порождает запросы — обновление приходит только
 * после паузы. По истечению задержки выдаёт последнее наблюдённое значение.
 */
export function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebounced(value);
    }, delayMs);
    return () => {
      window.clearTimeout(timer);
    };
  }, [value, delayMs]);

  return debounced;
}
