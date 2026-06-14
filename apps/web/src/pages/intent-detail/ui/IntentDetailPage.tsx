import { Outlet, useNavigate, useParams } from "react-router-dom";

import { useIntent } from "@/entities/intent";
import { errorMessage } from "@/shared/lib";
import { useRealtimeEvent } from "@/shared/realtime";

import { IntentDetailShell } from "./IntentDetailShell";

export function IntentDetailPage() {
  const { id = "" } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const intentQuery = useIntent(id || null);

  useRealtimeEvent("intent.deleted", (payload) => {
    if (payload.intent_id === id) {
      void navigate("/intents");
    }
  });

  if (!id || intentQuery.isPending) {
    return (
      <p className="px-6 py-4 text-[13px] text-base-content/60">Загрузка…</p>
    );
  }
  if (intentQuery.isError) {
    const message = errorMessage(intentQuery.error, {
      base: "Ошибка загрузки",
      byStatus: { 404: "Intent не найден." }
    });
    return (
      <p role="alert" className="px-6 py-4 text-[13px] text-error">
        {message}
      </p>
    );
  }

  return (
    <>
      <IntentDetailShell intent={intentQuery.data} />
      {/* Вложенный роут ревьюилки рендерит fullscreen-портал поверх деталей. */}
      <Outlet />
    </>
  );
}
