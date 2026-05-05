import { RefreshCw, X } from "lucide-react";
import { useEffect, useId, useState } from "react";
import { createPortal } from "react-dom";

import type { McpTokenIssued } from "@/entities/mcp-token";
import { issueMcpToken } from "@/entities/mcp-token";
import { HttpError } from "@/shared/api";
import { Button } from "@/shared/ui";

interface RegenerateMcpTokenButtonProps {
  onIssued: (issued: McpTokenIssued) => void;
}

export function RegenerateMcpTokenButton({
  onIssued
}: RegenerateMcpTokenButtonProps) {
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const titleId = useId();
  const descriptionId = useId();

  useEffect(() => {
    if (!confirmOpen) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !busy) {
        event.preventDefault();
        setConfirmOpen(false);
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => {
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [busy, confirmOpen]);

  const confirm = async () => {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      const issued = await issueMcpToken();
      setConfirmOpen(false);
      onIssued(issued);
    } catch (err: unknown) {
      const message =
        err instanceof HttpError
          ? `Не удалось перевыпустить токен (${String(err.status)}).`
          : "Не удалось перевыпустить токен.";
      setError(message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <Button
        icon={<RefreshCw aria-hidden size={16} strokeWidth={2} />}
        onClick={() => {
          setError(null);
          setConfirmOpen(true);
        }}
      >
        Перевыпустить
      </Button>
      {confirmOpen
        ? createPortal(
            <div
              className="modal modal-open modal-bottom sm:modal-middle"
              role="presentation"
              onClick={() => {
                if (!busy) setConfirmOpen(false);
              }}
            >
              <div
                className="modal-box max-w-md border border-base-300 bg-base-100"
                role="dialog"
                aria-modal="true"
                aria-labelledby={titleId}
                aria-describedby={descriptionId}
                onClick={(event) => {
                  event.stopPropagation();
                }}
              >
                <div className="mb-4 flex items-start justify-between gap-4">
                  <div className="flex flex-col gap-1.5">
                    <h3
                      id={titleId}
                      className="m-0 text-lg font-semibold leading-tight"
                    >
                      Перевыпустить MCP-токен?
                    </h3>
                    <p
                      id={descriptionId}
                      className="m-0 text-sm text-base-content/70"
                    >
                      Старый токен немедленно перестанет работать. Все
                      подключённые MCP-клиенты придётся перенастроить.
                    </p>
                  </div>
                  <button
                    type="button"
                    className="btn btn-sm btn-circle btn-ghost"
                    onClick={() => {
                      setConfirmOpen(false);
                    }}
                    aria-label="Отменить перевыпуск"
                    disabled={busy}
                  >
                    <X aria-hidden size={16} strokeWidth={2} />
                  </button>
                </div>
                {error ? (
                  <p role="alert" className="m-0 mb-3 text-sm text-error">
                    {error}
                  </p>
                ) : null}
                <div className="flex justify-end gap-2 max-sm:flex-col-reverse [&>*]:max-sm:w-full">
                  <Button
                    onClick={() => {
                      setConfirmOpen(false);
                    }}
                    disabled={busy}
                  >
                    Отмена
                  </Button>
                  <Button
                    variant="primary"
                    disabled={busy}
                    onClick={() => {
                      void confirm();
                    }}
                  >
                    {busy ? "Перевыпускаем…" : "Да, перевыпустить"}
                  </Button>
                </div>
              </div>
            </div>,
            document.body
          )
        : null}
    </>
  );
}
