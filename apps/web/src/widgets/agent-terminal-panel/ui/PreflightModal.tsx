import { Play, X } from "lucide-react";
import { useId } from "react";

import { Button, Modal } from "@/shared/ui";

import { usePreflightPreview } from "../model/use-preflight-preview";
import { RUN_MODE_LABEL } from "../model/types";
import type {
  PromptPartPreview,
  TerminalLaunchArgs,
  TerminalRunPayload
} from "../model/types";

import { PreflightColumn } from "./PreflightColumn";
import { PreflightSummary } from "./PreflightSummary";

interface PreflightModalProps {
  open: boolean;
  intentId: string;
  launch: TerminalLaunchArgs;
  actionLabel: string;
  isSubmitting: boolean;
  onClose: () => void;
  onLaunch: (payload: TerminalRunPayload) => void;
}

function byScope(parts: PromptPartPreview[], scope: string): PromptPartPreview[] {
  return parts.filter((p) => p.scope === scope);
}

export function PreflightModal({
  open,
  intentId,
  launch,
  actionLabel,
  isSubmitting,
  onClose,
  onLaunch
}: PreflightModalProps) {
  const preview = usePreflightPreview(intentId, launch.mode, open);
  const titleId = useId();

  if (!open) return null;

  const busy = preview.status === "loading";
  const launchDisabled = busy || isSubmitting || preview.status === "error";

  return (
    <Modal variant="fullscreen" labelledBy={titleId} onClose={onClose}>
      <header className="flex items-start justify-between gap-4 border-b border-base-300 px-4 py-3">
        <div className="flex flex-col gap-0.5">
          <p className="m-0 text-xs font-bold uppercase tracking-wider text-primary">
            Перед запуском · {RUN_MODE_LABEL[launch.mode]}
          </p>
          <h3 id={titleId} className="m-0 text-lg font-semibold leading-tight">
            Что уйдёт агенту
          </h3>
        </div>
        <button
          type="button"
          className="btn btn-sm btn-circle btn-ghost"
          onClick={onClose}
          aria-label="Закрыть"
        >
          <X aria-hidden size={16} strokeWidth={2} />
        </button>
      </header>

      {preview.status === "error" ? (
        <p
          role="alert"
          className="m-0 border-b border-error/30 bg-error/10 px-4 py-2 text-xs text-error"
        >
          {preview.error}
        </p>
      ) : null}

      <div className="grid min-h-0 flex-1 grid-cols-1 divide-base-300 lg:grid-cols-3 lg:divide-x">
        <PreflightColumn
          title="SYSTEM"
          parts={byScope(preview.parts, "system")}
          onToggle={preview.togglePart}
          onTextChange={preview.setPartText}
        />
        <PreflightColumn
          title="USER"
          parts={byScope(preview.parts, "user")}
          onToggle={preview.togglePart}
          onTextChange={preview.setPartText}
        >
          <TaskBodyFrame preview={preview} />
        </PreflightColumn>
        <section
          aria-label="FREE и итог"
          className="flex min-h-0 flex-1 flex-col gap-3 overflow-y-auto px-3 py-3"
        >
          <h4 className="m-0 text-xs font-semibold uppercase tracking-wide text-base-content/55">
            FREE
          </h4>
          <label className="flex flex-col gap-1 text-xs text-base-content/70">
            <span>Свободная вставка на эту сессию</span>
            <textarea
              aria-label="Дополнительный ввод оператора"
              data-testid="agent-terminal-free-input"
              className="textarea textarea-bordered min-h-28 text-xs"
              value={preview.freeInput}
              onChange={(e) => {
                preview.setFreeInput(e.target.value);
              }}
            />
          </label>
          <PreflightSummary
            systemPrompt={preview.systemPrompt}
            userPrompt={preview.body}
            freeInput={preview.freeInput}
          />
        </section>
      </div>

      <footer className="flex items-center justify-end gap-2 border-t border-base-300 px-4 py-3">
        <Button onClick={onClose}>Отмена</Button>
        <Button
          data-testid="agent-terminal-preflight-launch"
          variant="primary"
          icon={<Play aria-hidden size={14} strokeWidth={2} />}
          disabled={launchDisabled}
          onClick={() => {
            onLaunch(preview.buildPayload(launch));
          }}
        >
          {isSubmitting ? "Запускаем…" : actionLabel}
        </Button>
      </footer>
    </Modal>
  );
}

function TaskBodyFrame({
  preview
}: {
  preview: ReturnType<typeof usePreflightPreview>;
}) {
  return (
    <section className="flex flex-col gap-2 rounded-md border border-primary/30 bg-base-100 px-2 py-2">
      <span className="text-[11px] font-semibold uppercase tracking-wide text-base-content/55">
        Тело интента · задача
      </span>
      <textarea
        aria-label="Тело интента"
        data-testid="agent-terminal-task-body"
        className="textarea textarea-bordered min-h-32 w-full resize-y text-xs"
        value={preview.body}
        onChange={(e) => {
          preview.setBody(e.target.value);
        }}
      />
      <label className="flex items-center gap-2 text-xs text-base-content/70">
        <input
          type="checkbox"
          className="checkbox checkbox-xs"
          data-testid="agent-terminal-save-intent"
          checked={preview.saveIntentText}
          onChange={(e) => {
            preview.setSaveIntentText(e.target.checked);
          }}
        />
        <span>
          Обновить Intent.text{" "}
          {preview.bodyDirty ? "" : "(тело не менялось — сохранять нечего)"}
        </span>
      </label>
    </section>
  );
}
