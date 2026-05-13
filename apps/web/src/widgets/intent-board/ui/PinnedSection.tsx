import { Pin, PinOff } from "lucide-react";
import { useState, type DragEvent } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";

import { compareSortKeys, type IntentListItem } from "@/entities/intent";
import { movePin, pinIntent, unpinIntent } from "@/features/pin-intent";
import { INTENT_DND_MIME } from "@/shared/ui";

interface PinnedSectionProps {
  /** All loaded intents (sidebar already fetches this set). */
  items: readonly IntentListItem[];
  /**
   * Tag id of the currently selected sidebar context, or null when the user is
   * in a virtual context (Inbox / Без тегов / Архив / Холодильник). Drop-to-pin
   * is only enabled when this is non-null.
   */
  currentContextTagId: string | null;
  /** Hint label for the active context, used inside «Drop here» copy. */
  currentContextLabel: string;
  /**
   * Pinned intents that should appear in this section. Already sorted by the
   * caller — usually by pin_sort_key (server-defined) or by id for the
   * cross-context aggregate.
   */
  pinnedIntents: readonly IntentListItem[];
  /** Triggers a list refetch after optimistic-failure rollback. */
  onMutationFailed: () => void;
}

type DragMode =
  | { kind: "none" }
  | { kind: "external" }
  | { kind: "internal"; movedId: string };

interface DropTarget {
  intentId: string;
  position: "before" | "after";
}

export function PinnedSection({
  items,
  currentContextTagId,
  currentContextLabel,
  pinnedIntents,
  onMutationFailed
}: PinnedSectionProps) {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const [dragMode, setDragMode] = useState<DragMode>({ kind: "none" });
  const [dropTarget, setDropTarget] = useState<DropTarget | null>(null);

  const canAcceptExternal = currentContextTagId !== null;

  // The section is always rendered in tag contexts (even with no pins) so it
  // can serve as a drop target without needing global drag tracking.
  // `dragover` toggling local "external" state — payload isn't readable here
  // (security), but `dataTransfer.types` is, which is enough to recognise
  // intent drags. `movedId` is read at drop time, where getData works.
  const handleSectionDragOver = (e: DragEvent<HTMLElement>) => {
    if (!e.dataTransfer.types.includes(INTENT_DND_MIME)) return;
    if (!canAcceptExternal && dragMode.kind !== "internal") return;
    e.preventDefault();
    if (dragMode.kind === "none") {
      setDragMode({ kind: "external" });
    }
  };

  const handleSectionDragLeave = (e: DragEvent<HTMLElement>) => {
    // Only clear on real exit, not when the pointer crosses a child boundary.
    if (e.currentTarget.contains(e.relatedTarget as Node | null)) return;
    if (dragMode.kind === "external") {
      setDragMode({ kind: "none" });
    }
  };

  const handleSectionDrop = async (e: DragEvent<HTMLElement>) => {
    if (!e.dataTransfer.types.includes(INTENT_DND_MIME)) return;
    e.preventDefault();
    const movedId = e.dataTransfer.getData(INTENT_DND_MIME);
    const target = dropTarget;
    setDragMode({ kind: "none" });
    setDropTarget(null);
    if (!movedId) return;

    const isInternal = pinnedIntents.some((p) => p.id === movedId);
    const tagId = isInternal
      ? findPinContextTagId(items, movedId, currentContextTagId)
      : currentContextTagId;
    if (tagId === null) return;

    const { beforeId, afterId } = resolveNeighbours(
      pinnedIntents,
      movedId,
      target
    );

    try {
      if (isInternal && (beforeId !== null || afterId !== null)) {
        await movePin({
          intentId: movedId,
          contextTagId: tagId,
          beforeId,
          afterId
        });
      } else if (!isInternal) {
        await pinIntent({
          intentId: movedId,
          contextTagId: tagId,
          beforeId,
          afterId
        });
      }
    } catch {
      onMutationFailed();
    }
  };

  const handleRowDragStart = (e: DragEvent<HTMLLIElement>, id: string) => {
    e.dataTransfer.effectAllowed = "move";
    e.dataTransfer.setData("text/plain", id);
    e.dataTransfer.setData(INTENT_DND_MIME, id);
    setDragMode({ kind: "internal", movedId: id });
  };

  const handleRowDragOver = (e: DragEvent<HTMLLIElement>, id: string) => {
    if (dragMode.kind === "none") return;
    if (dragMode.kind === "internal" && dragMode.movedId === id) return;
    e.preventDefault();
    const rect = e.currentTarget.getBoundingClientRect();
    const position =
      e.clientY < rect.top + rect.height / 2 ? "before" : "after";
    setDropTarget((prev) =>
      prev?.intentId === id && prev.position === position
        ? prev
        : { intentId: id, position }
    );
  };

  const handleRowDragEnd = () => {
    setDragMode({ kind: "none" });
    setDropTarget(null);
  };

  const handleUnpin = async (intentId: string) => {
    const tagId = findPinContextTagId(items, intentId, currentContextTagId);
    if (tagId === null) return;
    try {
      await unpinIntent({ intentId, contextTagId: tagId });
    } catch {
      onMutationFailed();
    }
  };

  const goToIntent = (intentId: string) => {
    const search = params.toString();
    void navigate(
      search.length > 0
        ? `/intents/${intentId}?${search}`
        : `/intents/${intentId}`
    );
  };

  const isDragging = dragMode.kind !== "none";
  const ordered = [...pinnedIntents].sort((a, b) =>
    compareSortKeys(a.sort_key, b.sort_key)
  );
  const dropHint = canAcceptExternal
    ? `Запинить в #${currentContextLabel}`
    : "Выберите тег-контекст, чтобы запинить";

  return (
    <section
      aria-label="Запиненные intents"
      onDragOver={handleSectionDragOver}
      onDragLeave={handleSectionDragLeave}
      onDrop={(e) => {
        void handleSectionDrop(e);
      }}
      className={[
        "flex-shrink-0 border-b border-base-300 px-3 py-2",
        isDragging ? "bg-primary/5" : "bg-base-200/40"
      ].join(" ")}
    >
      <h3 className="m-0 mb-1 flex items-center gap-1.5 px-1 text-[11px] font-bold uppercase tracking-wider text-base-content/60">
        <Pin aria-hidden size={12} strokeWidth={2.5} />
        Pinned
      </h3>
      <ul className="m-0 flex list-none flex-col gap-0.5 p-0">
        {ordered.map((intent) => {
          const showLineBefore =
            dropTarget?.intentId === intent.id &&
            dropTarget.position === "before";
          const showLineAfter =
            dropTarget?.intentId === intent.id &&
            dropTarget.position === "after";
          return (
            <li
              key={intent.id}
              draggable
              onDragStart={(e) => {
                handleRowDragStart(e, intent.id);
              }}
              onDragOver={(e) => {
                handleRowDragOver(e, intent.id);
              }}
              onDragEnd={handleRowDragEnd}
              className={[
                "relative",
                showLineBefore
                  ? "before:absolute before:left-0 before:right-0 before:top-0 before:h-px before:bg-primary"
                  : "",
                showLineAfter
                  ? "after:absolute after:left-0 after:right-0 after:bottom-0 after:h-px after:bg-primary"
                  : ""
              ]
                .filter(Boolean)
                .join(" ")}
            >
              {/* Nested <button>s break HTML and suppress dragstart on the <li>.
                  Use a div+button pair instead so drag-reorder actually fires. */}
              <div
                role="button"
                tabIndex={0}
                onClick={() => {
                  goToIntent(intent.id);
                }}
                onKeyDown={(e) => {
                  if (e.key === "Enter" || e.key === " ") {
                    e.preventDefault();
                    goToIntent(intent.id);
                  }
                }}
                className="group flex w-full cursor-pointer items-center gap-2 rounded px-2 py-1 text-left text-[12px] transition-colors hover:bg-base-300/40 focus-visible:outline-2 focus-visible:outline-primary"
              >
                <Pin
                  aria-hidden
                  size={11}
                  strokeWidth={2.5}
                  className="flex-shrink-0 text-primary"
                />
                <span className="min-w-0 flex-1 truncate text-base-content">
                  {firstLine(intent.text_short) || intent.id}
                </span>
                <button
                  type="button"
                  onClick={(e) => {
                    e.stopPropagation();
                    void handleUnpin(intent.id);
                  }}
                  aria-label="Открепить"
                  title="Открепить"
                  className="hidden h-5 w-5 flex-shrink-0 items-center justify-center rounded text-base-content/50 transition-colors hover:bg-base-300 hover:text-base-content group-hover:flex"
                >
                  <PinOff aria-hidden size={11} strokeWidth={2.5} />
                </button>
              </div>
            </li>
          );
        })}
      </ul>
      {ordered.length === 0 || dragMode.kind === "external" ? (
        <div
          className={[
            "mt-1 rounded border-2 border-dashed px-2 py-2 text-center text-[11px]",
            dragMode.kind === "external" && canAcceptExternal
              ? "border-primary/50 bg-primary/5 text-primary"
              : "border-base-content/15 bg-base-200/30 text-base-content/50"
          ].join(" ")}
        >
          {ordered.length === 0 && dragMode.kind !== "external"
            ? "Перетащите карточку сюда, чтобы запинить"
            : dropHint}
        </div>
      ) : null}
    </section>
  );
}

