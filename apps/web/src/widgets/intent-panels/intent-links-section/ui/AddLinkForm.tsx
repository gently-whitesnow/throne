import { useEffect, useMemo, useRef, useState } from "react";

import { useInfiniteIntents } from "@/entities/intent";
import { errorMessage, httpErrorCode, useDebouncedValue } from "@/shared/lib";
import { Button, HighlightedSnippet } from "@/shared/ui";

import { createIntentLink } from "../api/intent-links-api";
import { bucketDropParams, type DisplayBucket } from "../model/types";

interface AddLinkFormProps {
  intentId: string;
  /** Если задано — селектор типа спрятан и связь создаётся в этом бакете. */
  presetBucket?: DisplayBucket;
  /** Прячет лишний chrome (заголовки, рационал) — для встроенной формы. */
  compact?: boolean;
  autoFocus?: boolean;
  onCreated?: () => void;
}

const SEARCH_LIMIT = 20;
const SEARCH_DEBOUNCE_MS = 200;

export function AddLinkForm({
  intentId,
  presetBucket,
  compact = false,
  autoFocus = false,
  onCreated
}: AddLinkFormProps) {
  const [query, setQuery] = useState("");
  const debouncedQuery = useDebouncedValue(query.trim(), SEARCH_DEBOUNCE_MS);
  // useInfiniteIntents даёт первую страницу без авто-пагинации; для
  // autocomplete'а нам её достаточно, лишние страницы только дёргают сеть.
  const intentsQuery = useInfiniteIntents({
    query: debouncedQuery.length > 0 ? debouncedQuery : undefined,
    limit: SEARCH_LIMIT
  });
  const matches = useMemo(() => {
    const firstPage = intentsQuery.data?.pages[0]?.items;
    if (!firstPage) return [];
    return firstPage.filter((i) => i.id !== intentId).slice(0, SEARCH_LIMIT);
  }, [intentId, intentsQuery.data]);

  const [selected, setSelected] = useState<string | null>(null);
  const [blocking, setBlocking] = useState(false);
  const [rationale, setRationale] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    if (autoFocus) inputRef.current?.focus();
  }, [autoFocus]);

  const reset = () => {
    setQuery("");
    setSelected(null);
    setRationale("");
  };

  const submit = () => {
    if (!selected || submitting) return;
    setSubmitting(true);
    setError(null);

    const params = presetBucket
      ? bucketDropParams(presetBucket, intentId, selected)
      : { fromId: intentId, toId: selected, blocking };
    createIntentLink(params.fromId, {
      to_id: params.toId,
      blocking: params.blocking,
      rationale: rationale.trim() ? rationale.trim() : undefined
    })
      .then(() => {
        reset();
        setSubmitting(false);
        onCreated?.();
      })
      .catch((err: unknown) => {
        const code = httpErrorCode(err);
        setError(
          code === "link.duplicate"
            ? "Такая связь уже существует."
            : code === "link.self_link"
              ? "Нельзя связать intent сам с собой."
              : errorMessage(err, {
                  base: "Ошибка",
                  fallback: "Не удалось создать связь."
                })
        );
        setSubmitting(false);
      });
  };

  const loading = intentsQuery.isPending || intentsQuery.isFetching;

  return (
    <div className="flex flex-col gap-2">
      {!presetBucket && (
        <select
          value={blocking ? "blocking" : "normal"}
          onChange={(e) => {
            setBlocking(e.target.value === "blocking");
          }}
          className="rounded border border-base-300 bg-base-100 px-2 py-1 text-[12px] text-base-content"
          aria-label="Тип связи"
        >
          <option value="normal">Обычная</option>
          <option value="blocking">Блокирующая</option>
        </select>
      )}
      <input
        ref={inputRef}
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
            {loading ? "Загрузка…" : "Не найдено"}
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
              {i.snippet ? (
                <HighlightedSnippet
                  snippet={i.snippet}
                  className="line-clamp-1"
                />
              ) : (
                <span className="line-clamp-1">{i.text_short || i.id}</span>
              )}
            </button>
          </li>
        ))}
      </ul>
      {!compact && (
        <textarea
          value={rationale}
          onChange={(e) => {
            setRationale(e.target.value);
          }}
          placeholder="Зачем эта связь? (опционально)"
          rows={2}
          className="rounded border border-base-300 bg-base-100 px-2 py-1 text-[12px] text-base-content placeholder:text-base-content/50"
        />
      )}
      {error && (
        <p role="alert" className="m-0 text-[11px] text-error">
          {error}
        </p>
      )}
      <div className="flex justify-end">
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
