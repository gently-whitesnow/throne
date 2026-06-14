import { Plus } from "lucide-react";
import type { ReactNode } from "react";
import { useCallback, useEffect } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";

import { useIntentContexts, type IntentStatus } from "@/entities/intent";
import { CreateIntentButton } from "@/features/create-intent";
import {
  ARCHIVE_CONTEXT,
  FRIDGE_CONTEXT,
  INBOX_HELP_CONTEXT,
  INBOX_REVIEW_CONTEXT,
  UNTAGGED_CONTEXT,
  errorMessage
} from "@/shared/lib";

import { ContextList } from "./ContextList";
import { InboxWidget } from "./InboxWidget";
import { useContextRows } from "../model/use-context-rows";

export function IntentContextRail() {
  const contextsQuery = useIntentContexts();
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

  const loadErrorMessage = contextsQuery.isError
    ? errorMessage(contextsQuery.error, {
        base: "Не удалось загрузить контексты"
      })
    : null;

  const {
    tagRows,
    untaggedCount,
    archiveCount,
    archiveTagRows,
    archiveUntaggedCount,
    fridgeCount,
    inboxReviewCount,
    inboxHelpCount,
    terminalRunningCount
  } = useContextRows(contextsQuery.data);

  const currentContext = params.get("context");
  const inboxTotal = inboxReviewCount + inboxHelpCount;

  // Auto-pick a default context once data is available.
  useEffect(() => {
    if (!contextsQuery.isSuccess) return;
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
    contextsQuery.isSuccess,
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
      {contextsQuery.isSuccess &&
      (inboxTotal > 0 || terminalRunningCount > 0) ? (
        <InboxWidget
          reviewCount={inboxReviewCount}
          helpCount={inboxHelpCount}
          terminalRunningCount={terminalRunningCount}
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
        {contextsQuery.isPending ? (
          <p className="m-0 px-3.5 py-3 text-[13px] text-base-content/60">
            Загрузка…
          </p>
        ) : null}
        {loadErrorMessage !== null ? (
          <p
            role="alert"
            className="m-0 px-3.5 py-3 text-[13px] text-base-content/60"
          >
            {loadErrorMessage}
          </p>
        ) : null}
        {contextsQuery.isSuccess ? (
          <ContextList
            tagRows={tagRows}
            untaggedCount={untaggedCount}
            fridgeCount={fridgeCount}
            archiveCount={archiveCount}
            archiveTagRows={archiveTagRows}
            archiveUntaggedCount={archiveUntaggedCount}
            currentContext={currentContext}
            onSelect={select}
            renderCreate={renderCreate}
          />
        ) : null}
      </nav>
    </aside>
  );
}
