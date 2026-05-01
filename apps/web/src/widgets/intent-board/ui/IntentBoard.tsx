import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

import { IntentApiCard, type IntentListItem } from "@/entities/intent";
import { CreateIntentButton } from "@/features/create-intent";
import { HttpError, httpGet, intentsEndpoints } from "@/shared/api";

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; items: IntentListItem[] }
  | { kind: "error"; message: string };

export function IntentBoard() {
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    httpGet<IntentListItem[]>(intentsEndpoints.listIntents(), controller.signal)
      .then((items) => {
        setState({ kind: "ready", items });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        const message =
          err instanceof HttpError
            ? `Не удалось загрузить intents (${String(err.status)}).`
            : "Не удалось загрузить intents.";
        setState({ kind: "error", message });
      });
    return () => {
      controller.abort();
    };
  }, [reloadKey]);

  const reload = () => {
    setReloadKey((v) => v + 1);
  };

  return (
    <section className="intent-board" aria-labelledby="intent-board-title">
      <div className="intent-board__toolbar">
        <h2 className="intent-board__title" id="intent-board-title">
          Intent cloud
        </h2>
        <CreateIntentButton onCreated={reload} />
      </div>
      {state.kind === "loading" && <p>Загрузка…</p>}
      {state.kind === "error" && <p role="alert">{state.message}</p>}
      {state.kind === "ready" && state.items.length === 0 && (
        <p>Нет intents. Создайте первый.</p>
      )}
      {state.kind === "ready" && state.items.length > 0 && (
        <div className="intent-board__grid">
          {state.items.map((intent) => (
            <Link
              key={intent.id}
              to={`/intents/${intent.id}`}
              className="intent-board__link"
            >
              <IntentApiCard intent={intent} />
            </Link>
          ))}
        </div>
      )}
    </section>
  );
}
