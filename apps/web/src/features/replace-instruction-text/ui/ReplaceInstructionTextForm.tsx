import { useState } from "react";

import type { InstructionDetail } from "@/entities/instruction";
import { HttpError, httpPost, instructionsEndpoints } from "@/shared/api";
import { Button } from "@/shared/ui";

interface ReplaceInstructionTextFormProps {
  instruction: InstructionDetail;
  onSaved: (next: InstructionDetail) => void;
  onCancel: () => void;
}

export function ReplaceInstructionTextForm({
  instruction,
  onSaved,
  onCancel
}: ReplaceInstructionTextFormProps) {
  const [draft, setDraft] = useState(instruction.text);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    if (busy) return;
    if (draft === instruction.text) {
      onCancel();
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const next = await httpPost<InstructionDetail>(
        instructionsEndpoints.replaceInstructionText(instruction.id),
        {
          expected_version: instruction.current_version,
          old_text: instruction.text,
          new_text: draft
        }
      );
      onSaved(next);
    } catch (err: unknown) {
      if (err instanceof HttpError) {
        setError(formatError(err));
      } else {
        setError("Не удалось сохранить.");
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        void submit();
      }}
      className="edit-text-form"
    >
      <textarea
        className="edit-text-form__textarea"
        value={draft}
        onChange={(e) => {
          setDraft(e.target.value);
        }}
        rows={20}
        aria-label="Текст instruction"
      />
      {error ? (
        <p role="alert" className="edit-text-form__error">
          {error}
        </p>
      ) : null}
      <div className="edit-text-form__actions">
        <Button type="submit" variant="primary" disabled={busy}>
          {busy ? "Сохраняем…" : "Сохранить"}
        </Button>
        <Button type="button" onClick={onCancel} disabled={busy}>
          Отмена
        </Button>
      </div>
    </form>
  );
}

function formatError(err: HttpError): string {
  if (err.status === 409) {
    return "Версия устарела — обновите страницу и повторите правку.";
  }
  if (err.status === 422) {
    return "Не удалось применить правку (текст не совпал).";
  }
  return `Ошибка сохранения (${String(err.status)}).`;
}
