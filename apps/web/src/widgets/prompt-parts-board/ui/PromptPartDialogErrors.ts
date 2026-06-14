import { errorMessage } from "@/shared/lib";

export function formatCreateError(err: unknown): string {
  return errorMessage(err, {
    base: "Ошибка создания",
    byStatus: {
      409: "Часть с таким key уже есть.",
      422: "Валидация не прошла (проверьте key/текст)."
    },
    fallback: "Не удалось создать часть."
  });
}

export function formatReplaceError(err: unknown): string {
  return errorMessage(err, {
    base: "Ошибка сохранения",
    byStatus: {
      404: "Часть не найдена.",
      409: "Версия устарела — обновите страницу и повторите правку.",
      422: "Текст не совпал — нечего заменять."
    },
    fallback: "Не удалось сохранить."
  });
}

export function formatDeleteError(err: unknown): string {
  return errorMessage(err, {
    base: "Ошибка удаления",
    byStatus: {
      404: "Часть уже удалена.",
      409: "Сначала снимите все роли части во всех режимах, затем удалите."
    },
    fallback: "Не удалось удалить часть."
  });
}
