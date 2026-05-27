import { useQueryClient, type QueryClient } from "@tanstack/react-query";

import { dreamsQueryKeys } from "@/entities/dream-session";
import { instructionPatchesQueryKeys } from "@/entities/instruction-patch";
import {
  intentLinksSummaryQueryKeys,
  intentsQueryKeys,
  type IntentListItem
} from "@/entities/intent";
import { intentEventsQueryKeys } from "@/entities/intent-event";
import {
  intentRepositoriesQueryKeys,
  type CloneStatus,
  type RepositoryBinding
} from "@/entities/repository-binding";
import { tagsQueryKeys } from "@/entities/tag";
import { useRealtimeEvent } from "@/shared/realtime";

/**
 * Маппинг realtime-событий в инвалидацию / точечный апдейт react-query кеша.
 * Один на всё приложение — никаких ручных `setReloadKey` в виджетах.
 * Подключается внутри `QueryClientProvider` в `App.tsx`.
 *
 * Стратегия по событиям:
 * - «list-shape» (`intent.created`) — invalidate списка, чтобы пришли свежие
 *   `text_short` и `sort_key` от сервера.
 * - точечные апдейты строк (`status_changed`, `tags_changed`, `pin*`,
 *   `reordered`, `text_changed`) — patch через `setQueryData`, чтобы не
 *   терять viewport канваса и не дёргать сеть.
 * - удаление — фильтр по списку + drop detail-кеша.
 */
const TEXT_SHORT_LEN = 140;

function patchListItem(
  qc: QueryClient,
  intentId: string,
  change: (item: IntentListItem) => IntentListItem
): void {
  qc.setQueryData<IntentListItem[]>(intentsQueryKeys.list(), (prev) =>
    prev ? prev.map((it) => (it.id === intentId ? change(it) : it)) : prev
  );
}

function upsertPin(
  pins: IntentListItem["pinned_in"],
  contextTagId: string,
  pinSortKey: string
): IntentListItem["pinned_in"] {
  const idx = pins.findIndex((p) => p.context_tag_id === contextTagId);
  const entry = { context_tag_id: contextTagId, pin_sort_key: pinSortKey };
  if (idx < 0) return [...pins, entry];
  const next = pins.slice();
  next[idx] = entry;
  return next;
}

