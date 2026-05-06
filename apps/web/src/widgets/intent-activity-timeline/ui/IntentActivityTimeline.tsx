import { Bot, Cog, User } from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import type { IntentQa } from "@/entities/intent-qa";
import type { IntentReview } from "@/entities/intent-review";
import type { TextVersion } from "@/entities/text-version";
import { HttpError, httpGet, intentsEndpoints } from "@/shared/api";
import { dayKey, formatDateLabel, formatRelativeTime } from "@/shared/lib";

import { type ActivityEvent, buildActivityFeed } from "../model/types";

interface IntentActivityTimelineProps {
  intentId: string;
  reloadKey?: number;
}

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; events: ActivityEvent[] }
  | { kind: "error"; message: string };

type AuthorKind = "user" | "agent" | "system";

const authorLabel: Record<AuthorKind, string> = {
  user: "Пользователь",
  agent: "Агент",
  system: "Система"
};

const versionKindLabel: Record<TextVersion["kind"], string> = {
  create: "создание",
  replace: "правка",
  insert: "вставка"
};

const eventTypeLabel: Record<ActivityEvent["kind"], string> = {
  version: "Версия",
  qa: "Q/A",
  review: "Review"
};

export function IntentActivityTimeline({
  intentId,
  reloadKey = 0
}: IntentActivityTimelineProps) {
  const [state, setState] = useState<LoadState>({ kind: "loading" });

  useEffect(() => {
    const controller = new AbortController();
    setState({ kind: "loading" });
    Promise.all([
      httpGet<TextVersion[]>(
        intentsEndpoints.listIntentVersions(intentId),
        controller.signal
      ),
      httpGet<IntentQa[]>(
        intentsEndpoints.listIntentQa(intentId),
        controller.signal
      ),
      httpGet<IntentReview[]>(
        intentsEndpoints.listIntentReviews(intentId),
        controller.signal
      )
    ])
      .then(([versions, qa, reviews]) => {
        setState({
          kind: "ready",
          events: buildActivityFeed(versions, qa, reviews)
        });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        const message =
          err instanceof HttpError
            ? `Не удалось загрузить активность (${String(err.status)}).`
            : "Не удалось загрузить активность.";
        setState({ kind: "error", message });
      });
    return () => {
      controller.abort();
    };
  }, [intentId, reloadKey]);

  const grouped = useMemo(() => {
    if (state.kind !== "ready") return [];
    const groups: { key: string; label: string; events: ActivityEvent[] }[] =
      [];
    for (const event of state.events) {
      const key = dayKey(event.at);
      const last = groups.at(-1);
      if (last?.key === key) {
        last.events.push(event);
      } else {
        groups.push({ key, label: formatDateLabel(event.at), events: [event] });
      }
    }
    return groups;
  }, [state]);

  if (state.kind === "loading") {
    return (
      <p className="m-0 text-xs text-base-content/60">
        Активность загружается…
      </p>
    );
  }
  if (state.kind === "error") {
    return (
      <p role="alert" className="m-0 text-xs text-error">
        {state.message}
      </p>
    );
  }
  if (state.events.length === 0) {
    return (
      <p className="m-0 text-xs text-base-content/60">Активности пока нет.</p>
    );
  }

  return (
    <div className="flex flex-col gap-3">
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
            {group.events.map((event) => (
              <li className="relative" key={eventKey(event)}>
                <Avatar event={event} />
                <div className="rounded-md border border-base-300 bg-base-100 px-3 py-2.5">
                  <header className="mb-1.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-[11px] text-base-content/60">
                    <span className={badgeClass(event.kind)}>
                      {eventTypeLabel[event.kind]}
                    </span>
                    <EventMeta event={event} />
                    <time
                      className="ml-auto tabular-nums"
                      dateTime={event.at}
                      title={new Date(event.at).toLocaleString()}
                    >
                      {formatRelativeTime(event.at)}
                    </time>
                  </header>
                  <EventBody event={event} />
                </div>
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}

function Avatar({ event }: { event: ActivityEvent }) {
  const author = eventAuthor(event);
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

function eventAuthor(event: ActivityEvent): AuthorKind {
  if (event.kind === "version") return event.version.changed_by;
  if (event.kind === "qa") return event.qa.created_by;
  return event.review.created_by;
}

function badgeClass(kind: ActivityEvent["kind"]): string {
  const base =
    "inline-flex items-center rounded-full border px-1.5 py-px text-[10px] font-semibold uppercase tracking-wide";
  if (kind === "version") {
    return `${base} border-primary/20 bg-primary/10 text-primary`;
  }
  if (kind === "qa") {
    return `${base} border-success/20 bg-success/10 text-success`;
  }
  return `${base} border-warning/20 bg-warning/10 text-warning`;
}

function EventMeta({ event }: { event: ActivityEvent }) {
  if (event.kind === "version") {
    return (
      <>
        <strong className="font-semibold text-base-content">
          v{event.version.version}
        </strong>
        <span>{versionKindLabel[event.version.kind]}</span>
        <span>{authorLabel[event.version.changed_by]}</span>
      </>
    );
  }
  if (event.kind === "qa") {
    return (
      <>
        <span className="font-semibold text-base-content">
          v{event.qa.intent_version_at_write}
        </span>
        <span>{authorLabel[event.qa.created_by]}</span>
      </>
    );
  }
  return (
    <>
      <span className="font-semibold text-base-content">
        v{event.review.intent_version_at_write}
      </span>
      <span>{event.review.reason}</span>
      <span>{authorLabel[event.review.created_by]}</span>
    </>
  );
}

const diffClass =
  "m-0 whitespace-pre-wrap break-words rounded bg-base-200 p-2 font-mono text-xs";

function EventBody({ event }: { event: ActivityEvent }) {
  if (event.kind === "version") {
    const v = event.version;
    if (v.kind === "create" && v.snapshot) {
      return <pre className={diffClass}>{v.snapshot}</pre>;
    }
    if (v.kind === "replace") {
      return (
        <div className={diffClass}>
          {v.old_text ? (
            <pre className="m-0">
              <del className="bg-error/10 line-through">{v.old_text}</del>
            </pre>
          ) : null}
          {v.new_text ? (
            <pre className="m-0">
              <ins className="bg-success/10 no-underline">{v.new_text}</ins>
            </pre>
          ) : null}
        </div>
      );
    }
    if (v.kind === "insert" && v.insert_text) {
      return (
        <pre className={diffClass}>
          <ins className="bg-success/10 no-underline">{v.insert_text}</ins>
        </pre>
      );
    }
    return null;
  }
  if (event.kind === "qa") {
    return (
      <dl className="m-0 grid grid-cols-[80px_1fr] gap-x-3 gap-y-1 text-[13px]">
        <dt className="font-semibold text-base-content/60">Вопрос</dt>
        <dd className="m-0 whitespace-pre-wrap text-base-content">
          {event.qa.question}
        </dd>
        <dt className="font-semibold text-base-content/60">Ответ</dt>
        <dd className="m-0 whitespace-pre-wrap text-base-content">
          {event.qa.answer}
        </dd>
      </dl>
    );
  }
  return (
    <p className="m-0 whitespace-pre-wrap text-[13px] text-base-content">
      {event.review.note}
    </p>
  );
}

function eventKey(event: ActivityEvent): string {
  switch (event.kind) {
    case "version":
      return `version:${String(event.version.version)}`;
    case "qa":
      return `qa:${event.qa.id}`;
    case "review":
      return `review:${event.review.id}`;
  }
}
