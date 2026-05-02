import { useEffect, useState } from "react";

import type { IntentQa } from "@/entities/intent-qa";
import type { IntentReview } from "@/entities/intent-review";
import type { TextVersion } from "@/entities/text-version";
import { HttpError, httpGet, intentsEndpoints } from "@/shared/api";

import { type ActivityEvent, buildActivityFeed } from "../model/types";

interface IntentActivityTimelineProps {
  intentId: string;
  reloadKey?: number;
}

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; events: ActivityEvent[] }
  | { kind: "error"; message: string };

const authorLabel: Record<TextVersion["changed_by"], string> = {
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

  if (state.kind === "loading") {
    return <p className="activity-timeline__hint">Активность загружается…</p>;
  }
  if (state.kind === "error") {
    return (
      <p role="alert" className="activity-timeline__hint">
        {state.message}
      </p>
    );
  }
  if (state.events.length === 0) {
    return <p className="activity-timeline__hint">Активности пока нет.</p>;
  }

  return (
    <ul className="activity-timeline">
      {state.events.map((event) => (
        <li
          className={`activity-timeline__item activity-timeline__item--${event.kind}`}
          key={eventKey(event)}
        >
          <header className="activity-timeline__header">
            <span
              className={`activity-timeline__badge activity-timeline__badge--${event.kind}`}
            >
              {eventTypeLabel[event.kind]}
            </span>
            <EventMeta event={event} />
            <time className="activity-timeline__time" dateTime={event.at}>
              {new Date(event.at).toLocaleString()}
            </time>
          </header>
          <EventBody event={event} />
        </li>
      ))}
    </ul>
  );
}

function EventMeta({ event }: { event: ActivityEvent }) {
  if (event.kind === "version") {
    return (
      <>
        <strong className="activity-timeline__primary">
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
        <span className="activity-timeline__primary">
          v{event.qa.intent_version_at_write}
        </span>
        <span>{authorLabel[event.qa.created_by]}</span>
      </>
    );
  }
  return (
    <>
      <span className="activity-timeline__primary">
        v{event.review.intent_version_at_write}
      </span>
      <span>{event.review.reason}</span>
      <span>{authorLabel[event.review.created_by]}</span>
    </>
  );
}

function EventBody({ event }: { event: ActivityEvent }) {
  if (event.kind === "version") {
    const v = event.version;
    if (v.kind === "create" && v.snapshot) {
      return <pre className="activity-timeline__diff">{v.snapshot}</pre>;
    }
    if (v.kind === "replace") {
      return (
        <div className="activity-timeline__diff">
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
      );
    }
    if (v.kind === "insert" && v.insert_text) {
      return (
        <pre className="activity-timeline__diff">
          <ins>{v.insert_text}</ins>
        </pre>
      );
    }
    return null;
  }
  if (event.kind === "qa") {
    return (
      <dl className="activity-timeline__qa">
        <dt>Вопрос</dt>
        <dd>{event.qa.question}</dd>
        <dt>Ответ</dt>
        <dd>{event.qa.answer}</dd>
      </dl>
    );
  }
  return <p className="activity-timeline__note">{event.review.note}</p>;
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
