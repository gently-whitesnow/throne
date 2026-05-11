import { Plus, X } from "lucide-react";
import {
  useCallback,
  useEffect,
  useId,
  useMemo,
  useRef,
  useState
} from "react";

import { normalizeTagSlug, TAG_NAME_MAX_LENGTH } from "./normalize";

interface TagMultiSelectProps {
  value: string[];
  onChange: (next: string[]) => void;
  /** All tag names known to the system (slugs). */
  availableTags: readonly string[];
  /** Optional creation handler. Returns the canonical slug to add on success. */
  onRequestCreate?: (slug: string) => Promise<string>;
  loadError?: string | null;
  disabled?: boolean;
  placeholder?: string;
  ariaLabel?: string;
  autoFocusInput?: boolean;
  className?: string;
}

export function TagMultiSelect({
  value,
  onChange,
  availableTags,
  onRequestCreate,
  loadError,
  disabled = false,
  placeholder = "Добавить тег…",
  ariaLabel = "Теги",
  autoFocusInput = false,
  className
}: TagMultiSelectProps) {
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);
  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  const rootRef = useRef<HTMLDivElement | null>(null);
  const listboxId = useId();

  useEffect(() => {
    if (!open) return;
    const handler = (event: MouseEvent) => {
      if (!rootRef.current) return;
      if (!rootRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener("mousedown", handler);
    return () => {
      document.removeEventListener("mousedown", handler);
    };
  }, [open]);

  const slug = useMemo(() => normalizeTagSlug(query), [query]);

  const matches = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (q.length === 0) return [...availableTags];
    return availableTags.filter((name) => name.toLowerCase().includes(q));
  }, [availableTags, query]);

  const exactExisting = useMemo(() => {
    if (!slug.valid) return null;
    return availableTags.find((name) => name === slug.slug) ?? null;
  }, [availableTags, slug]);

  const canCreate =
    Boolean(onRequestCreate) &&
    slug.valid &&
    !exactExisting &&
    !value.includes(slug.slug);

  const optionCount = matches.length + (canCreate ? 1 : 0);

  useEffect(() => {
    if (activeIndex >= optionCount) {
      setActiveIndex(Math.max(0, optionCount - 1));
    }
  }, [activeIndex, optionCount]);

  const addTag = useCallback(
    (name: string) => {
      if (!name) return;
      if (value.includes(name)) {
        setQuery("");
        return;
      }
      onChange([...value, name]);
      setQuery("");
      setActiveIndex(0);
    },
    [onChange, value]
  );

  const removeTag = useCallback(
    (name: string) => {
      onChange(value.filter((t) => t !== name));
    },
    [onChange, value]
  );

  const handleCreate = useCallback(async () => {
    if (!canCreate || creating || !onRequestCreate) return;
    setCreating(true);
    setCreateError(null);
    try {
      const created = await onRequestCreate(slug.slug);
      addTag(created);
    } catch (err: unknown) {
      setCreateError(
        err instanceof Error ? err.message : "Не удалось создать тег."
      );
    } finally {
      setCreating(false);
    }
  }, [addTag, canCreate, creating, onRequestCreate, slug.slug]);

  const commitActiveOption = useCallback(() => {
    if (activeIndex < matches.length) {
      const name = matches[activeIndex];
      addTag(name);
      return;
    }
    if (canCreate) void handleCreate();
  }, [activeIndex, addTag, canCreate, handleCreate, matches]);

  const onInputKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "Backspace" && query.length === 0 && value.length > 0) {
      event.preventDefault();
      const last = value[value.length - 1];
      removeTag(last);
      return;
    }
    if (!open && (event.key === "ArrowDown" || event.key === "Enter")) {
      setOpen(true);
      setActiveIndex(0);
    }
    if (event.key === "ArrowDown") {
      event.preventDefault();
      setActiveIndex((i) => Math.min(optionCount - 1, i + 1));
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      setActiveIndex((i) => Math.max(0, i - 1));
    } else if (event.key === "Enter") {
      event.preventDefault();
      commitActiveOption();
    } else if (event.key === "Escape") {
      event.preventDefault();
      setOpen(false);
      setQuery("");
    }
  };

  const slugError = useMemo(() => {
    if (slug.reason === null || query.trim().length === 0) return null;
    if (slug.reason === "too-long") {
      return `Слишком длинный (макс ${String(TAG_NAME_MAX_LENGTH)}).`;
    }
    if (slug.reason === "bad-chars") {
      return "Только a–z, 0–9, '-', '_'. Начинается с буквы или цифры.";
    }
    return null;
  }, [query, slug]);

  return (
    <div
      ref={rootRef}
      className={`relative flex flex-col gap-1 ${className ?? ""}`}
    >
      <div
        className="flex min-h-9 flex-wrap items-center gap-1 rounded-md border border-base-300 bg-base-100 px-2 py-1 focus-within:border-primary"
        role="group"
        aria-label={ariaLabel}
      >
        {value.map((name) => (
          <span
            key={name}
            className="inline-flex h-[22px] items-center gap-1 rounded-full border border-primary/40 bg-primary/10 pl-2 pr-1 text-[11px] font-medium text-primary"
          >
            #{name}
            <button
              type="button"
              className="inline-flex h-[18px] w-[18px] items-center justify-center rounded-full text-primary/80 transition-colors hover:bg-primary/15 hover:text-primary disabled:cursor-not-allowed disabled:opacity-50"
              onClick={() => {
                removeTag(name);
              }}
              aria-label={`Убрать тег ${name}`}
              disabled={disabled}
            >
              <X aria-hidden size={12} strokeWidth={2} />
            </button>
          </span>
        ))}
        <input
          type="text"
          className="min-w-[8ch] flex-1 bg-transparent text-[13px] outline-none placeholder:text-base-content/50"
          value={query}
          placeholder={value.length === 0 ? placeholder : ""}
          aria-label={ariaLabel}
          aria-autocomplete="list"
          aria-controls={open ? listboxId : undefined}
          aria-expanded={open}
          autoFocus={autoFocusInput}
          disabled={disabled}
          onChange={(e) => {
            setQuery(e.target.value);
            setOpen(true);
            setActiveIndex(0);
            setCreateError(null);
          }}
          onFocus={() => {
            setOpen(true);
          }}
          onKeyDown={onInputKeyDown}
        />
      </div>
      {slugError ? (
        <p className="m-0 text-[11px] text-error">{slugError}</p>
      ) : null}
      {createError ? (
        <p role="alert" className="m-0 text-[11px] text-error">
          {createError}
        </p>
      ) : null}
      {loadError ? (
        <p role="alert" className="m-0 text-[11px] text-error">
          {loadError}
        </p>
      ) : null}
      {open ? (
        <ul
          id={listboxId}
          role="listbox"
          aria-label="Доступные теги"
          className="absolute left-0 right-0 top-full z-30 mt-1 flex max-h-64 flex-col overflow-y-auto rounded-md border border-base-300 bg-base-100 py-1 shadow-lg"
        >
          {matches.length === 0 && !canCreate ? (
            <li className="px-3 py-2 text-[12px] text-base-content/60">
              {query.trim().length > 0 ? "Нет совпадений." : "Тегов пока нет."}
            </li>
          ) : null}
          {matches.map((name, idx) => {
            const selected = value.includes(name);
            const active = idx === activeIndex;
            return (
              <li
                key={name}
                role="option"
                aria-selected={selected}
                className={[
                  "flex cursor-pointer items-center justify-between gap-2 px-3 py-1.5 text-[13px]",
                  active ? "bg-primary/10" : "hover:bg-base-200"
                ].join(" ")}
                onMouseEnter={() => {
                  setActiveIndex(idx);
                }}
                onMouseDown={(event) => {
                  event.preventDefault();
                  if (selected) {
                    removeTag(name);
                  } else {
                    addTag(name);
                  }
                }}
              >
                <span className="truncate text-base-content">#{name}</span>
                {selected ? (
                  <span className="text-[10px] uppercase tracking-wider text-primary">
                    выбран
                  </span>
                ) : null}
              </li>
            );
          })}
          {canCreate ? (
            <li
              role="option"
              aria-selected={false}
              className={[
                "flex cursor-pointer items-center gap-2 border-t border-base-300 px-3 py-1.5 text-[13px]",
                activeIndex === matches.length
                  ? "bg-primary/10"
                  : "hover:bg-base-200"
              ].join(" ")}
              onMouseEnter={() => {
                setActiveIndex(matches.length);
              }}
              onMouseDown={(event) => {
                event.preventDefault();
                void handleCreate();
              }}
            >
              <Plus aria-hidden size={14} strokeWidth={2} />
              <span className="truncate">
                Создать тег{" "}
                <span className="font-mono text-primary">#{slug.slug}</span>
              </span>
              {creating ? (
                <span className="ml-auto text-[10px] uppercase tracking-wider text-base-content/60">
                  создаём…
                </span>
              ) : null}
            </li>
          ) : null}
        </ul>
      ) : null}
    </div>
  );
}
