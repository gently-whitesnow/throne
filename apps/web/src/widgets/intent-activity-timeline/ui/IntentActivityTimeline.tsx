import { Activity, Bot, Cog, Link2, Link2Off, User } from "lucide-react";
import { useMemo } from "react";

import { useIntentEvents, type IntentEvent } from "@/entities/intent-event";
import { HttpError } from "@/shared/api";
import { dayKey, formatDateLabel, formatRelativeTime } from "@/shared/lib";
import { CollapsibleSection } from "@/shared/ui";

import { type ActivityFeedItem, buildActivityFeed } from "../model/types";

interface IntentActivityTimelineProps {
  intentId: string;
}

const authorLabel: Record<NonNullable<IntentEvent["created_by"]>, string> = {
  user: "Пользователь",
  agent: "Агент",
  system: "Система"
};

const linkTypeLabel: Record<string, string> = {
  relates: "связан с",
  blocks: "блокирует",
  derived_from: "происходит из",
  duplicate_of: "дубликат"
};

export function IntentActivityTimeline({
  intentId
}: IntentActivityTimelineProps) {
  const eventsQuery = useIntentEvents(intentId);
  const items: ActivityFeedItem[] = useMemo(
    () => (eventsQuery.data ? buildActivityFeed(eventsQuery.data) : []),
    [eventsQuery.data]
  );

  const grouped = useMemo(() => {
    if (!eventsQuery.isSuccess) return [];
    const groups: { key: string; label: string; items: ActivityFeedItem[] }[] =
      [];
    for (const item of items) {
      const key = dayKey(item.event.created_at);
      const last = groups.at(-1);
      if (last?.key === key) {
        last.items.push(item);
      } else {
        groups.push({
          key,
          label: formatDateLabel(item.event.created_at),
          items: [item]
        });
      }
    }
    return groups;
  }, [eventsQuery.isSuccess, items]);

  // Пустую/ещё не загруженную активность не разворачиваем в блок — без событий
  // секции на странице нет вовсе (прогрессивное раскрытие).
  if (eventsQuery.isPending) return null;
  if (eventsQuery.isError) {
    const err = eventsQuery.error;
    const message =
      err instanceof HttpError
        ? `Не удалось загрузить активность (${String(err.status)}).`
        : "Не удалось загрузить активность.";
    return (
      <p role="alert" className="m-0 text-xs text-error">
        {message}
      </p>
    );
  }
  if (items.length === 0) return null;

  return (
    <CollapsibleSection
      aria-label="Активность"
      icon={<Activity size={14} strokeWidth={2} />}
      title="Активность"
      count={items.length}
    >
      <div className="flex flex-col gap-3 pt-2">
        {grouped.map((group) => (
          <section key={group.key} className="flex flex-col gap-2">
            <h3 className="m-0 text-[11px] font-semibold uppercase tracking-wider text-base-content/50">
              {group.label}
            </h3>
            <ul
              className="relative m-0 flex list-none flex-col gap-3 p-0 pl-7
              before:absolute before:left-[13px] before:top-1.5 before:bottom-1.5
              before:w-px before:bg-base-300"
            >
              {group.items.map((item) => (
                <li className="relative" key={item.event.id}>
                  <Avatar event={item.event} viewerIntentId={intentId} />
                  <div className="rounded-md border border-base-300 bg-base-100 px-3 py-2.5">
                    <header className="mb-1.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-[11px] text-base-content/60">
                      <EventBadge event={item.event} />
                      <EventMeta event={item.event} viewerIntentId={intentId} />
                      <time
                        className="ml-auto tabular-nums"
                        dateTime={item.event.created_at}
                        title={new Date(item.event.created_at).toLocaleString()}
                      >
                        {formatRelativeTime(item.event.created_at)}
                      </time>
                    </header>
                    <EventBody event={item.event} />
                  </div>
                </li>
              ))}
            </ul>
          </section>
        ))}
      </div>
    </CollapsibleSection>
  );
}

