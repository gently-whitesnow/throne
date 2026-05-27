import {
  Archive,
  CircleAlert,
  Hash,
  Inbox,
  Plus,
  Snowflake
} from "lucide-react";
import type { ReactNode } from "react";
import { useCallback, useEffect, useMemo } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";

import {
  ARCHIVE_STATUSES,
  FRIDGE_STATUS,
  INBOX_STATUSES,
  intentStatusMeta,
  useIntents,
  type IntentStatus
} from "@/entities/intent";
import { CreateIntentButton } from "@/features/create-intent";
import { HttpError } from "@/shared/api";
import {
  ARCHIVE_CONTEXT,
  FRIDGE_CONTEXT,
  INBOX_HELP_CONTEXT,
  INBOX_REVIEW_CONTEXT,
  UNTAGGED_CONTEXT,
  archiveSubContext,
  isArchiveContext
} from "@/shared/lib";

interface ContextRow {
  key: string;
  label: string;
  count: number;
  icon: "tag" | "untagged" | "archive";
}

export function IntentContextRail() {
  // TODO follow-up: counts endpoint for context rail — сейчас рейл вынужден
  // подтянуть весь список через useIntents (facade, который автодобирает все
  // страницы), чтобы посчитать теги/архив/inbox. На больших датасетах это
  // деградирует — нужен отдельный /api/v1/intents/contexts с агрегатами.
  const intentsQuery = useIntents();
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();

  const goToCreated = useCallback(
    (intentId: string) => {
      const search = params.toString();
      const target =
        search.length > 0
          ? `/intents/${intentId}?${search}`
          : `/intents/${intentId}`;
      void navigate(target);
    },
    [navigate, params]
  );

  const renderCreate = (
    initialTags: readonly string[] | undefined,
    initialStatus: IntentStatus | undefined,
    ariaLabel: string
  ): ReactNode => (
    <CreateIntentButton
      initialTags={initialTags}
      initialStatus={initialStatus}
      onCreated={(intent) => {
        goToCreated(intent.id);
      }}
      trigger={({ open }) => (
        <button
          type="button"
          onClick={(e) => {
            e.stopPropagation();
            open();
          }}
          aria-label={ariaLabel}
          className="flex h-6 w-6 items-center justify-center rounded text-base-content/60 transition-colors hover:bg-base-300/60 hover:text-base-content"
        >
          <Plus aria-hidden size={14} strokeWidth={2} />
        </button>
      )}
    />
  );

  const errorMessage = intentsQuery.isError
    ? intentsQuery.error instanceof HttpError
      ? `Не удалось загрузить контексты (${String(intentsQuery.error.status)}).`
      : "Не удалось загрузить контексты."
    : null;

  const {
    tagRows,
    untaggedCount,
    archiveCount,
    archiveTagRows,
    archiveUntaggedCount,
    fridgeCount,
    inboxReviewCount,
    inboxHelpCount
  } = useMemo(() => {
    const items = intentsQuery.data;
    if (!items) {
      return {
        tagRows: [] as ContextRow[],
        untaggedCount: 0,
        archiveCount: 0,
        archiveTagRows: [] as ContextRow[],
        archiveUntaggedCount: 0,
        fridgeCount: 0,
        inboxReviewCount: 0,
        inboxHelpCount: 0
      };
    }
    const counts = new Map<string, number>();
    const archiveCounts = new Map<string, number>();
    let untagged = 0;
    let archive = 0;
    let archiveUntagged = 0;
    let fridge = 0;
    let inboxReview = 0;
    let inboxHelp = 0;
    for (const item of items) {
      if (INBOX_STATUSES.has(item.status)) {
        if (item.status === "ready_for_review") inboxReview += 1;
        else if (item.status === "needs_help") inboxHelp += 1;
      }
      if (item.status === FRIDGE_STATUS) {
        fridge += 1;
        continue;
      }
      const isArchive = ARCHIVE_STATUSES.has(item.status);
      if (isArchive) {
        archive += 1;
        if (item.tags.length === 0) {
          archiveUntagged += 1;
        } else {
          for (const tag of item.tags) {
            archiveCounts.set(tag.name, (archiveCounts.get(tag.name) ?? 0) + 1);
          }
        }
        continue;
      }
      if (item.tags.length === 0) {
        untagged += 1;
        continue;
      }
      for (const tag of item.tags) {
        counts.set(tag.name, (counts.get(tag.name) ?? 0) + 1);
      }
    }
    const sortRows = (entries: Iterable<[string, number]>): ContextRow[] =>
      [...entries]
        .map(([name, count]) => ({
          key: name,
          label: name,
          count,
          icon: "tag" as const
        }))
        .sort((a, b) => {
          if (b.count !== a.count) return b.count - a.count;
          return a.label.localeCompare(b.label);
        });
    return {
      tagRows: sortRows(counts.entries()),
      untaggedCount: untagged,
      archiveCount: archive,
      archiveTagRows: sortRows(archiveCounts.entries()),
      archiveUntaggedCount: archiveUntagged,
      fridgeCount: fridge,
      inboxReviewCount: inboxReview,
      inboxHelpCount: inboxHelp
    };
  }, [intentsQuery.data]);

  const currentContext = params.get("context");
  const inboxTotal = inboxReviewCount + inboxHelpCount;

  // Auto-pick a default context once data is available.
  useEffect(() => {
    if (!intentsQuery.isSuccess) return;
    if (currentContext) return;
    let next: string | null = null;
    if (inboxTotal > 0) {
      next = inboxReviewCount > 0 ? INBOX_REVIEW_CONTEXT : INBOX_HELP_CONTEXT;
    } else if (tagRows.length > 0) next = tagRows[0].key;
    else if (untaggedCount > 0) next = UNTAGGED_CONTEXT;
    else if (fridgeCount > 0) next = FRIDGE_CONTEXT;
    else if (archiveCount > 0) next = ARCHIVE_CONTEXT;
    if (!next) return;
    const nextParams = new URLSearchParams(params);
    nextParams.set("context", next);
    setParams(nextParams, { replace: true });
  }, [
    archiveCount,
    currentContext,
    fridgeCount,
    inboxHelpCount,
    inboxReviewCount,
    inboxTotal,
    intentsQuery.isSuccess,
    params,
    setParams,
    tagRows,
    untaggedCount
  ]);

  const select = (key: string) => {
    const nextParams = new URLSearchParams(params);
    nextParams.set("context", key);
    setParams(nextParams);
  };

  const totalActive =
    tagRows.reduce((acc, row) => acc + row.count, 0) + untaggedCount;

  return (
    <aside
      className="flex min-h-0 min-w-0 flex-col overflow-hidden border-base-300 bg-base-100 max-md:border-b md:border-r"
      aria-label="Контексты Intents"
    >
      {intentsQuery.isSuccess && inboxTotal > 0 ? (
        <InboxWidget
          reviewCount={inboxReviewCount}
          helpCount={inboxHelpCount}
          activeContext={currentContext}
          onSelect={select}
        />
      ) : null}
      <div className="flex flex-shrink-0 items-center justify-between gap-3 border-b border-base-300 px-3.5 py-3">
        <h2 className="m-0 text-[13px] font-bold uppercase tracking-wider text-base-content/60">
          Контексты
        </h2>
        <span className="text-[11px] tabular-nums text-base-content/60">
          {String(totalActive)}
        </span>
      </div>
      <nav
        className="min-h-0 flex-1 overflow-y-auto py-1"
        aria-label="Список контекстов"
      >
        {intentsQuery.isPending ? (
          <p className="m-0 px-3.5 py-3 text-[13px] text-base-content/60">
            Загрузка…
          </p>
        ) : null}
        {errorMessage !== null ? (
          <p
            role="alert"
            className="m-0 px-3.5 py-3 text-[13px] text-base-content/60"
          >
            {errorMessage}
          </p>
        ) : null}
        {intentsQuery.isSuccess ? (
          <ul className="m-0 flex list-none flex-col p-0">
            {tagRows.map((row) => (
              <li key={row.key}>
                <RailRow
                  label={`#${row.label}`}
                  icon={<Hash aria-hidden size={14} strokeWidth={2} />}
                  count={row.count}
                  active={currentContext === row.key}
                  onSelect={() => {
                    select(row.key);
                  }}
                  action={renderCreate(
                    [row.key],
                    undefined,
                    `Создать intent с тегом #${row.label}`
                  )}
                />
              </li>
            ))}
            {untaggedCount > 0 ? (
              <li className="mt-1 border-t border-base-300 pt-1">
                <RailRow
                  label="Без тегов"
                  icon={<Inbox aria-hidden size={14} strokeWidth={2} />}
                  count={untaggedCount}
                  active={currentContext === UNTAGGED_CONTEXT}
                  onSelect={() => {
                    select(UNTAGGED_CONTEXT);
                  }}
                  muted
                />
              </li>
            ) : null}
            <li
              className={
                untaggedCount > 0 ? "" : "mt-1 border-t border-base-300 pt-1"
              }
            >
              <RailRow
                label="Холодильник"
                icon={<Snowflake aria-hidden size={14} strokeWidth={2} />}
                count={fridgeCount}
                active={currentContext === FRIDGE_CONTEXT}
                onSelect={() => {
                  select(FRIDGE_CONTEXT);
                }}
                muted
                action={renderCreate(
                  undefined,
                  FRIDGE_STATUS,
                  "Создать intent в холодильнике"
                )}
              />
            </li>
            <li>
              <RailRow
                label="Архив"
                icon={<Archive aria-hidden size={14} strokeWidth={2} />}
                count={archiveCount}
                active={currentContext === ARCHIVE_CONTEXT}
                onSelect={() => {
                  select(ARCHIVE_CONTEXT);
                }}
                muted
              />
            </li>
            {isArchiveContext(currentContext) && archiveCount > 0
              ? [
                  ...archiveTagRows.map((row) => (
                    <li key={`archive-${row.key}`}>
                      <RailRow
                        label={`#${row.label}`}
                        icon={<Hash aria-hidden size={14} strokeWidth={2} />}
                        count={row.count}
                        active={currentContext === archiveSubContext(row.key)}
                        onSelect={() => {
                          select(archiveSubContext(row.key));
                        }}
                        muted
                        nested
                        action={renderCreate(
                          [row.key],
                          undefined,
                          `Создать intent с тегом #${row.label}`
                        )}
                      />
                    </li>
                  )),
                  archiveUntaggedCount > 0 ? (
                    <li key="archive-untagged">
                      <RailRow
                        label="Без тегов"
                        icon={<Inbox aria-hidden size={14} strokeWidth={2} />}
                        count={archiveUntaggedCount}
                        active={
                          currentContext === archiveSubContext(UNTAGGED_CONTEXT)
                        }
                        onSelect={() => {
                          select(archiveSubContext(UNTAGGED_CONTEXT));
                        }}
                        muted
                        nested
                      />
                    </li>
                  ) : null
                ]
              : null}
            {tagRows.length === 0 &&
            untaggedCount === 0 &&
            fridgeCount === 0 &&
            archiveCount === 0 ? (
              <li className="px-3.5 py-3 text-[12px] text-base-content/60">
                Пока нет intents. Создайте первый — он определит контекст.
              </li>
            ) : null}
          </ul>
        ) : null}
      </nav>
    </aside>
  );
}

