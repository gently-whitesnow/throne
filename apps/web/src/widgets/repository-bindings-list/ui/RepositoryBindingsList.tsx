import { AlertCircle, FolderGit2 } from "lucide-react";
import { useCallback, useState } from "react";

import { useIntentRepositories } from "@/entities/repository-binding";
import { BindRepositoryButton } from "@/features/bind-repository";

import { RepositoryBindingRow } from "./RepositoryBindingRow";

interface RepositoryBindingsListProps {
  intentId: string;
}

/**
 * Intent → секция «Репозитории».
 *
 * Тонкая обёртка над `useIntentRepositories` (entities/repository-binding):
 * хук уже подписан на `intent.repository_bound`, `intent.repository_unbound`
 * и `intent.repository_clone_progress`, поэтому виджет ничего не дублирует и
 * только рендерит производное состояние списка плюс заголовок с кнопкой
 * «Bind repository» (модал живёт отдельно).
 *
 * Per-row «Обновить» дёргает `refresh()` хука (re-fetch списка) — это самая
 * дешёвая консервативная семантика. PR-sync вынесен в виджет
 * pull-request-comments, чтобы не дублировать действие и не мешать
 * двум секциям одной страницы.
 */
export function RepositoryBindingsList({
  intentId
}: RepositoryBindingsListProps) {
  const { bindings, isLoading, error, refresh } =
    useIntentRepositories(intentId);

  // Optimistic remove — DELETE отдаёт 204 без тела; реалтайм-эвент
  // `intent.repository_unbound` сам уберёт строку у других вкладок.
  // Локальный список держим в стейте хука: чтобы не плодить второй источник
  // правды, прячем удаляемые id и применяем фильтр на рендере.
  const [removedIds, setRemovedIds] = useState<ReadonlySet<string>>(
    () => new Set()
  );
  const onUnbound = useCallback((id: string) => {
    setRemovedIds((prev) => {
      const next = new Set(prev);
      next.add(id);
      return next;
    });
  }, []);

  const visible = bindings.filter((b) => !removedIds.has(b.id));

  return (
    <section
      aria-label="Репозитории"
      className="mt-6 flex flex-col gap-3"
      data-testid="repository-bindings-section"
    >
      <header className="flex items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <h2 className="m-0 text-sm font-semibold text-base-content">
            Репозитории
          </h2>
          {visible.length > 0 ? (
            <span className="rounded-full bg-base-200 px-2 py-0.5 text-[11px] font-medium text-base-content/60">
              {visible.length}
            </span>
          ) : null}
        </div>
        <BindRepositoryButton intentId={intentId} />
      </header>

      <Body
        isLoading={isLoading}
        error={error}
        bindings={visible}
        intentId={intentId}
        refresh={refresh}
        onUnbound={onUnbound}
      />
    </section>
  );
}

interface BodyProps {
  isLoading: boolean;
  error: Error | null;
  bindings: ReturnType<typeof useIntentRepositories>["bindings"];
  intentId: string;
  refresh: () => void;
  onUnbound: (bindingId: string) => void;
}

function Body({
  isLoading,
  error,
  bindings,
  intentId,
  refresh,
  onUnbound
}: BodyProps) {
  if (error) {
    return (
      <p
        role="alert"
        className="m-0 flex items-start gap-2 rounded-md border border-error/30 bg-error/10 px-3 py-2 text-sm text-error"
      >
        <AlertCircle aria-hidden size={16} strokeWidth={2} className="mt-0.5" />
        <span>Не удалось загрузить список репозиториев: {error.message}</span>
      </p>
    );
  }

  if (bindings.length === 0) {
    if (isLoading) {
      return (
        <p className="m-0 text-sm text-base-content/60">Загружаем список…</p>
      );
    }
    return (
      <div className="flex flex-col items-center gap-2 rounded-lg border border-dashed border-base-300 bg-base-100 px-4 py-6 text-center">
        <span
          aria-hidden
          className="inline-flex h-9 w-9 items-center justify-center rounded-md bg-base-200 text-base-content/60"
        >
          <FolderGit2 size={18} strokeWidth={2} />
        </span>
        <p className="m-0 text-sm text-base-content/70">
          Репозитории не привязаны.
        </p>
        <p className="m-0 max-w-[44ch] text-xs text-base-content/60">
          Привяжите репозиторий, чтобы агенты Throne могли работать с кодом и
          вытягивать комментарии к PR.
        </p>
      </div>
    );
  }

  return (
    <ul className="m-0 flex list-none flex-col gap-2 p-0">
      {bindings.map((binding) => (
        <RepositoryBindingRow
          key={binding.id}
          intentId={intentId}
          binding={binding}
          onRefresh={refresh}
          onUnbound={onUnbound}
        />
      ))}
    </ul>
  );
}
