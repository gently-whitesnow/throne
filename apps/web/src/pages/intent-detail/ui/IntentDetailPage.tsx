import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";

import type { IntentDetail } from "@/entities/intent";
import { TextVersionList } from "@/entities/text-version";
import { DeleteIntentButton } from "@/features/delete-intent";
import { ReplaceIntentTextForm } from "@/features/replace-intent-text";
import { HttpError, httpGet, intentsEndpoints } from "@/shared/api";
import { Button } from "@/shared/ui";

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; intent: IntentDetail }
  | { kind: "error"; status?: number; message: string };

export function IntentDetailPage() {
  const { id = "" } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [editing, setEditing] = useState(false);
  const [historyKey, setHistoryKey] = useState(0);

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
            <div className="detail__actions">
              {!editing && (
                <Button
                  onClick={() => {
                    setEditing(true);
                  }}
                >
                  Редактировать
                </Button>
              )}
              <DeleteIntentButton
                intentId={state.intent.id}
                onDeleted={() => {
                  void navigate("/");
                }}
              />
            </div>
          </header>

          {editing ? (
            <ReplaceIntentTextForm
              intent={state.intent}
              onSaved={(next) => {
                setState({ kind: "ready", intent: next });
                setEditing(false);
                setHistoryKey((k) => k + 1);
              }}
              onCancel={() => {
                setEditing(false);
              }}
            />
          ) : (
            <pre className="detail__text">{state.intent.text}</pre>
          )}

          <section className="detail__history">
            <h2 className="detail__history-title">История</h2>
            <TextVersionList
              endpoint={intentsEndpoints.listIntentVersions(state.intent.id)}
              reloadKey={historyKey}
            />
          </section>
        </>
      )}
    </main>
  );
}