interface InboxWidgetProps {
  reviewCount: number;
  helpCount: number;
  activeContext: string | null;
  onSelect: (key: string) => void;
}

function InboxWidget({
  reviewCount,
  helpCount,
  activeContext,
  onSelect
}: InboxWidgetProps) {
  const reviewMeta = intentStatusMeta.ready_for_review;
  const helpMeta = intentStatusMeta.needs_help;
  return (
    <section
      aria-label="Inbox: intents, ждущие действия оператора"
      className="flex-shrink-0 border-b border-base-300 bg-base-200/40 px-3 py-2"
    >
      <h3 className="m-0 mb-1 flex items-center gap-1.5 px-1 text-[11px] font-bold uppercase tracking-wider text-base-content/60">
        <Inbox aria-hidden size={12} strokeWidth={2.5} />
        Inbox
      </h3>
      <ul className="m-0 flex list-none flex-col gap-0.5 p-0">
        {reviewCount > 0 ? (
          <li>
            <InboxRow
              label="Жду ревью"
              count={reviewCount}
              ink={reviewMeta.ink}
              surface={reviewMeta.surface}
              icon={<Inbox aria-hidden size={14} strokeWidth={2} />}
              active={activeContext === INBOX_REVIEW_CONTEXT}
              onSelect={() => {
                onSelect(INBOX_REVIEW_CONTEXT);
              }}
            />
          </li>
        ) : null}
        {helpCount > 0 ? (
          <li>
            <InboxRow
              label="Нужна помощь"
              count={helpCount}
              ink={helpMeta.ink}
              surface={helpMeta.surface}
              icon={<CircleAlert aria-hidden size={14} strokeWidth={2} />}
              active={activeContext === INBOX_HELP_CONTEXT}
              onSelect={() => {
                onSelect(INBOX_HELP_CONTEXT);
              }}
            />
          </li>
        ) : null}
      </ul>
    </section>
  );
}

