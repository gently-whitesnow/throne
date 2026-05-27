import { AlertTriangle, Link2, Plus, X } from "lucide-react";
import { useMemo, useState, type DragEvent } from "react";

import { useIntent } from "@/entities/intent";
import { HttpError } from "@/shared/api";
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

const EMPTY_LINKS: readonly IntentLinkView[] = [];

export function IntentLinksSection({ intentId }: IntentLinksSectionProps) {
  const intentQuery = useIntent(intentId);
  const links: readonly IntentLinkView[] =
    intentQuery.data?.links ?? EMPTY_LINKS;
  const [actionError, setActionError] = useState<string | null>(null);
  const [sectionDragOver, setSectionDragOver] = useState(false);
  const [pickerPeerId, setPickerPeerId] = useState<string | null>(null);
  const [addOpen, setAddOpen] = useState(false);

  const loadError = intentQuery.isError
    ? intentQuery.error instanceof HttpError
      ? `Ошибка (${String(intentQuery.error.status)}).`
      : "Не удалось загрузить связи."
    : null;
  const error = actionError ?? loadError;

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
      setActionError(
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
      setActionError(
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
          Связей пока нет. Перетащите карточку с доски или нажмите «Новая
          связь».
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

      {addOpen ? (
        <div className="mt-1 rounded-md border border-base-300 bg-base-100 p-2.5">
          <div className="mb-1.5 flex items-center justify-between gap-2">
            <h3 className="m-0 text-[11px] font-semibold uppercase tracking-wider text-base-content/55">
              Новая связь
            </h3>
            <button
              type="button"
              onClick={() => {
                setAddOpen(false);
              }}
              aria-label="Свернуть форму"
              className="inline-flex h-5 w-5 items-center justify-center rounded text-base-content/40 transition-colors hover:bg-base-200 hover:text-base-content focus-visible:outline-2 focus-visible:outline-primary focus-visible:outline-offset-1"
            >
              <X size={13} aria-hidden />
            </button>
          </div>
          <AddLinkForm
            intentId={intentId}
            autoFocus
            onCreated={() => {
              setAddOpen(false);
            }}
          />
        </div>
      ) : (
        <button
          type="button"
          onClick={() => {
            setAddOpen(true);
          }}
          className="mt-1 inline-flex w-fit items-center gap-1.5 self-start rounded-md border border-dashed border-base-300 px-2.5 py-1 text-[12px] text-base-content/60 transition-colors hover:border-primary/40 hover:bg-base-200 hover:text-base-content focus-visible:outline-2 focus-visible:outline-primary focus-visible:outline-offset-1"
        >
          <Plus size={13} aria-hidden /> Новая связь
        </button>
      )}

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
