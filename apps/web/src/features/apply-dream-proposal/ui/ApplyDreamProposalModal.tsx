import { useState } from "react";

import type { DreamProposal } from "@/entities/dream-proposal";
import type { DreamRun } from "@/entities/dream-run";
import { dreamEndpoints, HttpError, httpPost } from "@/shared/api";
import { Button } from "@/shared/ui";

interface Props {
  runId: string;
  proposal: DreamProposal;
  baseVersionMatchesCurrent: boolean;
  currentInstructionVersion: number;
  onApplied: (run: DreamRun) => void;
  onClose: () => void;
}

export function ApplyDreamProposalModal({
  runId,
  proposal,
  baseVersionMatchesCurrent,
  currentInstructionVersion,
  onApplied,
  onClose
}: Props) {
  const [draft, setDraft] = useState(proposal.proposed_rule);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [needsRebase, setNeedsRebase] = useState(!baseVersionMatchesCurrent);

  const submit = async () => {
    if (busy) return;
    if (needsRebase) return;
    setBusy(true);
    setError(null);
    try {
      const trimmed = draft.trim();
      const final_rule =
        trimmed && trimmed !== proposal.proposed_rule.trim()
          ? trimmed
          : undefined;
      const next = await httpPost<DreamRun>(
        dreamEndpoints.applyDreamProposal(runId, proposal.id),
        { final_rule }
      );
      onApplied(next);
    } catch (err: unknown) {
      if (err instanceof HttpError) {
        if (err.status === 409 && err.code === "dream.proposal.needs_rebase") {
          setNeedsRebase(true);
          setError(
            "Базовая версия инструкции изменилась. Запустите свежий /tdream."
          );
        } else if (err.status === 409) {
          setError(
            "Конфликт состояния (proposal уже обработан или run закрыт)."
          );
        } else if (err.status === 422) {
          setError("Не удалось применить (валидация).");
        } else {
          setError(`Ошибка применения (${String(err.status)}).`);
        }
      } else {
        setError("Не удалось применить.");
      }
    } finally {
      setBusy(false);
    }
  };

  const overLimit = draft.length > 280;

  return (
    <dialog open className="modal modal-open">
      <div className="modal-box max-w-xl">
        <h3 className="m-0 mb-3 text-lg font-bold">Применить dream proposal</h3>
        <p className="m-0 mb-3 text-sm text-base-content/70">
          Правило будет добавлено в секцию{" "}
          <code className="rounded bg-base-200 px-1.5 py-px font-mono text-xs">
            ## Learned rules
          </code>{" "}
          инструкции <span className="font-mono">{proposal.target_kind}</span>.
        </p>
        <label className="label flex flex-col items-start gap-1">
          <span className="label-text text-xs uppercase tracking-wide text-base-content/60">
            Final rule
          </span>
          <textarea
            className="textarea textarea-bordered w-full font-mono text-[13px] leading-relaxed"
            value={draft}
            onChange={(e) => {
              setDraft(e.target.value);
            }}
            rows={4}
            aria-label="Final rule"
            disabled={busy || needsRebase}
          />
        </label>
        <div className="mt-1 flex items-center justify-between text-xs text-base-content/60">
          <span>
            Base version proposal:{" "}
            <strong>{proposal.base_instruction_version}</strong> · Current:{" "}
            <strong>{currentInstructionVersion}</strong>
          </span>
          <span className={overLimit ? "text-error" : ""}>
            {String(draft.length)} / 280
          </span>
        </div>
        {error ? (
          <p role="alert" className="mt-3 text-sm text-error">
            {error}
          </p>
        ) : null}
        <div className="modal-action mt-4">
          {needsRebase ? (
            <Button type="button" onClick={onClose} variant="primary">
              Закрыть
            </Button>
          ) : (
            <>
              <Button type="button" onClick={onClose} disabled={busy}>
                Отмена
              </Button>
              <Button
                type="button"
                onClick={() => {
                  void submit();
                }}
                variant="primary"
                disabled={busy || overLimit || draft.trim().length === 0}
              >
                {busy ? "Применяем…" : "Применить"}
              </Button>
            </>
          )}
        </div>
      </div>
      <button
        type="button"
        aria-label="Закрыть"
        className="modal-backdrop"
        onClick={onClose}
      />
    </dialog>
  );
}
