import { Plus } from "lucide-react";
import { useState, type DragEvent } from "react";

import { INTENT_DND_MIME } from "@/shared/ui";

import {
  bucketLabel,
  type DisplayBucket,
  type IntentLinkView
} from "../model/types";
import { AddLinkForm } from "./AddLinkForm";
import { LinkCard } from "./LinkCard";
import { Popover } from "./Popover";

interface LinkBucketProps {
  bucket: DisplayBucket;
  intentId: string;
  items: IntentLinkView[];
  onDeleteLink: (view: IntentLinkView) => void;
  onDropPeer: (peerId: string) => void;
}

export function LinkBucket({
  bucket,
  intentId,
  items,
  onDeleteLink,
  onDropPeer
}: LinkBucketProps) {
  const [dragOver, setDragOver] = useState(false);
  const [addOpen, setAddOpen] = useState(false);

  const carriesIntent = (e: DragEvent) =>
    e.dataTransfer.types.includes(INTENT_DND_MIME);

  const handleDragOver = (e: DragEvent<HTMLElement>) => {
    if (!carriesIntent(e)) return;
    e.preventDefault();
    e.stopPropagation();
    e.dataTransfer.dropEffect = "link";
    if (!dragOver) setDragOver(true);
  };

  const handleDragLeave = (e: DragEvent<HTMLElement>) => {
    // dragleave срабатывает при заходе на дочерний элемент — фильтруем через
    // relatedTarget, чтобы подсветка не мигала.
    if (
      e.relatedTarget instanceof Node &&
      e.currentTarget.contains(e.relatedTarget)
    ) {
      return;
    }
    setDragOver(false);
  };

  const handleDrop = (e: DragEvent<HTMLElement>) => {
    if (!carriesIntent(e)) return;
    e.preventDefault();
    e.stopPropagation();
    setDragOver(false);
    const peerId = e.dataTransfer.getData(INTENT_DND_MIME);
    if (peerId && peerId !== intentId) onDropPeer(peerId);
  };

  return (
    <section
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
      className={[
        "flex flex-col gap-1.5 rounded-md transition-colors",
        dragOver
          ? "bg-primary/10 ring-2 ring-primary/40 ring-offset-1 ring-offset-base-100"
          : ""
      ].join(" ")}
    >
      <header className="flex items-center justify-between gap-2 px-1">
        <h3 className="m-0 inline-flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wider text-base-content/55">
          {bucketLabel[bucket]}
          <span className="rounded bg-base-200 px-1.5 py-px text-[10px] font-bold tabular-nums text-base-content/60">
            {items.length}
          </span>
        </h3>
        <button
          type="button"
          onClick={() => {
            setAddOpen(true);
          }}
          title={`Добавить связь «${bucketLabel[bucket]}»`}
          aria-label={`Добавить связь «${bucketLabel[bucket]}»`}
          className="inline-flex h-5 w-5 items-center justify-center rounded text-base-content/40 transition-colors hover:bg-base-200 hover:text-base-content focus-visible:outline-2 focus-visible:outline-primary focus-visible:outline-offset-1"
        >
          <Plus size={13} aria-hidden />
        </button>
      </header>
      <ul className="m-0 flex list-none flex-col gap-1.5 p-0">
        {items.map((view) => (
          <LinkCard
            key={view.link.id}
            view={view}
            onDelete={() => {
              onDeleteLink(view);
            }}
          />
        ))}
      </ul>
      <Popover
        open={addOpen}
        onClose={() => {
          setAddOpen(false);
        }}
        label={`Добавить связь: ${bucketLabel[bucket]}`}
      >
        <div className="flex flex-col gap-2">
          <h3 className="m-0 text-[13px] font-semibold text-base-content">
            Добавить связь: {bucketLabel[bucket]}
          </h3>
          <AddLinkForm
            intentId={intentId}
            presetBucket={bucket}
            compact
            autoFocus
            onCreated={() => {
              setAddOpen(false);
            }}
          />
        </div>
      </Popover>
    </section>
  );
}
