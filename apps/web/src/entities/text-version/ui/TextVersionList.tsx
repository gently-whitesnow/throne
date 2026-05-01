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
    return <p>История загружается…</p>;
  }
  if (state.kind === "error") {
    return <p role="alert">{state.message}</p>;
  }
  if (state.items.length === 0) {
    return <p>Истории пока нет.</p>;
  }

  return (
    <ul className="text-version-list">
      {state.items.map((v) => (
        <li className="text-version-list__item" key={v.version}>
          <header className="text-version-list__header">
            <strong>v{v.version}</strong>
            <span>{kindLabel[v.kind]}</span>
            <span>{authorLabel[v.changed_by]}</span>
            <time dateTime={v.changed_at}>
              {new Date(v.changed_at).toLocaleString()}
            </time>
          </header>
          {v.kind === "create" && v.snapshot ? (
            <pre className="text-version-list__diff">{v.snapshot}</pre>
          ) : null}
          {v.kind === "replace" ? (
            <div className="text-version-list__diff">
              {v.old_text ? (
                <pre>
                  <del>{v.old_text}</del>
                </pre>
              ) : null}
              {v.new_text ? (
                <pre>
                  <ins>{v.new_text}</ins>
                </pre>
              ) : null}
            </div>
          ) : null}
        </li>
      ))}
    </ul>
  );
}
