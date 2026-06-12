import { HttpError } from "@/shared/api";

export function formatCreateError(err: unknown): string {
  if (err instanceof HttpError) {
    if (err.status === 409) return "Часть с таким key уже есть.";
    if (err.status === 422) return "Валидация не прошла (проверьте key/текст).";
    return `Ошибка создания (${String(err.status)}).`;
  }
  return "Не удалось создать часть.";
}

export function formatReplaceError(err: unknown): string {
  if (err instanceof HttpError) {
    if (err.status === 404) return "Часть не найдена.";
    if (err.status === 409)
      return "Версия устарела — обновите страницу и повторите правку.";
    if (err.status === 422) return "Текст не совпал — нечего заменять.";
    return `Ошибка сохранения (${String(err.status)}).`;
  }
  return "Не удалось сохранить.";
}

export function formatDeleteError(err: unknown): string {
  if (err instanceof HttpError) {
    if (err.status === 404) return "Часть уже удалена.";
    if (err.status === 409)
      return "Сначала снимите все роли части во всех режимах, затем удалите.";
    return `Ошибка удаления (${String(err.status)}).`;
  }
  return "Не удалось удалить часть.";
}
