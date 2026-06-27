import { Navigate } from "react-router-dom";

import { useInfiniteIntents } from "@/entities/intent";
import { useThroneReadiness } from "@/features/throne-readiness";

/**
 * Корень `/`: на first-run (пустой список интентов И Throne не готов) уводит на
 * церемонию `/start`, иначе — сразу в кокпит. Редирект мягкий: со /start всегда
 * можно уйти в кокпит, так что опытных это не запирает (Вариант A).
 */
export function HomePage() {
  const readiness = useThroneReadiness();
  // limit=1: нам нужен только факт «есть хоть один интент», не весь список.
  const probe = useInfiniteIntents({ limit: 1 });

  if (readiness.isLoading || probe.isPending) {
    return null;
  }

  const isEmpty = probe.data?.pages[0]?.items.length === 0;
  const firstRun = isEmpty && !readiness.ready;

  return <Navigate to={firstRun ? "/start" : "/intents"} replace />;
}
