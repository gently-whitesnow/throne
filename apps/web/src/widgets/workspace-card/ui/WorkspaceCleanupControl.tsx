import { useQueryClient } from "@tanstack/react-query";
import { Eraser, Trash2 } from "lucide-react";
import { useState } from "react";

import {
  cleanWorkspace,
  formatWorkspaceSize,
  workspaceSettingsQueryKeys,
  type WorkspaceCleanMode,
  type WorkspaceCleanResult
} from "@/entities/workspace-setting";
import { HttpError } from "@/shared/api";
import { Button } from "@/shared/ui";

const MODE_LABEL: Record<WorkspaceCleanMode, string> = {
  all: "Удалить всё",
  closed_only: "Только закрытые"
};

/**
 * Управление массовой очисткой корня workspace. Два режима: «всё» сносит все
 * клоны и их binding-записи, «только закрытые» — лишь по интентам в
 * done/reject/fridge. Перед удалением показываем confirm с конкретикой —
 * сколько клонов и сколько места освободится (dry-run на бэке). После
 * выполнения инвалидируем настройки, чтобы карточка пересчитала размер.
 */
export function WorkspaceCleanupControl() {
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState<WorkspaceCleanMode | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<string | null>(null);

  async function run(mode: WorkspaceCleanMode) {
    setBusy(mode);
    setError(null);
    setResult(null);
    try {
      const preview = await cleanWorkspace({ mode, dry_run: true });
      if (preview.removed_clones === 0) {
        setResult("Нечего удалять — подходящих клонов нет.");
        return;
      }
      if (!window.confirm(buildConfirm(mode, preview))) {
        return;
      }
      const done = await cleanWorkspace({ mode, dry_run: false });
      setResult(
        `Удалено клонов: ${String(done.removed_clones)}, освобождено ` +
          `${formatWorkspaceSize(done.freed_bytes)}.`
      );
      await queryClient.invalidateQueries({
        queryKey: workspaceSettingsQueryKeys.all
      });
    } catch (err) {
      setError(
        err instanceof HttpError
          ? `Не удалось очистить (${String(err.status)}). Папка могла быть занята — закройте процессы и повторите.`
          : "Не удалось очистить workspace."
      );
    } finally {
      setBusy(null);
    }
  }

  return (
    <div className="flex flex-col gap-2" data-testid="workspace-cleanup">
      <div className="flex flex-wrap items-center gap-2">
        <Button
          icon={<Eraser aria-hidden size={14} strokeWidth={2} />}
          disabled={busy !== null}
          onClick={() => void run("closed_only")}
          data-testid="workspace-clean-closed"
        >
          {busy === "closed_only" ? "Очищаем…" : MODE_LABEL.closed_only}
        </Button>
        <Button
          className="btn-error"
          icon={<Trash2 aria-hidden size={14} strokeWidth={2} />}
          disabled={busy !== null}
          onClick={() => void run("all")}
          data-testid="workspace-clean-all"
        >
          {busy === "all" ? "Удаляем…" : MODE_LABEL.all}
        </Button>
      </div>
      <p className="m-0 text-xs leading-relaxed text-base-content/60">
        «Только закрытые» удаляет клоны интентов в статусах done / reject /
        fridge. «Удалить всё» сносит весь корень целиком, включая активные
        интенты. Операция необратима.
      </p>
      {result ? (
        <p
          className="m-0 text-xs text-base-content/70"
          data-testid="workspace-clean-result"
        >
          {result}
        </p>
      ) : null}
      {error ? (
        <p role="alert" className="m-0 text-xs text-error">
          {error}
        </p>
      ) : null}
    </div>
  );
}

function buildConfirm(
  mode: WorkspaceCleanMode,
  preview: WorkspaceCleanResult
): string {
  const scope =
    mode === "all"
      ? "Весь корень workspace будет очищен (клоны всех интентов, включая активные)."
      : "Будут удалены клоны интентов в статусах done / reject / fridge.";
  return (
    `${MODE_LABEL[mode]}?\n\n${scope}\n\n` +
    `Клонов к удалению: ${String(preview.removed_clones)}\n` +
    `Освободится: ${formatWorkspaceSize(preview.freed_bytes)}\n\n` +
    "Папки будут удалены с диска без возможности восстановления. " +
    "Несохранённые изменения и локальные ветки пропадут.\n\nПродолжить?"
  );
}
