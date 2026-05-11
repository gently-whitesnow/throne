import { ChevronDown, ChevronLeft, Link2, Plus, X } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";

import type { IntentDetail } from "@/entities/intent";
import { HttpError, httpGet, intentsEndpoints } from "@/shared/api";
import { useRealtimeEvent } from "@/shared/realtime";
import { Button } from "@/shared/ui";

import { deleteIntentLink } from "../api/intent-links-api";
import {
  type DisplayBucket,
  type IntentLinkView,
  bucketLabel,
  bucketOf
} from "../model/types";
import { AddLinkForm } from "./AddLinkForm";

interface IntentLinksPanelProps {
  intentId: string;
}

const BUCKET_ORDER: DisplayBucket[] = [
  "blocks_incoming",
  "blocks_outgoing",
  "relates",
  "derived_outgoing",
  "derived_incoming"
];

export function IntentLinksPanel({ intentId }: IntentLinksPanelProps) {
  const [collapsed, setCollapsed] = useState(true);
  const [links, setLinks] = useState<IntentLinkView[]>([]);
  const [adding, setAdding] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    httpGet<IntentDetail>(
      intentsEndpoints.getIntent(intentId),
      controller.signal
    )
      .then((d) => {
        setLinks(d.links);
        setError(null);
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        setError(
          err instanceof HttpError
            ? `Ошибка (${String(err.status)}).`
            : "Не удалось загрузить связи."
        );
      });
    return () => {
      controller.abort();
    };
  }, [intentId, reloadKey]);

  const refresh = useCallback(() => {
    setReloadKey((k) => k + 1);
  }, []);

  // Realtime: stage-1 mutations already broadcast `intent.link_added` /
  // `intent.link_removed`. Refetch the detail (which carries the projected `links[]`)
  // so peer-info stays consistent.
  useRealtimeEvent("intent.link_added", (payload) => {
    if (payload.from_id === intentId || payload.to_id === intentId) refresh();
  });
  useRealtimeEvent("intent.link_removed", (payload) => {
    if (payload.from_id === intentId || payload.to_id === intentId) refresh();
  });

  const grouped = useMemo(() => {
    const map = new Map<DisplayBucket, IntentLinkView[]>();
    for (const view of links) {
      const b = bucketOf(view);
      const arr = map.get(b);
      if (arr) arr.push(view);
      else map.set(b, [view]);
    }
    return BUCKET_ORDER.filter((b) => map.has(b)).map((b) => ({
      bucket: b,
      items: map.get(b) ?? []
    }));
  }, [links]);

  const handleDelete = (view: IntentLinkView) => {
    const peerId = view.peer.id;
    const fromId = view.direction === "outgoing" ? intentId : peerId;
    const toId = view.direction === "outgoing" ? peerId : intentId;
    deleteIntentLink(fromId, toId, view.link.type).catch((err: unknown) => {
      setError(
        err instanceof HttpError
          ? `Ошибка удаления (${String(err.status)}).`
          : "Не удалось удалить связь."
      );
    });
  };

  const total = links.length;

  if (collapsed) {
    return (
      <button
        type="button"
        onClick={() => {
          setCollapsed(false);
        }}
        title="Развернуть связи"
        aria-label={`Развернуть связи (${String(total)})`}
        className="flex flex-shrink-0 items-center gap-1 rounded-md border border-base-300 bg-base-100 px-2 py-1 text-[12px] text-base-content/70 hover:bg-base-200"
      >
        <Link2 size={14} aria-hidden />
        <span className="font-semibold">{total}</span>
        <ChevronLeft size={12} aria-hidden />
      </button>
    );
  }

  return (
    <aside
      aria-label="Связи intent"
      className="flex w-[280px] flex-shrink-0 flex-col gap-2 border-l border-base-300 bg-base-100 px-3 py-3"
    >
      <header className="flex items-center justify-between gap-2">
        <h2 className="m-0 inline-flex items-center gap-1.5 text-[12px] font-bold uppercase tracking-wider text-base-content/60">
          <Link2 size={14} aria-hidden />
          Связи
          <span className="text-base-content/40">{total}</span>
        </h2>
        <button
          type="button"
          onClick={() => {
            setCollapsed(true);
          }}
          title="Свернуть"
          aria-label="Свернуть связи"
          className="inline-flex h-6 w-6 items-center justify-center rounded-md text-base-content/50 transition-colors hover:bg-base-200 hover:text-base-content"
        >
          <ChevronDown size={14} aria-hidden />
        </button>
      </header>
      {error && (
        <p role="alert" className="m-0 text-[11px] text-error">
          {error}
        </p>
      )}
      {!adding ? (
        <Button
          onClick={() => {
            setAdding(true);
          }}
        >
          <Plus size={12} aria-hidden /> Добавить связь
        </Button>
      ) : (
        <AddLinkForm
          intentId={intentId}
          onCancel={() => {
            setAdding(false);
          }}
          onCreated={() => {
            setAdding(false);
            refresh();
          }}
        />
      )}
      <div className="flex min-h-0 flex-1 flex-col gap-3 overflow-y-auto">
        {grouped.length === 0 && (
          <p className="m-0 text-[12px] text-base-content/50">
            Связей пока нет.
          </p>
        )}
        {grouped.map((g) => (
          <section key={g.bucket} className="flex flex-col gap-1">
            <h3 className="m-0 text-[10px] font-semibold uppercase tracking-wider text-base-content/50">
              {bucketLabel[g.bucket]}
              <span className="ml-1 text-base-content/30">
                {g.items.length}
              </span>
            </h3>
            <ul className="m-0 flex list-none flex-col gap-1 p-0">
              {g.items.map((view) => (
                <LinkRow
                  key={view.link.id}
                  view={view}
                  onDelete={() => {
                    handleDelete(view);
                  }}
                />
              ))}
            </ul>
          </section>
        ))}
      </div>
    </aside>
  );
}

function LinkRow({
  view,
  onDelete
}: {
  view: IntentLinkView;
  onDelete: () => void;
}) {
  const { peer, link } = view;
  const title = peer.text_short.split(/\r?\n/, 1)[0] ?? peer.id;
  return (
    <li className="group relative flex items-start gap-1 rounded border border-base-300 bg-base-100 px-2 py-1.5 hover:border-primary/40">
      <Link
        to={`/intents/${peer.id}`}
        className="min-w-0 flex-1 text-[12px] text-base-content no-underline hover:text-primary"
        title={peer.text_short}
      >
        <span className="line-clamp-1">{title}</span>
        {link.rationale && (
          <span className="m-0 mt-0.5 block text-[11px] text-base-content/50 line-clamp-2">
            {link.rationale}
          </span>
        )}
      </Link>
      <button
        type="button"
        onClick={onDelete}
        title="Удалить связь"
        aria-label="Удалить связь"
        className="invisible inline-flex h-5 w-5 flex-shrink-0 items-center justify-center rounded text-base-content/40 hover:bg-error/10 hover:text-error focus-visible:visible group-hover:visible"
      >
        <X size={12} aria-hidden />
      </button>
    </li>
  );
}
