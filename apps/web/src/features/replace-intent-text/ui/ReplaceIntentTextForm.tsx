import { useState } from "react";

import type { IntentAttachment, IntentDetail } from "@/entities/intent";
import {
  INTENT_ATTACHMENTS_CHANGED_EVENT,
  httpPost,
  httpPostForm,
  intentsEndpoints
} from "@/shared/api";
import {
  computeMinimalTextDelta,
  errorMessage,
  filesFromClipboard
} from "@/shared/lib";
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
    const delta = computeMinimalTextDelta(intent.text, draft);
    if (delta === null) {
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
          old_text: delta.oldText,
          new_text: delta.newText
        }
      );
      onSaved(next);
    } catch (err: unknown) {
      setError(
        errorMessage(err, {
          base: "Ошибка сохранения",
          byStatus: {
            409: "Версия устарела — обновите страницу и повторите правку.",
            422: "Не удалось применить правку (текст не совпал)."
          },
          fallback: "Не удалось сохранить."
        })
      );
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
      className="flex min-h-0 flex-1 flex-col gap-3"
    >
      <textarea
        className="textarea textarea-bordered min-h-0 w-full flex-1 resize-none font-mono text-[13px] leading-relaxed"
        value={draft}
        onChange={(e) => {
          setDraft(e.target.value);
        }}
        onPaste={handlePaste}
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
      const message = errorMessage(err, {
        base: `Не удалось загрузить ${file.name}`
      });
      window.dispatchEvent(
        new CustomEvent(INTENT_ATTACHMENTS_CHANGED_EVENT, {
          detail: { intentId, error: message }
        })
      );
    }
  }
  return success;
}
