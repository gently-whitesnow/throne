import { History } from "lucide-react";
import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";

import { intentStatusMeta, type IntentDetail } from "@/entities/intent";
import { DeleteIntentButton } from "@/features/delete-intent";
import { IntentAttachmentsPanel } from "@/features/manage-intent-attachments";
import { ReplaceIntentTextForm } from "@/features/replace-intent-text";
import { SetIntentStatusForm } from "@/features/set-intent-status";
import { HttpError, httpGet, intentsEndpoints } from "@/shared/api";
import { Button } from "@/shared/ui";
import { VersionsDrawer } from "@/widgets/versions-drawer";

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
  const [versionsOpen, setVersionsOpen] = useState(false);

  useEffect(() => {
    if (!id) return;
    const controller = new AbortController();
    setState({ kind: "loading" });
    setEditing(false);
    setVersionsOpen(false);
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

  if (state.kind === "loading") {
    return <p className="detail__hint">Загрузка…</p>;
  }
  if (state.kind === "error") {
    return (
      <p role="alert" className="detail__hint">
        {state.message}
      </p>
    );
  }

  const intent = state.intent;
  const title = firstLine(intent.text) || intent.id;
  const status = intentStatusMeta[intent.status];

  return (
    <>
      <header className="detail__header">
        <div className="detail__heading">
          <h1 className="detail__title">{title}</h1>
          <div className="detail__meta">
            <span
              className="detail__status"
              style={{ background: status.surface, color: status.ink }}
            >
              {status.label}
            </span>
            <span className="detail__meta-item">v{intent.current_version}</span>
            {intent.tags.length > 0 ? (
              <span className="detail__meta-item">
                #{intent.tags.join(" #")}
              </span>
            ) : null}
            <span className="detail__meta-item detail__meta-item--muted">
              {new Date(intent.updated_at).toLocaleString()}
            </span>
          </div>
        </div>
        <div className="detail__actions">
          <Button
            icon={<History aria-hidden size={14} strokeWidth={2} />}
            onClick={() => {
              setVersionsOpen(true);
            }}
          >
            История
          </Button>
          {!editing && (
            <Button
              variant="primary"
              onClick={() => {
                setEditing(true);
              }}
            >
              Редактировать
            </Button>
          )}
          <DeleteIntentButton
            intentId={intent.id}
            onDeleted={() => {
              void navigate("/intents");
            }}
          />
        </div>
      </header>

      <div className="detail__body">
        <SetIntentStatusForm
          intent={intent}
          onSaved={(next) => {
            setState({ kind: "ready", intent: next });
            setHistoryKey((k) => k + 1);
          }}
        />
        {editing ? (
          <ReplaceIntentTextForm
            intent={intent}
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
          <pre className="detail__text">{intent.text}</pre>
        )}
        <IntentAttachmentsPanel intentId={intent.id} />
      </div>

      <VersionsDrawer
        open={versionsOpen}
        endpoint={intentsEndpoints.listIntentVersions(intent.id)}
        reloadKey={historyKey}
        onClose={() => {
          setVersionsOpen(false);
        }}
      />
    </>
  );
}

function firstLine(text: string): string {
  const line = text.split(/\r?\n/, 1)[0] ?? "";
  return line.length > 80 ? `${line.slice(0, 80)}…` : line;
}
