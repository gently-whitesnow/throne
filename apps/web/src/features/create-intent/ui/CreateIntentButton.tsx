import { Plus } from "lucide-react";
import { useState } from "react";

import type { IntentDetail } from "@/entities/intent";
import { HttpError, httpPost, intentsEndpoints } from "@/shared/api";
import { Button } from "@/shared/ui";

interface CreateIntentButtonProps {
  onCreated?: (intent: IntentDetail) => void;
}

export function CreateIntentButton({ onCreated }: CreateIntentButtonProps) {
  const [open, setOpen] = useState(false);
  const [text, setText] = useState("");
  const [tags, setTags] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const reset = () => {
    setText("");
    setTags("");
    setError(null);
  };

  const submit = async () => {
    if (busy || text.trim().length === 0) return;
    setBusy(true);
    setError(null);
    try {
      const tagList = tags
        .split(",")
        .map((t) => t.trim())
        .filter(Boolean);
      const created = await httpPost<IntentDetail>(
        intentsEndpoints.createIntent(),
        {
          text,
          tags: tagList.length > 0 ? tagList : undefined
        }
      );
      reset();
      setOpen(false);
      onCreated?.(created);
    } catch (err: unknown) {
      const message =
        err instanceof HttpError
          ? `Не удалось создать (${String(err.status)}).`
          : "Не удалось создать.";
      setError(message);
    } finally {
      setBusy(false);
    }
  };

  if (!open) {
    return (
      <Button
        aria-label="Создать intent"
        icon={<Plus aria-hidden size={18} strokeWidth={2.4} />}
        variant="primary"
        onClick={() => {
          setOpen(true);
        }}
      >
        Создать
      </Button>
    );
  }

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        void submit();
      }}
      className="create-intent-form"
    >
      <textarea
        className="create-intent-form__textarea"
        placeholder="Текст нового Intent"
        value={text}
        onChange={(e) => {
          setText(e.target.value);
        }}
        rows={6}
        aria-label="Текст intent"
        autoFocus
      />
      <input
        className="create-intent-form__tags"
        placeholder="Теги через запятую (опционально)"
        value={tags}
        onChange={(e) => {
          setTags(e.target.value);
        }}
        aria-label="Теги intent"
      />
      {error ? (
        <p role="alert" className="edit-text-form__error">
          {error}
        </p>
      ) : null}
      <div className="edit-text-form__actions">
        <Button
          type="submit"
          variant="primary"
          disabled={busy || text.trim().length === 0}
        >
          {busy ? "Создаём…" : "Создать"}
        </Button>
        <Button
          type="button"
          onClick={() => {
            reset();
            setOpen(false);
          }}
          disabled={busy}
        >
          Отмена
        </Button>
      </div>
    </form>
  );
}
