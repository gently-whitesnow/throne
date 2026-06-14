import { HttpError } from "@/shared/api";

/** HTTP-статус ошибки или undefined, если это не HttpError. */
export function httpErrorStatus(err: unknown): number | undefined {
  return err instanceof HttpError ? err.status : undefined;
}

/** Машинный `code` из problem+json или undefined. */
export function httpErrorCode(err: unknown): string | undefined {
  return err instanceof HttpError ? err.code : undefined;
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