export function RealtimeQueryBridge() {
  const qc = useQueryClient();

  useRealtimeEvent("tag.created", () => {
    void qc.invalidateQueries({ queryKey: tagsQueryKeys.all });
  });
  useRealtimeEvent("tag.updated", () => {
    void qc.invalidateQueries({ queryKey: tagsQueryKeys.all });
  });
  useRealtimeEvent("tag.deleted", () => {
    void qc.invalidateQueries({ queryKey: tagsQueryKeys.all });
  });

  useRealtimeEvent("instruction_patch.proposed", () => {
    void qc.invalidateQueries({ queryKey: instructionPatchesQueryKeys.all });
  });
  useRealtimeEvent("instruction_patch.applied", () => {
    void qc.invalidateQueries({ queryKey: instructionPatchesQueryKeys.all });
  });
  useRealtimeEvent("instruction_patch.rejected", () => {
    void qc.invalidateQueries({ queryKey: instructionPatchesQueryKeys.all });
  });
  useRealtimeEvent("instruction_patch.superseded", () => {
    void qc.invalidateQueries({ queryKey: instructionPatchesQueryKeys.all });
  });
  // A new DreamSession typically lands together with a batch of fresh
  // proposed patches (ADR-0022) — invalidate both branches so the UI surfaces
  // them without waiting for a manual reload.
  useRealtimeEvent("dream_session.recorded", () => {
    void qc.invalidateQueries({ queryKey: dreamsQueryKeys.all });
    void qc.invalidateQueries({ queryKey: instructionPatchesQueryKeys.all });
  });

  useRealtimeEvent("intent.created", () => {
    void qc.invalidateQueries({ queryKey: intentsQueryKeys.lists() });
  });
  useRealtimeEvent("intent.deleted", (payload) => {
    qc.setQueryData<IntentListItem[]>(intentsQueryKeys.list(), (prev) =>
      prev ? prev.filter((it) => it.id !== payload.intent_id) : prev
    );
    qc.removeQueries({ queryKey: intentsQueryKeys.detail(payload.intent_id) });
  });
  useRealtimeEvent("intent.text_changed", (payload) => {
    patchListItem(qc, payload.id, (it) => ({
      ...it,
      text_short: payload.text.slice(0, TEXT_SHORT_LEN),
      current_version: payload.current_version,
      updated_at: payload.updated_at
    }));
    qc.setQueryData(intentsQueryKeys.detail(payload.id), payload);
    void qc.invalidateQueries({
      queryKey: intentEventsQueryKeys.list(payload.id)
    });
  });
  useRealtimeEvent("intent.status_changed", (payload) => {
    patchListItem(qc, payload.id, (it) => ({
      ...it,
      status: payload.status,
      current_version: payload.current_version,
      updated_at: payload.updated_at
    }));
    qc.setQueryData(intentsQueryKeys.detail(payload.id), payload);
  });
  useRealtimeEvent("intent.tags_changed", (payload) => {
    patchListItem(qc, payload.id, (it) => ({
      ...it,
      tags: payload.tags,
      current_version: payload.current_version,
      updated_at: payload.updated_at
    }));
    qc.setQueryData(intentsQueryKeys.detail(payload.id), payload);
  });
  useRealtimeEvent("intent.pinned", (payload) => {
    patchListItem(qc, payload.intent_id, (it) => ({
      ...it,
      pinned_in: upsertPin(
        it.pinned_in,
        payload.context_tag_id,
        payload.pin_sort_key
      )
    }));
  });
  useRealtimeEvent("intent.pin_moved", (payload) => {
    patchListItem(qc, payload.intent_id, (it) => ({
      ...it,
      pinned_in: upsertPin(
        it.pinned_in,
        payload.context_tag_id,
        payload.pin_sort_key
      )
    }));
  });
  useRealtimeEvent("intent.unpinned", (payload) => {
    patchListItem(qc, payload.intent_id, (it) => ({
      ...it,
      pinned_in: it.pinned_in.filter(
        (p) => p.context_tag_id !== payload.context_tag_id
      )
    }));
  });
  useRealtimeEvent("intent.reordered", (payload) => {
    patchListItem(qc, payload.intent_id, (it) => ({
      ...it,
      sort_key: payload.sort_key
    }));
  });

  useRealtimeEvent("intent.attachment_added", (payload) => {
    void qc.invalidateQueries({
      queryKey: intentsQueryKeys.attachments(payload.intent_id)
    });
  });
  useRealtimeEvent("intent.attachment_deleted", (payload) => {
    void qc.invalidateQueries({
      queryKey: intentsQueryKeys.attachments(payload.intent_id)
    });
  });

  useRealtimeEvent("intent.repository_bound", (payload) => {
    void qc.invalidateQueries({
      queryKey: intentRepositoriesQueryKeys.list(payload.intent_id)
    });
  });
  useRealtimeEvent("intent.repository_unbound", (payload) => {
    void qc.invalidateQueries({
      queryKey: intentRepositoriesQueryKeys.list(payload.intent_id)
    });
  });
  useRealtimeEvent("intent.repository_clone_progress", (payload) => {
    qc.setQueryData<RepositoryBinding[]>(
      intentRepositoriesQueryKeys.list(payload.intent_id),
      (prev) =>
        prev?.map((b) =>
          b.id === payload.binding_id
            ? {
                ...b,
                clone_status: payload.status as CloneStatus,
                clone_error: payload.error
              }
            : b
        )
    );
  });

  useRealtimeEvent("intent.link_added", (payload) => {
    void qc.invalidateQueries({
      queryKey: intentEventsQueryKeys.list(payload.from_id)
    });
    void qc.invalidateQueries({
      queryKey: intentEventsQueryKeys.list(payload.to_id)
    });
    void qc.invalidateQueries({
      queryKey: intentsQueryKeys.detail(payload.from_id)
    });
    void qc.invalidateQueries({
      queryKey: intentsQueryKeys.detail(payload.to_id)
    });
    void qc.invalidateQueries({
      queryKey: intentLinksSummaryQueryKeys.all
    });
  });
  useRealtimeEvent("intent.link_removed", (payload) => {
    void qc.invalidateQueries({
      queryKey: intentEventsQueryKeys.list(payload.from_id)
    });
    void qc.invalidateQueries({
      queryKey: intentEventsQueryKeys.list(payload.to_id)
    });
    void qc.invalidateQueries({
      queryKey: intentsQueryKeys.detail(payload.from_id)
    });
    void qc.invalidateQueries({
      queryKey: intentsQueryKeys.detail(payload.to_id)
    });
    void qc.invalidateQueries({
      queryKey: intentLinksSummaryQueryKeys.all
    });
  });

  return null;
}
