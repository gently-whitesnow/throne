import { useState } from "react";

import type { DreamProposal } from "@/entities/dream-proposal";
import type { DreamRun } from "@/entities/dream-run";
import { dreamEndpoints, HttpError, httpPost } from "@/shared/api";
import { Button } from "@/shared/ui";

interface Props {
  runId: string;
  proposal: DreamProposal;
  onSkipped: (run: DreamRun) => void;
  onClose: () => void;
}

const MIN_REASON_LENGTH = 5;

export function SkipDreamProposalModal({
  runId,
  proposal,
  onSkipped,
  onClose
}: Props) {
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const trimmed = reason.trim();
  const tooShort = trimmed.length < MIN_REASON_LENGTH;

  const submit = async () => {
    if (busy || tooShort) return;
    setBusy(true);
    setError(null);
    try {
      const next = await httpPost<DreamRun>(
        dreamEndpoints.skipDreamProposal(runId, proposal.id),
        { reason: trimmed }
      );
      onSkipped(next);
    } catch (err: unknown) {
      if (err instanceof HttpError) {
        if (err.status === 409) {
          setError("Proposal уже обработан или run закрыт.");
        } else if (err.status === 422) {
          setError("Не удалось пропустить (валидация).");
        } else {
          setError(`Ошибка skip (${String(err.status)}).`);
        }
      } else {
        setError("Не удалось пропустить.");
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <dialog open className="modal modal-open">
      <div className="modal-box max-w-lg">
        <h3 className="m-0 mb-3 text-lg font-bold">Пропустить proposal</h3>
        <p className="m-0 mb-3 text-sm text-base-content/70">
          Reason — обучающий сигнал: dream-loop увидит, что правило
          <span className="font-mono">
            {" "}
            «{previewRule(proposal.proposed_rule)}»{" "}
          </span>
          было отвергнуто.
        </p>
        <label className="label flex flex-col items-start gap-1">
          <span className="label-text text-xs uppercase tracking-wide text-base-content/60">
            Reason (минимум {String(MIN_REASON_LENGTH)} символов)
          </span>
          <textarea
            className="textarea textarea-bordered w-full text-[13px] leading-relaxed"
            value={reason}
            onChange={(e) => {
              setReason(e.target.value);
            }}
            rows={4}
            aria-label="Reason"
            disabled={busy}
          />
        </label>
        {error ? (
          <p role="alert" className="mt-3 text-sm text-error">
            {error}
          </p>
        ) : null}
        <div className="modal-action mt-4">
          <Button type="button" onClick={onClose} disabled={busy}>
            Отмена
          </Button>
          <Button
            type="button"
            onClick={() => {
              void submit();
            }}
            variant="primary"
            disabled={busy || tooShort}
          >
            {busy ? "Сохраняем…" : "Пропустить"}
          </Button>
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

function previewRule(rule: string): string {
  if (rule.length <= 60) return rule;
  return `${rule.slice(0, 57)}…`;
}