function firstLine(text: string): string {
  return text.split(/\r?\n/, 1)[0] ?? "";
}

function findPinContextTagId(
  items: readonly IntentListItem[],
  intentId: string,
  fallback: string | null
): string | null {
  const item = items.find((i) => i.id === intentId);
  if (!item) return fallback;
  if (item.pinned_in.length === 0) return fallback;
  if (fallback && item.pinned_in.includes(fallback)) return fallback;
  return item.pinned_in[0];
}

function resolveNeighbours(
  pinned: readonly IntentListItem[],
  movedId: string,
  target: DropTarget | null
): { beforeId: string | null; afterId: string | null } {
  if (!target) {
    if (pinned.length === 0) return { beforeId: null, afterId: null };
    const tail = pinned[pinned.length - 1];
    return {
      beforeId: tail.id !== movedId ? tail.id : null,
      afterId: null
    };
  }
  const idx = pinned.findIndex((p) => p.id === target.intentId);
  if (idx < 0) return { beforeId: null, afterId: null };
  let beforeIdx = target.position === "before" ? idx - 1 : idx;
  let afterIdx = target.position === "before" ? idx : idx + 1;
  if (pinned[beforeIdx]?.id === movedId) beforeIdx -= 1;
  if (pinned[afterIdx]?.id === movedId) afterIdx += 1;
  return {
    beforeId: pinned[beforeIdx]?.id ?? null,
    afterId: pinned[afterIdx]?.id ?? null
  };
}
