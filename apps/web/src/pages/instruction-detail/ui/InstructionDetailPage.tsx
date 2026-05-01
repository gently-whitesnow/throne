import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";

import {
  instructionKindLabel,
  type InstructionDetail
} from "@/entities/instruction";
import { TextVersionList } from "@/entities/text-version";
import { ReplaceInstructionTextForm } from "@/features/replace-instruction-text";
import { HttpError, httpGet, instructionsEndpoints } from "@/shared/api";
import { Button } from "@/shared/ui";

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; instruction: InstructionDetail }
  | { kind: "error"; status?: number; message: string };

export function InstructionDetailPage() {
  const { id = "" } = useParams<{ id: string }>();
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [editing, setEditing] = useState(false);
  const [historyKey, setHistoryKey] = useState(0);

  useEffect(() => {
    if (!id) return;
    const controller = new AbortController();
    setState({ kind: "loading" });
    httpGet<InstructionDetail>(
      instructionsEndpoints.getInstruction(id),
      controller.signal
    )
      .then((instruction) => {
        setState({ kind: "ready", instruction });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        if (err instanceof HttpError) {
          setState({
            kind: "error",
            status: err.status,
            message:
              err.status === 404
                ? "Instruction не найдена."
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
            <h1 className="home-page__title">
              {instructionKindLabel(state.instruction.kind).label}
            </h1>
            <p className="home-page__lead">
              {state.instruction.kind} · v{state.instruction.current_version}
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
            </div>
          </header>

          {editing ? (
            <ReplaceInstructionTextForm
              instruction={state.instruction}
              onSaved={(next) => {
                setState({ kind: "ready", instruction: next });
                setEditing(false);
                setHistoryKey((k) => k + 1);
              }}
              onCancel={() => {
                setEditing(false);
              }}
            />
          ) : (
            <pre className="detail__text">{state.instruction.text}</pre>
          )}

          <section className="detail__history">
            <h2 className="detail__history-title">История</h2>
            <TextVersionList
              endpoint={instructionsEndpoints.listInstructionVersions(
                state.instruction.id
              )}
              reloadKey={historyKey}
            />
          </section>
        </>
      )}
    </main>
  );
}
