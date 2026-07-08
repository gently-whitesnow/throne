import { errorMessage, httpErrorStatus } from "@/shared/lib";

/**
 * Maps the board-card endpoints' degradation codes (ADR-0052) to friendly RU
 * copy so a blocked tariff / disconnected tracker / offline provider never
 * renders a white screen. Falls back to the generic error message otherwise.
 * `404` only reaches the single-card read (card gone / not on this board).
 */
export function boardCardsErrorMessage(error: unknown): string {
  switch (httpErrorStatus(error)) {
    case 402:
      return "Тариф не позволяет просматривать карточки трекера.";
    case 409:
      return "Трекер не подключён или токен отклонён. Переподключите его в настройках.";
    case 422:
      return "Этот трекер пока не поддерживает просмотр карточек.";
    case 502:
      return "Трекер недоступен. Попробуйте обновить позже.";
    case 404:
      return "Карточка недоступна или её больше нет на этой доске.";
    default:
      return errorMessage(error, { base: "Не удалось загрузить карточки" });
  }
}
