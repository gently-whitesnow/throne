import { Check, Pencil, X } from "lucide-react";
import { useEffect, useRef, useState } from "react";

import type { IntentDetail } from "@/entities/intent";
import { httpPut, intentsEndpoints } from "@/shared/api";
import { errorMessage } from "@/shared/lib";
import { Button } from "@/shared/ui";

interface SetIntentTitleFormProps {
  intent: IntentDetail;
  onSaved: (next: IntentDetail) => void;
}

/**
 * Inline-редактор заголовка интента. В режиме просмотра показывает title с
 * фоллбэком (первая строка текста → id), в режиме правки шлёт PUT
 * `…/title` с optimistic-concurrency `expected_version`. Пустая строка
 * очищает title (отправляем `null`).
 */
export function SetIntentTitleForm({
  intent,
  onSaved
}: SetIntentTitleFormProps) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(intent.title ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    setEditing(false);
    setError(null);
  }, [intent.id]);

  useEffect(() => {
    if (editing) {
      setDraft(intent.title ?? "");
      setError(null);
      inputRef.current?.focus();
    }
  }, [editing, intent.title]);

  const display =
    (intent.title?.trim() ?? "") || firstLine(intent.text) || intent.id;
  const hasTitle = Boolean(intent.title?.trim());

  const submit = async () => {
    if (busy) return;
    const trimmed = draft.trim();
    if (trimmed === (intent.title?.trim() ?? "")) {
      setEditing(false);
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const next = await httpPut<IntentDetail>(
        intentsEndpoints.setIntentTitle(intent.id),
        {
          // Пустая строка → null: очищаем заголовок.
          title: trimmed.length > 0 ? trimmed : null,
          expected_version: intent.current_version
        }
      );
      onSaved(next);
      setEditing(false);
    } catch (err: unknown) {
      setError(
        errorMessage(err, {
          base: "Не удалось сохранить заголовок",
          byStatus: {
            409: "Intent уже изменился. Перезагрузите карточку и попробуйте снова.",
            422: "У связанного интента заголовок обязателен — он не может быть пустым."
          }
        })
      );
    } finally {
      setBusy(false);
    }
  };

  if (editing) {
    return (
      <form
        onSubmit={(e) => {
          e.preventDefault();
          void submit();
        }}
        className="flex min-w-0 flex-col gap-2"
      >
        <div className="flex min-w-0 items-center gap-2">
          <input
            ref={inputRef}
            className="input input-bordered input-sm min-w-0 flex-1 text-base font-semibold"
            value={draft}
            onChange={(e) => {
              setDraft(e.target.value);
            }}
            onKeyDown={(e) => {
              if (e.key === "Escape") {
                e.preventDefault();
                setEditing(false);
              }
            }}
            placeholder="Заголовок интента"
            aria-label="Заголовок интента"
            disabled={busy}
          />
          <Button
            type="submit"
            variant="primary"
            disabled={busy}
            aria-label="Сохранить заголовок"
            title="Сохранить"
          >
            <Check aria-hidden size={14} strokeWidth={2.5} />
          </Button>
          <Button
            type="button"
            onClick={() => {
              setEditing(false);
            }}
            disabled={busy}
            aria-label="Отменить"
            title="Отмена"
          >
            <X aria-hidden size={14} strokeWidth={2.5} />
          </Button>
        </div>
        {error ? (
          <p role="alert" className="m-0 text-xs text-error">
            {error}
          </p>
        ) : null}
      </form>
    );
  }

  return (
    <div className="group flex min-w-0 items-start gap-2">
      <h1
        className="m-0 min-w-0 break-words text-lg font-semibold leading-snug text-base-content"
        style={hasTitle ? undefined : { fontStyle: "italic", opacity: 0.85 }}
      >
        {display}
      </h1>
      <button
        type="button"
        onClick={() => {
          setEditing(true);
        }}
        aria-label={hasTitle ? "Изменить заголовок" : "Задать заголовок"}
        title={hasTitle ? "Изменить заголовок" : "Задать заголовок"}
        className="mt-1 inline-flex h-6 w-6 flex-shrink-0 items-center justify-center rounded-md text-base-content/50 opacity-0 transition hover:bg-base-200 hover:text-base-content focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary group-hover:opacity-100"
      >
        <Pencil aria-hidden size={13} strokeWidth={2} />
      </button>
    </div>
  );
}

function firstLine(text: string): string {
  const line = text.split(/\r?\n/, 1)[0] ?? "";
  return line.length > 80 ? `${line.slice(0, 80)}…` : line;
}
