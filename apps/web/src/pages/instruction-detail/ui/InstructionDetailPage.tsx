import { History } from "lucide-react";
import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";

import {
  instructionKindLabel,
  type InstructionDetail
} from "@/entities/instruction";
import { ReplaceInstructionTextForm } from "@/features/replace-instruction-text";
import { HttpError, httpGet, instructionsEndpoints } from "@/shared/api";
import { Button } from "@/shared/ui";
import { VersionsDrawer } from "@/widgets/versions-drawer";

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; instruction: InstructionDetail }
  | { kind: "error"; status?: number; message: string };

export function InstructionDetailPage() {
  const { id = "" } = useParams<{ id: string }>();
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

  const instruction = state.instruction;
  const meta = instructionKindLabel(instruction.kind);

  return (
    <>
      <header className="detail__header">
        <div className="detail__heading">
          <h1 className="detail__title">{meta.label}</h1>
          <div className="detail__meta">
            <span className="detail__meta-item">{instruction.kind}</span>
            <span className="detail__meta-item">
              v{instruction.current_version}
            </span>
            <span className="detail__meta-item detail__meta-item--muted">
              {new Date(instruction.updated_at).toLocaleString()}
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
        </div>
      </header>

      <div className="detail__body">
        {editing ? (
          <ReplaceInstructionTextForm
            instruction={instruction}
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
          <pre className="detail__text">{instruction.text}</pre>
        )}
      </div>

      <VersionsDrawer
        open={versionsOpen}
        endpoint={instructionsEndpoints.listInstructionVersions(instruction.id)}
        reloadKey={historyKey}
        onClose={() => {
          setVersionsOpen(false);
        }}
      />
    </>
  );
}
