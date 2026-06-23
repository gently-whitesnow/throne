import { useState } from "react";

import {
  refreshIntentRepository,
  syncIntentRepositoryBranch,
  unbindIntentRepository,
  type RepositoryBinding
} from "@/entities/repository-binding";
import { errorMessage, httpErrorStatus } from "@/shared/lib";

interface UseBindingRowActionsArgs {
  intentId: string;
  binding: RepositoryBinding;
  fullName: string;
  /** Patches this row in the cache from a `refresh` / `sync-branch` response. */
  onRefreshed: (binding: RepositoryBinding) => void;
  /** Optimistic removal hook fired after a successful DELETE. */
  onUnbound: (bindingId: string) => void;
}

/**
 * Overflow-menu actions of a repository row (refresh / sync-branch / delete) with their
 * loading + error state. Extracted from the row component so the view stays within the
 * file-size budget. Each action owns a confirm where it is destructive:
 *
 *  - syncBranch — `git fetch` + `git reset --hard origin/{branch}` on the server. Rewrites
 *    the working tree, so uncommitted local changes are lost — the confirm spells this out.
 *  - unbind — removes the binding AND its on-disk folder; confirm covers the data loss.
 *
 * Refresh stays confirm-free: it only restores a missing clone / dotягивает PR metadata.
 */
export function useBindingRowActions({
  intentId,
  binding,
  fullName,
  onRefreshed,
  onUnbound
}: UseBindingRowActionsArgs) {
  const [refreshing, setRefreshing] = useState(false);
  const [syncingBranch, setSyncingBranch] = useState(false);
  const [unbinding, setUnbinding] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const busy = refreshing || syncingBranch || unbinding;

  async function refresh() {
    setRefreshing(true);
    setError(null);
    try {
      onRefreshed(await refreshIntentRepository(intentId, binding.id));
    } catch (err) {
      setError(
        errorMessage(err, {
          base: "Не удалось обновить",
          fallback: "Не удалось обновить репозиторий."
        })
      );
    } finally {
      setRefreshing(false);
    }
  }

  async function syncBranch() {
    const confirmed = window.confirm(
      `Синхронизировать ветку ${fullName} с удалённым репозиторием?\n\n` +
        "Текущая ветка локального клона будет жёстко сброшена на состояние удалённой " +
        "(git fetch + reset --hard). Незакоммиченные изменения в рабочей папке и локальные " +
        "коммиты, которых нет на сервере, будут утеряны без возможности восстановления.\n\n" +
        "Продолжить синхронизацию?"
    );
    if (!confirmed) return;
    setSyncingBranch(true);
    setError(null);
    try {
      onRefreshed(await syncIntentRepositoryBranch(intentId, binding.id));
    } catch (err) {
      setError(
        errorMessage(err, {
          base: "Не удалось синхронизировать ветку",
          fallback: "Не удалось синхронизировать ветку репозитория."
        })
      );
    } finally {
      setSyncingBranch(false);
    }
  }

  async function unbind() {
    const confirmed = window.confirm(
      `Удалить репозиторий ${fullName}?\n\n` +
        "Рабочая папка этого репозитория будет удалена с диска без возможности " +
        "восстановления. Несохранённые изменения, локальные ветки и файлы, " +
        "которых нет на GitHub, пропадут.\n\nПродолжить удаление?"
    );
    if (!confirmed) return;
    setUnbinding(true);
    setError(null);
    try {
      await unbindIntentRepository(intentId, binding.id);
      onUnbound(binding.id);
    } catch (err) {
      const status = httpErrorStatus(err);
      setError(
        status !== undefined
          ? `Не удалось удалить (${String(status)}). Папка могла быть занята — закройте процессы и повторите.`
          : "Не удалось удалить репозиторий."
      );
      setUnbinding(false);
    }
  }

  return {
    refresh,
    syncBranch,
    unbind,
    refreshing,
    syncingBranch,
    unbinding,
    busy,
    error
  };
}
