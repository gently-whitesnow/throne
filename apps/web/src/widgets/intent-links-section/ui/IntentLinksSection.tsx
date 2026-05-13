import { AlertTriangle, Link2 } from "lucide-react";
import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type DragEvent
} from "react";

import type { IntentDetail } from "@/entities/intent";
import { HttpError, httpGet, intentsEndpoints } from "@/shared/api";
import { useRealtimeEvent } from "@/shared/realtime";
import { INTENT_DND_MIME } from "@/shared/ui";

import { createIntentLink, deleteIntentLink } from "../api/intent-links-api";
import {
  BUCKET_ORDER,
  bucketDropParams,
  bucketOf,
  type DisplayBucket,
  type IntentLinkView
} from "../model/types";
import { AddLinkForm } from "./AddLinkForm";
import { LinkBucket } from "./LinkBucket";
import { LinkTypePicker } from "./LinkTypePicker";
import { Popover } from "./Popover";

interface IntentLinksSectionProps {
  intentId: string;
}

export function IntentLinksSection({ intentId }: IntentLinksSectionProps) {
  const [links, setLinks] = useState<IntentLinkView[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);
  const [sectionDragOver, setSectionDragOver] = useState(false);
  const [pickerPeerId, setPickerPeerId] = useState<string | null>(null);

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

  const total = links.length;
  const incomingBlocks = useMemo(
    () =>
      links.filter(
        (v) => v.direction === "incoming" && v.link.type === "blocks"
      ).length,
    [links]
  );

  const handleDeleteLink = (view: IntentLinkView) => {
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

  const handleBucketDrop = (bucket: DisplayBucket, peerId: string) => {
    const params = bucketDropParams(bucket, intentId, peerId);
    createIntentLink(params.fromId, {
      to_id: params.toId,
      type: params.type
    }).catch((err: unknown) => {
      const code = err instanceof HttpError ? err.code : undefined;
      setError(
        code === "link.duplicate"
          ? "Такая связь уже существует."
          : code === "link.self_link"
            ? "Нельзя связать intent сам с собой."
            : err instanceof HttpError
              ? `Ошибка (${String(err.status)}).`
              : "Не удалось создать связь."
      );
    });
  };

  const carriesIntent = (e: DragEvent) =>
    e.dataTransfer.types.includes(INTENT_DND_MIME);

  const handleSectionDragOver = (e: DragEvent<HTMLElement>) => {
    if (!carriesIntent(e)) return;
    e.preventDefault();
    e.dataTransfer.dropEffect = "link";
    if (!sectionDragOver) setSectionDragOver(true);
  };

  const handleSectionDragLeave = (e: DragEvent<HTMLElement>) => {
    if (
      e.relatedTarget instanceof Node &&
      e.currentTarget.contains(e.relatedTarget)
    ) {
      return;
    }
    setSectionDragOver(false);
  };

  const handleSectionDrop = (e: DragEvent<HTMLElement>) => {
    if (!carriesIntent(e)) return;
    e.preventDefault();
    setSectionDragOver(false);
    const peerId = e.dataTransfer.getData(INTENT_DND_MIME);
    if (peerId && peerId !== intentId) setPickerPeerId(peerId);
  };

  return (
    <section
      aria-labelledby="links-section-title"
      onDragOver={handleSectionDragOver}
      onDragLeave={handleSectionDragLeave}
      onDrop={handleSectionDrop}
      className={[
        "mt-5 flex flex-col gap-3 rounded-md border-t border-base-300 pt-4 transition-colors",
        sectionDragOver
          ? "ring-2 ring-primary/40 ring-offset-2 ring-offset-base-100"
          : ""
      ].join(" ")}
    >
      <header className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-2">
          <h2
            id="links-section-title"
            className="m-0 inline-flex items-center gap-1.5 text-base font-bold text-base-content"
          >
            <Link2 size={16} aria-hidden /> Связи
          </h2>
          <span
            className="rounded bg-primary/10 px-1.5 py-0.5 text-[11px] font-semibold tabular-nums text-primary"
            aria-label={`Всего связей: ${String(total)}`}
          >
            {total}
          </span>
          {incomingBlocks > 0 && (
            <span
              className="inline-flex items-center gap-1 rounded bg-warning/15 px-1.5 py-0.5 text-[11px] font-semibold tabular-nums text-warning"
              title={`Блокируется ${String(incomingBlocks)} связями`}
              aria-label={`Блокируется ${String(incomingBlocks)} связями`}
            >
              <AlertTriangle size={11} aria-hidden /> {incomingBlocks}
            </span>
          )}
        </div>
        <p className="m-0 hidden text-[11px] text-base-content/50 sm:block">
          Перетащите карточку с доски сюда, чтобы создать связь.
        </p>
      </header>

      {error && (
        <p role="alert" className="m-0 text-[12px] text-error">
          {error}
        </p>
      )}

      {grouped.length === 0 ? (
        <p className="m-0 text-[12px] text-base-content/50">
          Связей пока нет. Перетащите карточку с доски или воспользуйтесь формой
          ниже.
        </p>
      ) : (
        <div className="flex flex-col gap-3">
          {grouped.map((g) => (
            <LinkBucket
              key={g.bucket}
              bucket={g.bucket}
              intentId={intentId}
              items={g.items}
              onDeleteLink={handleDeleteLink}
              onDropPeer={(peerId) => {
                handleBucketDrop(g.bucket, peerId);
              }}
            />
          ))}
        </div>
      )}

      <div className="mt-1 rounded-md border border-base-300 bg-base-100 p-2.5">
        <h3 className="m-0 mb-1.5 text-[11px] font-semibold uppercase tracking-wider text-base-content/55">
          Новая связь
        </h3>
        <AddLinkForm intentId={intentId} />
      </div>

      <Popover
        open={pickerPeerId !== null}
        onClose={() => {
          setPickerPeerId(null);
        }}
        label="Выбор типа связи"
      >
        {pickerPeerId && (
          <LinkTypePicker
            peerId={pickerPeerId}
            onCancel={() => {
              setPickerPeerId(null);
            }}
            onPick={(bucket) => {
              handleBucketDrop(bucket, pickerPeerId);
              setPickerPeerId(null);
            }}
          />
        )}
      </Popover>
    </section>
  );
}
