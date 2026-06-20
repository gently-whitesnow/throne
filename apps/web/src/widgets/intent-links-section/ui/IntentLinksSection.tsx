import { AlertTriangle, Link2, Plus, X } from "lucide-react";
import { useMemo, useState, type DragEvent, type ReactNode } from "react";

import { useIntent } from "@/entities/intent";
import { errorMessage, httpErrorCode } from "@/shared/lib";
import { CollapsibleSection, INTENT_DND_MIME } from "@/shared/ui";

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
    ? errorMessage(intentQuery.error, {
        base: "Ошибка",
        fallback: "Не удалось загрузить связи."
      })
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
      links.filter((v) => v.direction === "incoming" && v.link.blocking).length,
    [links]
  );

  const handleDeleteLink = (view: IntentLinkView) => {
    const peerId = view.peer.id;
    const fromId = view.direction === "outgoing" ? intentId : peerId;
    const toId = view.direction === "outgoing" ? peerId : intentId;
    deleteIntentLink(fromId, toId).catch((err: unknown) => {
      setActionError(
        errorMessage(err, {
          base: "Ошибка удаления",
          fallback: "Не удалось удалить связь."
        })
      );
    });
  };

  const handleBucketDrop = (bucket: DisplayBucket, peerId: string) => {
    const params = bucketDropParams(bucket, intentId, peerId);
    createIntentLink(params.fromId, {
      to_id: params.toId,
      blocking: params.blocking
    }).catch((err: unknown) => {
      const code = httpErrorCode(err);
      setActionError(
        code === "link.duplicate"
          ? "Такая связь уже существует."
          : code === "link.self_link"
            ? "Нельзя связать intent сам с собой."
            : errorMessage(err, {
                base: "Ошибка",
                fallback: "Не удалось создать связь."
              })
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

  const addAffordance: ReactNode = addOpen ? (
    <div className="rounded-md border border-base-300 bg-base-100 p-2.5">
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
      className="inline-flex w-fit items-center gap-1.5 self-start rounded-md border border-dashed border-base-300 px-2.5 py-1 text-[12px] text-base-content/60 transition-colors hover:border-primary/40 hover:bg-base-200 hover:text-base-content focus-visible:outline-2 focus-visible:outline-primary focus-visible:outline-offset-1"
    >
      <Plus size={13} aria-hidden />{" "}
      {total > 0 ? "Новая связь" : "Добавить связь"}
    </button>
  );

  const errorNode = error ? (
    <p role="alert" className="m-0 text-[12px] text-error">
      {error}
    </p>
  ) : null;

  return (
    <div
      onDragOver={handleSectionDragOver}
      onDragLeave={handleSectionDragLeave}
      onDrop={handleSectionDrop}
      className={[
        "rounded-md transition-colors",
        sectionDragOver
          ? "ring-2 ring-primary/40 ring-offset-2 ring-offset-base-100"
          : ""
      ].join(" ")}
    >
      {total > 0 ? (
        <CollapsibleSection
          aria-label="Связи"
          icon={<Link2 size={14} strokeWidth={2} />}
          title="Связи"
          count={total}
          meta={
            incomingBlocks > 0 ? (
              <span
                className="inline-flex items-center gap-1 rounded bg-warning/15 px-1.5 py-0.5 text-[11px] font-semibold tabular-nums text-warning"
                title={`Блокируется ${String(incomingBlocks)} связями`}
                aria-label={`Блокируется ${String(incomingBlocks)} связями`}
              >
                <AlertTriangle size={11} aria-hidden /> {incomingBlocks}
              </span>
            ) : null
          }
        >
          <div className="flex flex-col gap-3 pt-2">
            {errorNode}
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
            {addAffordance}
          </div>
        </CollapsibleSection>
      ) : (
        <div className="flex flex-col gap-2" aria-label="Связи">
          {errorNode}
          {addAffordance}
        </div>
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
    </div>
  );
}
