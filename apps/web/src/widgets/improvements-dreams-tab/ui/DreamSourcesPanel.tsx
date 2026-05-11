import { useDreamSources } from "../model/use-dream-sources";

export function DreamSourcesPanel() {
  const { state } = useDreamSources();

  if (state.kind === "loading") {
    return (
      <p className="m-0 text-xs text-base-content/60">Загрузка sources...</p>
    );
  }
  if (state.kind === "error") {
    return (
      <p
        role="alert"
        className="m-0 rounded border border-error/30 bg-error/10 p-2 text-xs text-error"
      >
        {state.message}
      </p>
    );
  }
  if (state.items.length === 0) {
    return (
      <p className="m-0 text-xs text-base-content/60">
        В манифесте не объявлено ни одного dream-source.
      </p>
    );
  }
  return (
    <ul className="m-0 flex flex-col gap-2 p-0">
      {state.items.map((s) => (
        <li
          key={s.vendor}
          className="m-0 list-none rounded border border-base-300 bg-base-100 p-2 text-xs"
        >
          <div className="flex items-center gap-2">
            <span className="font-semibold uppercase tracking-wide">
              {s.vendor}
            </span>
            <code className="rounded bg-base-200 px-1 py-0.5">{s.path}</code>
          </div>
          <p className="m-0 mt-1 text-[11px] text-base-content/70">{s.hint}</p>
        </li>
      ))}
    </ul>
  );
}
