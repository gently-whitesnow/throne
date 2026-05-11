import { useEffect, useState } from "react";

import { HttpError, httpGet } from "@/shared/api";

import type { TextVersion } from "../model/types";

interface TextVersionListProps {
  endpoint: string;
  reloadKey?: number;
}

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; items: TextVersion[] }
  | { kind: "error"; message: string };

const authorLabel: Record<TextVersion["changed_by"], string> = {
  user: "Пользователь",
  agent: "Агент",
  system: "Система"
};

const kindLabel: Record<TextVersion["kind"], string> = {
  create: "создание",
  replace: "правка",
  insert: "вставка"
};

export function TextVersionList({
  endpoint,
  reloadKey = 0
}: TextVersionListProps) {
  const [state, setState] = useState<LoadState>({ kind: "loading" });

  useEffect(() => {
    const controller = new AbortController();
    setState({ kind: "loading" });
    httpGet<TextVersion[]>(endpoint, controller.signal)
      .then((items) => {
        const sorted = [...items].sort((a, b) => b.version - a.version);
        setState({ kind: "ready", items: sorted });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        const message =
          err instanceof HttpError
            ? `Не удалось загрузить историю (${String(err.status)}).`
            : "Не удалось загрузить историю.";
        setState({ kind: "error", message });
      });
    return () => {
      controller.abort();
    };
  }, [endpoint, reloadKey]);

  if (state.kind === "loading") {
    return <p className="text-sm text-base-content/60">История загружается…</p>;
  }
  if (state.kind === "error") {
    return (
      <p role="alert" className="text-sm text-error">
        {state.message}
      </p>
    );
  }
  if (state.items.length === 0) {
    return <p className="text-sm text-base-content/60">Истории пока нет.</p>;
  }

  return (
    <ul className="flex flex-col gap-2.5">
      {state.items.map((v) => (
        <li
          className="rounded-md border border-base-300 px-3 py-2.5"
          key={v.version}
        >
          <header className="mb-1.5 flex flex-wrap gap-2.5 text-[11px] text-base-content/60">
            <strong className="text-base-content">v{v.version}</strong>
            <span>{kindLabel[v.kind]}</span>
            <span>{authorLabel[v.changed_by]}</span>
            <time dateTime={v.changed_at}>
              {new Date(v.changed_at).toLocaleString()}
            </time>
          </header>
          {v.kind === "create" && v.snapshot ? (
            <pre className="m-0 whitespace-pre-wrap break-words rounded bg-base-200 p-2 font-mono text-xs">
              {v.snapshot}
            </pre>
          ) : null}
          {v.kind === "replace" ? (
            <div className="m-0 rounded bg-base-200 p-2 font-mono text-xs">
              {v.old_text ? (
                <pre className="m-0 whitespace-pre-wrap break-words">
                  <del className="bg-error/10 line-through">{v.old_text}</del>
                </pre>
              ) : null}
              {v.new_text ? (
                <pre className="m-0 whitespace-pre-wrap break-words">
                  <ins className="bg-success/10 no-underline">{v.new_text}</ins>
                </pre>
              ) : null}
            </div>
          ) : null}
        </li>
      ))}
    </ul>
  );
}
