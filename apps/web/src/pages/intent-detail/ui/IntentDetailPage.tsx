import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";

import type { IntentDetail } from "@/entities/intent";
import { HttpError, httpGet, intentsEndpoints } from "@/shared/api";

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; intent: IntentDetail }
  | { kind: "error"; status?: number; message: string };

export function IntentDetailPage() {
  const { id = "" } = useParams<{ id: string }>();
  const [state, setState] = useState<LoadState>({ kind: "loading" });

  useEffect(() => {
    if (!id) return;
    const controller = new AbortController();
    setState({ kind: "loading" });
    httpGet<IntentDetail>(intentsEndpoints.getIntent(id), controller.signal)
      .then((intent) => {
        setState({ kind: "ready", intent });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        if (err instanceof HttpError) {
          setState({
            kind: "error",
            status: err.status,
            message:
              err.status === 404
                ? "Intent не найден."
                : `Ошибка загрузки (${String(err.status)}).`
          });
          return;
        }
        setState({ kind: "error", message: "Ошибка загрузки." });
      });
    return () => {
      controller.abort();
    };
  }, [id]);

  return (
    <main className="page-shell home-page">
      <p className="home-page__eyebrow">
        <Link to="/" className="home-page__back">
          ← Главная
        </Link>
      </p>
      {state.kind === "loading" && <p>Загрузка…</p>}
      {state.kind === "error" && <p role="alert">{state.message}</p>}
      {state.kind === "ready" && (
        <>
          <header className="home-page__header">
            <h1 className="home-page__title">Intent {state.intent.id}</h1>
            <p className="home-page__lead">
              Версия v{state.intent.current_version}
              {state.intent.tags.length > 0
                ? ` · #${state.intent.tags.join(" #")}`
                : null}
            </p>
          </header>
          <pre className="detail__text">{state.intent.text}</pre>
        </>
      )}
    </main>
  );
}
