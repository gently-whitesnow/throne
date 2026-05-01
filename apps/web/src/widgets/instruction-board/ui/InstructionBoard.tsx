import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

import {
  InstructionCard,
  type InstructionListItem
} from "@/entities/instruction";
import { HttpError, httpGet, instructionsEndpoints } from "@/shared/api";

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; items: InstructionListItem[] }
  | { kind: "error"; message: string };

export function InstructionBoard() {
  const [state, setState] = useState<LoadState>({ kind: "loading" });

  useEffect(() => {
    const controller = new AbortController();
    httpGet<InstructionListItem[]>(
      instructionsEndpoints.listInstructions(),
      controller.signal
    )
      .then((items) => {
        setState({ kind: "ready", items });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        const message =
          err instanceof HttpError
            ? `Не удалось загрузить instructions (${String(err.status)}).`
            : "Не удалось загрузить instructions.";
        setState({ kind: "error", message });
      });
    return () => {
      controller.abort();
    };
  }, []);

  return (
    <section className="intent-board" aria-labelledby="instruction-board-title">
      <div className="intent-board__toolbar">
        <h2 className="intent-board__title" id="instruction-board-title">
          Instruction cloud
        </h2>
      </div>
      {state.kind === "loading" && <p>Загрузка…</p>}
      {state.kind === "error" && <p role="alert">{state.message}</p>}
      {state.kind === "ready" && state.items.length === 0 && (
        <p>Инструкции отсутствуют — seed bootstrap не отработал.</p>
      )}
      {state.kind === "ready" && state.items.length > 0 && (
        <div className="intent-board__grid">
          {state.items.map((instruction) => (
            <Link
              key={instruction.id}
              to={`/instructions/${instruction.id}`}
              className="intent-board__link"
            >
              <InstructionCard instruction={instruction} />
            </Link>
          ))}
        </div>
      )}
    </section>
  );
}