interface InboxRowProps {
  label: string;
  count: number;
  ink: string;
  surface: string;
  icon: React.ReactNode;
  active: boolean;
  onSelect: () => void;
}

function InboxRow({
  label,
  count,
  ink,
  surface,
  icon,
  active,
  onSelect
}: InboxRowProps) {
  return (
    <button
      type="button"
      onClick={onSelect}
      aria-current={active ? "true" : undefined}
      className={[
        "flex w-full items-center gap-2 rounded px-2 py-1.5 text-left text-[13px] font-semibold transition-colors",
        active ? "ring-1 ring-primary" : "hover:bg-base-300/40"
      ].join(" ")}
      style={{ background: surface, color: ink }}
    >
      <span aria-hidden style={{ color: ink }}>
        {icon}
      </span>
      <span className="min-w-0 flex-1 truncate">{label}</span>
      <span className="tabular-nums text-[11px] opacity-70">
        {String(count)}
      </span>
    </button>
  );
}

interface RailRowProps {
  label: string;
  icon: React.ReactNode;
  count: number;
  active: boolean;
  onSelect: () => void;
  muted?: boolean;
  nested?: boolean;
  action?: ReactNode;
}

function RailRow({
  label,
  icon,
  count,
  active,
  onSelect,
  muted,
  nested,
  action
}: RailRowProps) {
  return (
    <div
      className={[
        "group relative flex w-full items-center border-l-[3px] transition-colors",
        active
          ? "border-primary bg-primary/10"
          : "border-transparent hover:bg-base-200"
      ].join(" ")}
    >
      <button
        type="button"
        onClick={onSelect}
        aria-current={active ? "true" : undefined}
        className={[
          "flex min-w-0 flex-1 items-center gap-2 py-1.5 pr-9 text-left text-[13px]",
          nested ? "pl-8" : "pl-3.5",
          active
            ? "font-semibold text-primary"
            : muted
              ? "text-base-content/70"
              : "text-base-content"
        ].join(" ")}
      >
        <span
          className={
            active
              ? "text-primary"
              : muted
                ? "text-base-content/50"
                : "text-base-content/70"
          }
        >
          {icon}
        </span>
        <span className="min-w-0 flex-1 truncate">{label}</span>
      </button>
      <div className="pointer-events-none absolute right-2 top-1/2 flex h-6 w-6 -translate-y-1/2 items-center justify-center">
        <span
          aria-hidden={action ? "true" : undefined}
          className={[
            "tabular-nums text-[11px] transition-opacity",
            action ? "group-hover:opacity-0 group-focus-within:opacity-0" : "",
            active ? "text-primary/80" : "text-base-content/40"
          ].join(" ")}
        >
          {String(count)}
        </span>
        {action ? (
          <div className="pointer-events-auto absolute inset-0 opacity-0 transition-opacity group-hover:opacity-100 group-focus-within:opacity-100">
            {action}
          </div>
        ) : null}
      </div>
    </div>
  );
}
