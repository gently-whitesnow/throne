import { useDreamSessions } from "../model/use-dream-sessions";

import { DreamSessionRow } from "./DreamSessionRow";
import { DreamSourcesPanel } from "./DreamSourcesPanel";

/**
 * Dreams tab: история проходов фронтир-агента по разбору диалогов пользователя
 * (ADR-0022). Slева — последние сессии (vendor, период, summary, reflection,
 * счётчики предложенных патчей и обработанных диалогов). Справа — таблица
 * dream_sources из манифеста: vendor → локальный путь + подсказка, чтобы
 * пользователь видел, что агент сможет читать, и при необходимости настраивал
 * клиента.
 */
export function DreamsTab() {
  const { state } = useDreamSessions();

  return (
    <div className="grid gap-4 lg:grid-cols-[1fr_minmax(0,320px)]">
      <section aria-label="DreamSessions">
        <DreamSessionsList />
      </section>
      <aside aria-label="Dream sources" className="flex flex-col gap-2">
        <h3 className="m-0 text-sm font-semibold">Источники диалогов</h3>
        <DreamSourcesPanel />
      </aside>
    </div>
  );

  function DreamSessionsList() {
    if (state.kind === "loading") {
      return (
        <p className="m-0 text-sm text-base-content/60">Загрузка сессий...</p>
      );
    }
    if (state.kind === "error") {
      return (
        <p
          role="alert"
          className="rounded border border-error/30 bg-error/10 p-3 text-sm text-error"
        >
          {state.message}
        </p>
      );
    }
    if (state.items.length === 0) {
      return (
        <p className="m-0 rounded border border-base-300 bg-base-100 p-4 text-sm text-base-content/60">
          Записей пока нет. Dream улучшает блоки промпта по твоим реальным
          диалогам с агентами. Создай интент с описанием, на чём учиться
          (например «обучись на последних сессиях claude code»), и запусти его в
          режиме «Рефлексия»: агент разберёт диалоги, предложит правки блоков
          (придут во вкладку «Улучшения») и запишет проход сюда.
        </p>
      );
    }
    return (
      <ul
        className="m-0 flex max-h-[70vh] flex-col gap-2 overflow-auto p-0"
        aria-label="DreamSessions"
      >
        {state.items.map((s) => (
          <DreamSessionRow key={s.id} session={s} />
        ))}
      </ul>
    );
  }
}
