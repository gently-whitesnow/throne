import { HttpError } from "@/shared/api";

/** HTTP-статус ошибки или undefined, если это не HttpError. */
export function httpErrorStatus(err: unknown): number | undefined {
  return err instanceof HttpError ? err.status : undefined;
}

/** Машинный `code` из problem+json или undefined. */
export function httpErrorCode(err: unknown): string | undefined {
  return err instanceof HttpError ? err.code : undefined;
}

export function httpErrorDetail(err: unknown): string | undefined {
  if (!(err instanceof HttpError)) return undefined;
  const detail = err.extensions.detail;
  return typeof detail === "string" && detail.length > 0 ? detail : undefined;
}

export function httpErrorTitle(err: unknown): string | undefined {
  if (!(err instanceof HttpError)) return undefined;
  const title = err.extensions.title;
  return typeof title === "string" && title.length > 0 ? title : undefined;
}

export function httpErrorExtension(err: unknown, key: string): unknown {
  return err instanceof HttpError ? err.extensions[key] : undefined;
}

export function httpErrorExtensionString(
  err: unknown,
  key: string
): string | undefined {
  const value = httpErrorExtension(err, key);
  return typeof value === "string" && value.length > 0 ? value : undefined;
}

export interface ErrorMessageOptions {
  /** Базовая фраза без завершающей точки, напр. «Не удалось привязать». */
  base: string;
  /** Сообщения под конкретные статусы — перекрывают base. */
  byStatus?: Record<number, string>;
  /** Дописывать ` (status).` к base для HttpError. По умолчанию true. */
  withStatus?: boolean;
  /** Фраза для не-HttpError. По умолчанию `${base}.`. */
  fallback?: string;
}

/**
 * Единая точка перевода ошибки запроса в человекочитаемое сообщение. Заменяет
 * россыпь `err instanceof HttpError ? … : …` в features/widgets/pages, где
 * `instanceof HttpError` запрещён ESLint — статус читается через хелперы здесь.
 */
export function errorMessage(
  err: unknown,
  { base, byStatus, withStatus = true, fallback }: ErrorMessageOptions
): string {
  if (err instanceof HttpError) {
    const specific = byStatus?.[err.status];
    if (specific !== undefined) return specific;
    return withStatus ? `${base} (${String(err.status)}).` : `${base}.`;
  }
  return fallback ?? `${base}.`;
}