function Avatar({
  event,
  viewerIntentId
}: {
  event: IntentEvent;
  viewerIntentId: string;
}) {
  const isLink = event.kind !== "text_changed";
  if (isLink) {
    const Icon = event.kind === "link_added" ? Link2 : Link2Off;
    const ring =
      event.kind === "link_added"
        ? "border-primary/30 bg-primary/10 text-primary"
        : "border-base-300 bg-base-200 text-base-content/60";
    return (
      <span
        aria-hidden
        className={`absolute -left-7 top-1.5 inline-flex h-6 w-6 items-center justify-center rounded-full border ${ring}`}
      >
        <Icon size={12} strokeWidth={2.2} />
      </span>
    );
  }
  // Suppress lint: viewerIntentId only used in EventMeta for link events.
  void viewerIntentId;
  const author = event.created_by ?? "system";
  const Icon = author === "agent" ? Bot : author === "system" ? Cog : User;
  const ring =
    author === "agent"
      ? "border-success/40 bg-success/10 text-success"
      : author === "system"
        ? "border-base-300 bg-base-200 text-base-content/60"
        : "border-primary/30 bg-primary/10 text-primary";
  return (
    <span
      aria-hidden
      className={`absolute -left-7 top-1.5 inline-flex h-6 w-6 items-center justify-center rounded-full border ${ring}`}
    >
      <Icon size={12} strokeWidth={2.2} />
    </span>
  );
}

function EventBadge({ event }: { event: IntentEvent }) {
  const cls =
    "inline-flex items-center rounded-full border px-1.5 py-px text-[10px] font-semibold uppercase tracking-wide";
  if (event.kind === "text_changed") {
    return (
      <span className={`${cls} border-primary/20 bg-primary/10 text-primary`}>
        Версия
      </span>
    );
  }
  if (event.kind === "link_added") {
    return (
      <span className={`${cls} border-success/30 bg-success/10 text-success`}>
        Связь
      </span>
    );
  }
  return (
    <span className={`${cls} border-base-300 bg-base-200 text-base-content/60`}>
      Связь снята
    </span>
  );
}

function EventMeta({
  event,
  viewerIntentId
}: {
  event: IntentEvent;
  viewerIntentId: string;
}) {
  const author = event.created_by ?? "system";
  if (event.kind === "text_changed") {
    return (
      <>
        <strong className="font-semibold text-base-content">
          v{event.version ?? 0}
        </strong>
        <span>{textChangeKindLabel(event.text_change?.kind)}</span>
        <span>{authorLabel[author]}</span>
      </>
    );
  }
  // Link events. `intent_id` is from_id; `peer_intent_id` is to_id.
  const isOutgoing = event.intent_id === viewerIntentId;
  const peerId = isOutgoing ? event.peer_intent_id : event.intent_id;
  const linkType = event.link?.type ?? "relates";
  const verb =
    event.kind === "link_added"
      ? isOutgoing
        ? (linkTypeLabel[linkType] ?? "связан с")
        : reverseLinkLabel(linkType)
      : "удалена связь";
  return (
    <>
      <span>{verb}</span>
      <code className="rounded bg-base-200 px-1 font-mono text-[10px] text-base-content/70">
        {peerId ?? "?"}
      </code>
      <span>{authorLabel[author]}</span>
    </>
  );
}

function textChangeKindLabel(kind: string | undefined): string {
  switch (kind) {
    case "create":
      return "создание";
    case "replace":
      return "правка";
    case "insert":
      return "вставка";
    default:
      return "правка";
  }
}

function reverseLinkLabel(type: string): string {
  if (type === "blocks") return "заблокирован";
  if (type === "derived_from") return "источник для";
  return linkTypeLabel[type] ?? "связан с";
}

const diffClass =
  "m-0 whitespace-pre-wrap break-words rounded bg-base-200 p-2 font-mono text-xs";

function EventBody({ event }: { event: IntentEvent }) {
  if (event.kind !== "text_changed") {
    if (event.link?.rationale) {
      return (
        <p className="m-0 text-[12px] text-base-content/80">
          {event.link.rationale}
        </p>
      );
    }
    return null;
  }
  const t = event.text_change;
  if (!t) return null;
  if (t.kind === "create" && t.snapshot) {
    return <pre className={diffClass}>{t.snapshot}</pre>;
  }
  if (t.kind === "replace") {
    return (
      <div className={diffClass}>
        {t.old_text ? (
          <pre className="m-0 whitespace-pre-wrap break-words">
            <del className="bg-error/10 line-through">{t.old_text}</del>
          </pre>
        ) : null}
        {t.new_text ? (
          <pre className="m-0 whitespace-pre-wrap break-words">
            <ins className="bg-success/10 no-underline">{t.new_text}</ins>
          </pre>
        ) : null}
      </div>
    );
  }
  if (t.kind === "insert" && t.insert_text) {
    return (
      <pre className={diffClass}>
        <ins className="bg-success/10 no-underline">{t.insert_text}</ins>
      </pre>
    );
  }
  return null;
}
