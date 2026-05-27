import { QueryClient } from "@tanstack/react-query";

import { HttpError } from "@/shared/api";

/**
 * QueryClient — единая точка кеширования сетевых запросов на фронте. Дефолты:
 * - staleTime 30s: запросы того же ключа в этом окне отдаются из кеша без сети.
 *   Отдельные хуки могут поднимать его для редко-меняющихся ресурсов (`/tags`).
 * - gcTime 5min: данные неактивных query сохраняются до сборки мусора, чтобы
 *   быстрое возвращение на экран не давало blank-state.
 * - refetchOnWindowFocus отключён: realtime инвалидация делает фокус-рефетч
 *   избыточным; точечно включается там, где realtime не покрывает источник.
 * - retry: на 4xx HttpError не повторяем (контрактные ошибки чинит код, не
 *   повтор), на остальном — до двух повторов.
 */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        gcTime: 5 * 60_000,
        refetchOnWindowFocus: false,
        retry: (failureCount, error) => {
          if (error instanceof HttpError) {
            if (error.status >= 400 && error.status < 500) return false;
          }
          return failureCount < 2;
        }
      }
    }
  });
}
