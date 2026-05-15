import { useState } from "react";

import type { InstructionDetail } from "@/entities/instruction";
import { HttpError, httpPost, instructionsEndpoints } from "@/shared/api";
import { computeMinimalTextDelta } from "@/shared/lib";
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

  const isInitialCreate = instruction.current_version === 0;

  const submit = async () => {
    if (busy) return;
    if (!isInitialCreate) {
      const delta = computeMinimalTextDelta(instruction.text, draft);
      if (delta === null) {
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
            old_text: delta.oldText,
            new_text: delta.newText
          }
        );
        onSaved(next);
      } catch (err: unknown) {
        if (err instanceof HttpError) {
          setError(formatReplaceError(err));
        } else {
          setError("Не удалось сохранить.");
        }
      } finally {
        setBusy(false);
      }
      return;
    }

    setBusy(true);
    setError(null);
    try {
      const next = await httpPost<InstructionDetail>(
        instructionsEndpoints.createInstruction(),
        { kind: instruction.kind, text: draft }
      );
      onSaved(next);
    } catch (err: unknown) {
      if (err instanceof HttpError) {
        setError(formatCreateError(err));
      } else {
        setError("Не удалось создать инструкцию.");
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
      className="flex flex-col gap-3"
    >
      <textarea
        className="textarea textarea-bordered min-h-80 w-full font-mono text-[13px] leading-relaxed"
        value={draft}
        onChange={(e) => {
          setDraft(e.target.value);
        }}
        rows={20}
        aria-label="Текст instruction"
      />
      {error ? (
        <p role="alert" className="m-0 text-sm text-error">
          {error}
        </p>
      ) : null}
      <div className="flex gap-2">
        <Button type="submit" variant="primary" disabled={busy}>
          {busy
            ? isInitialCreate
              ? "Создаём…"
              : "Сохраняем…"
            : isInitialCreate
              ? "Создать"
              : "Сохранить"}
        </Button>
        <Button type="button" onClick={onCancel} disabled={busy}>
          Отмена
        </Button>
      </div>
    </form>
  );
}

function formatReplaceError(err: HttpError): string {
  if (err.status === 409) {
    return "Версия устарела — обновите страницу и повторите правку.";
  }
  if (err.status === 422) {
    return "Не удалось применить правку (текст не совпал).";
  }
  return `Ошибка сохранения (${String(err.status)}).`;
}

function formatCreateError(err: HttpError): string {
  if (err.status === 409) {
    return "Инструкция уже существует — обновите страницу и редактируйте имеющуюся.";
  }
  if (err.status === 422) {
    return "Не удалось создать инструкцию (валидация не прошла).";
  }
  return `Ошибка создания (${String(err.status)}).`;
}
