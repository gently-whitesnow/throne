import { useState } from "react";

import type { DreamRun } from "@/entities/dream-run";
import { isEmptyRun, pendingProposalsCount } from "@/entities/dream-run";
import { dreamEndpoints, HttpError, httpPost } from "@/shared/api";
import { Button } from "@/shared/ui";

interface Props {
  run: DreamRun;
  onClosed: (run: DreamRun) => void;
  onCancel: () => void;
}

export function CloseDreamRunModal({ run, onClosed, onCancel }: Props) {
  const empty = isEmptyRun(run);
  const pendingCount = pendingProposalsCount(run);
  const defaultRelease = empty;
  const [releaseEvidence, setReleaseEvidence] = useState(defaultRelease);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      const next = await httpPost<DreamRun>(
        dreamEndpoints.closeDreamRun(run.id),
        { release_evidence: releaseEvidence }
      );
      onClosed(next);
    } catch (err: unknown) {
      if (err instanceof HttpError) {
        if (err.status === 409) {
          setError("Run уже закрыт.");
        } else if (err.status === 404) {
          setError("Run не найден.");
        } else {
          setError(`Ошибка close (${String(err.status)}).`);
        }
      } else {
        setError("Не удалось закрыть.");
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <dialog open className="modal modal-open">
      <div className="modal-box max-w-md">
        <h3 className="m-0 mb-3 text-lg font-bold">
          {empty ? "Discard пустой run" : "Force close run"}
        </h3>
        {empty ? (
          <p className="m-0 mb-3 text-sm text-base-content/70">
            Run без proposals — агент не нашёл достойных правил. Можно закрыть и
            при необходимости вернуть evidence в пул для следующего /tdream.
          </p>
        ) : (
          <p className="m-0 mb-3 text-sm text-warning">
            В run всё ещё{" "}
            <strong>{String(pendingCount)} pending proposal(s)</strong>. После
            close их нельзя будет применить.
          </p>
        )}
        <label className="flex items-start gap-2 text-sm">
          <input
            type="checkbox"
            className="checkbox checkbox-sm mt-0.5"
            checked={releaseEvidence}
            onChange={(e) => {
              setReleaseEvidence(e.target.checked);
            }}
            disabled={busy}
          />
          <span>
            <span className="font-semibold">Release evidence</span>
            <span className="block text-xs text-base-content/60">
              Если включено, evidence_refs run-а вернутся в пул unprocessed и
              смогут попасть в следующий /tdream.
              {empty ? " По умолчанию для пустых run-ов." : ""}
            </span>
          </span>
        </label>
        {error ? (
          <p role="alert" className="mt-3 text-sm text-error">
            {error}
          </p>
        ) : null}
        <div className="modal-action mt-4">
          <Button type="button" onClick={onCancel} disabled={busy}>
            Отмена
          </Button>
          <Button
            type="button"
            onClick={() => {
              void submit();
            }}
            variant="primary"
            disabled={busy}
          >
            {busy ? "Закрываем…" : "Закрыть run"}
          </Button>
        </div>
      </div>
      <button
        type="button"
        aria-label="Закрыть"
        className="modal-backdrop"
        onClick={onCancel}
      />
    </dialog>
  );
}
