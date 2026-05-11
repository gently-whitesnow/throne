import { useEffect, useMemo, useState } from "react";

import type { IntentListItem } from "@/entities/intent";
import { HttpError, httpGet, intentsEndpoints } from "@/shared/api";
import { Button } from "@/shared/ui";

import { createIntentLink } from "../api/intent-links-api";
import type { IntentLinkType } from "../model/types";

interface AddLinkFormProps {
  intentId: string;
  onCreated: () => void;
  onCancel: () => void;
}

const linkTypeOptions: { value: IntentLinkType; label: string }[] = [
  { value: "relates", label: "Связан с" },
  { value: "blocks", label: "Блокирует" },
  { value: "derived_from", label: "Происходит из" }
];

export function AddLinkForm({
  intentId,
  onCreated,
  onCancel
}: AddLinkFormProps) {
  const [allIntents, setAllIntents] = useState<IntentListItem[] | null>(null);
  const [query, setQuery] = useState("");
  const [selected, setSelected] = useState<string | null>(null);
  const [type, setType] = useState<IntentLinkType>("relates");
  const [rationale, setRationale] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    httpGet<IntentListItem[]>(intentsEndpoints.listIntents(), controller.signal)
      .then((items) => {
        setAllIntents(items.filter((i) => i.id !== intentId));
      })
      .catch(() => {
        if (!controller.signal.aborted) setAllIntents([]);
      });
    return () => {
      controller.abort();
    };
  }, [intentId]);

  const matches = useMemo(() => {
    if (!allIntents) return [];
    const q = query.trim().toLowerCase();
    if (!q) return allIntents.slice(0, 8);
    return allIntents
      .filter(
        (i) =>
          i.text_short.toLowerCase().includes(q) ||
          i.tags.some((t) => t.name.toLowerCase().includes(q))
      )
      .slice(0, 8);
  }, [allIntents, query]);

  const submit = () => {
    if (!selected || submitting) return;
    setSubmitting(true);
    setError(null);
    createIntentLink(intentId, {
      to_id: selected,
      type,
      rationale: rationale.trim() ? rationale.trim() : undefined
    })
      .then(() => {
        onCreated();
      })
      .catch((err: unknown) => {
        const code = err instanceof HttpError ? err.code : undefined;
        setError(
          code === "link.duplicate"
            ? "Такая связь уже существует."
            : code === "link.self_link"
              ? "Нельзя связать интент сам с собой."
              : code === "link.type_unsupported"
                ? "Этот тип пока не поддержан."
                : err instanceof HttpError
                  ? `Ошибка (${String(err.status)}).`
                  : "Не удалось создать связь."
        );
        setSubmitting(false);
      });
  };

  return (
    <div className="flex flex-col gap-2 rounded-md border border-base-300 bg-base-100 p-2.5">
      <select
        value={type}
        onChange={(e) => {
          setType(e.target.value as IntentLinkType);
        }}
        className="rounded border border-base-300 bg-base-100 px-2 py-1 text-[12px] text-base-content"
        aria-label="Тип связи"
      >
        {linkTypeOptions.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
      <input
        type="text"
        value={query}
        onChange={(e) => {
          setQuery(e.target.value);
          setSelected(null);
        }}
        placeholder="Поиск intent…"
        className="rounded border border-base-300 bg-base-100 px-2 py-1 text-[12px] text-base-content placeholder:text-base-content/50"
        aria-label="Поиск intent"
      />
      <ul className="m-0 max-h-40 list-none overflow-y-auto p-0 text-[12px]">
        {matches.length === 0 && (
          <li className="px-1 py-0.5 text-base-content/50">
            {allIntents === null ? "Загрузка…" : "Не найдено"}
          </li>
        )}
        {matches.map((i) => (
          <li key={i.id}>
            <button
              type="button"
              onClick={() => {
                setSelected(i.id);
              }}
              className={`block w-full rounded px-1.5 py-1 text-left transition-colors hover:bg-base-200 ${
                selected === i.id ? "bg-primary/10 text-primary" : ""
              }`}
            >
              <span className="line-clamp-1">{i.text_short || i.id}</span>
            </button>
          </li>
        ))}
      </ul>
      <textarea
        value={rationale}
        onChange={(e) => {
          setRationale(e.target.value);
        }}
        placeholder="Зачем эта связь? (опционально)"
        rows={2}
        className="rounded border border-base-300 bg-base-100 px-2 py-1 text-[12px] text-base-content placeholder:text-base-content/50"
      />
      {error && (
        <p role="alert" className="m-0 text-[11px] text-error">
          {error}
        </p>
      )}
      <div className="flex justify-end gap-2">
        <Button onClick={onCancel}>Отмена</Button>
        <Button
          variant="primary"
          onClick={submit}
          disabled={!selected || submitting}
        >
          Добавить
        </Button>
      </div>
    </div>
  );
}
