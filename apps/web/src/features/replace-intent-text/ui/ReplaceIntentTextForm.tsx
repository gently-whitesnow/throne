import { useState } from "react";

import type { IntentAttachment, IntentDetail } from "@/entities/intent";
import {
  HttpError,
  INTENT_ATTACHMENTS_CHANGED_EVENT,
  httpPost,
  httpPostForm,
  intentsEndpoints
} from "@/shared/api";
import { filesFromClipboard } from "@/shared/lib";
import { Button } from "@/shared/ui";

interface ReplaceIntentTextFormProps {
  intent: IntentDetail;
  onSaved: (next: IntentDetail) => void;
  onCancel: () => void;
}

export function ReplaceIntentTextForm({
  intent,
  onSaved,
  onCancel
}: ReplaceIntentTextFormProps) {
  const [draft, setDraft] = useState(intent.text);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pasteHint, setPasteHint] = useState<string | null>(null);

  const submit = async () => {
    if (busy) return;
    if (draft === intent.text) {
      onCancel();
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const next = await httpPost<IntentDetail>(
        intentsEndpoints.replaceIntentText(intent.id),
        {
          expected_version: intent.current_version,
          old_text: intent.text,
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

  const handlePaste = (event: React.ClipboardEvent<HTMLTextAreaElement>) => {
    const files = filesFromClipboard(event.clipboardData);
    if (files.length === 0) return;
    event.preventDefault();
    setPasteHint(`Загружаем ${String(files.length)} вложение(й)…`);
    void uploadAttachments(intent.id, files)
      .then((count) => {
        setPasteHint(
          count > 0
            ? `Добавлено вложений: ${String(count)}.`
            : "Не удалось добавить вложение."
        );
      })
      .catch(() => {
        setPasteHint("Не удалось добавить вложение.");
      });
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
        onPaste={handlePaste}
        rows={20}
        aria-label="Текст intent"
      />
      <p className="m-0 text-[11px] text-base-content/60">
        Можно вставить картинку из буфера — она прикрепится как вложение.
      </p>
      {pasteHint ? (
        <p className="m-0 text-[11px] text-base-content/70">{pasteHint}</p>
      ) : null}
      {error ? (
        <p role="alert" className="m-0 text-sm text-error">
          {error}
        </p>
      ) : null}
      <div className="flex gap-2">
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

async function uploadAttachments(
  intentId: string,
  files: File[]
): Promise<number> {
  let success = 0;
  for (const file of files) {
    const form = new FormData();
    form.append("file", file, file.name);
    try {
      await httpPostForm<IntentAttachment>(
        intentsEndpoints.uploadIntentAttachment(intentId),
        form
      );
      success += 1;
      window.dispatchEvent(
        new CustomEvent(INTENT_ATTACHMENTS_CHANGED_EVENT, {
          detail: { intentId }
        })
      );
    } catch (err) {
      const message =
        err instanceof HttpError
          ? `Не удалось загрузить ${file.name} (${String(err.status)}).`
          : `Не удалось загрузить ${file.name}.`;
      window.dispatchEvent(
        new CustomEvent(INTENT_ATTACHMENTS_CHANGED_EVENT, {
          detail: { intentId, error: message }
        })
      );
    }
  }
  return success;
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
