import { Archive, Hash, Inbox, Snowflake } from "lucide-react";
import type { ReactNode } from "react";

import { FRIDGE_STATUS, type IntentStatus } from "@/entities/intent";
import {
  ARCHIVE_CONTEXT,
  FRIDGE_CONTEXT,
  UNTAGGED_CONTEXT,
  archiveSubContext,
  fridgeSubContext,
  isArchiveContext,
  isFridgeContext
} from "@/shared/lib";

import { RailRow } from "./RailRow";
import type { ContextRow } from "../model/use-context-rows";

interface ContextListProps {
  tagRows: ContextRow[];
  untaggedCount: number;
  fridgeCount: number;
  fridgeTagRows: ContextRow[];
  fridgeUntaggedCount: number;
  archiveCount: number;
  archiveTagRows: ContextRow[];
  archiveUntaggedCount: number;
  currentContext: string | null;
  onSelect: (key: string) => void;
  renderCreate: (
    initialTags: readonly string[] | undefined,
    initialStatus: IntentStatus | undefined,
    ariaLabel: string
  ) => ReactNode;
}

export function ContextList({
  tagRows,
  untaggedCount,
  fridgeCount,
  fridgeTagRows,
  fridgeUntaggedCount,
  archiveCount,
  archiveTagRows,
  archiveUntaggedCount,
  currentContext,
  onSelect,
  renderCreate
}: ContextListProps) {
  return (
    <ul className="m-0 flex list-none flex-col p-0">
      {tagRows.map((row) => (
        <li key={row.key}>
          <RailRow
            label={`#${row.label}`}
            icon={<Hash aria-hidden size={14} strokeWidth={2} />}
            count={row.count}
            active={currentContext === row.key}
            onSelect={() => {
              onSelect(row.key);
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
              onSelect(UNTAGGED_CONTEXT);
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
            onSelect(FRIDGE_CONTEXT);
          }}
          muted
          action={renderCreate(
            undefined,
            FRIDGE_STATUS,
            "Создать intent в холодильнике"
          )}
        />
      </li>
      {isFridgeContext(currentContext) && fridgeCount > 0
        ? [
            ...fridgeTagRows.map((row) => (
              <li key={`fridge-${row.key}`}>
                <RailRow
                  label={`#${row.label}`}
                  icon={<Hash aria-hidden size={14} strokeWidth={2} />}
                  count={row.count}
                  active={currentContext === fridgeSubContext(row.key)}
                  onSelect={() => {
                    onSelect(fridgeSubContext(row.key));
                  }}
                  muted
                  nested
                  action={renderCreate(
                    [row.key],
                    FRIDGE_STATUS,
                    `Создать intent в холодильнике с тегом #${row.label}`
                  )}
                />
              </li>
            )),
            fridgeUntaggedCount > 0 ? (
              <li key="fridge-untagged">
                <RailRow
                  label="Без тегов"
                  icon={<Inbox aria-hidden size={14} strokeWidth={2} />}
                  count={fridgeUntaggedCount}
                  active={currentContext === fridgeSubContext(UNTAGGED_CONTEXT)}
                  onSelect={() => {
                    onSelect(fridgeSubContext(UNTAGGED_CONTEXT));
                  }}
                  muted
                  nested
                />
              </li>
            ) : null
          ]
        : null}
      <li>
        <RailRow
          label="Архив"
          icon={<Archive aria-hidden size={14} strokeWidth={2} />}
          count={archiveCount}
          active={currentContext === ARCHIVE_CONTEXT}
          onSelect={() => {
            onSelect(ARCHIVE_CONTEXT);
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
                    onSelect(archiveSubContext(row.key));
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
                    onSelect(archiveSubContext(UNTAGGED_CONTEXT));
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
  );
}
