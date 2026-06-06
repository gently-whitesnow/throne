import { useNavigate, useParams } from "react-router-dom";

import { useIntent } from "@/entities/intent";
import { HttpError } from "@/shared/api";
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
    const err = intentQuery.error;
    const message =
      err instanceof HttpError
        ? err.status === 404
          ? "Intent не найден."
          : `Ошибка загрузки (${String(err.status)}).`
        : "Ошибка загрузки.";
    return (
      <p role="alert" className="px-6 py-4 text-[13px] text-error">
        {message}
      </p>
    );
  }

  return <IntentDetailShell intent={intentQuery.data} />;
}
