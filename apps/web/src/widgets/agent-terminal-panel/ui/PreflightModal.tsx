import { Play, X } from "lucide-react";
import { useId } from "react";

import { Button, Modal } from "@/shared/ui";
import { promptRegionAccent } from "@/shared/lib";

import { usePreflightPreview } from "../model/use-preflight-preview";
import { RUN_MODE_LABEL } from "../model/types";
import type { TerminalLaunchArgs, TerminalRunPayload } from "../model/types";

import { AutoTextarea } from "./AutoTextarea";
import { PreflightColumn } from "./PreflightColumn";
import { PreflightSummary } from "./PreflightSummary";
import { SkillToggleList } from "./SkillToggleList";

interface PreflightModalProps {
  open: boolean;
  intentId: string;
  launch: TerminalLaunchArgs;
  reviewBindingId: string | null;
  actionLabel: string;
  isSubmitting: boolean;
  onClose: () => void;
  onLaunch: (payload: TerminalRunPayload) => void;
}

export function PreflightModal({
  open,
  intentId,
  launch,
  reviewBindingId,
  actionLabel,
  isSubmitting,
  onClose,
  onLaunch
}: PreflightModalProps) {
  const preview = usePreflightPreview(intentId, launch.mode, open);
  const titleId = useId();
  const userAccent = promptRegionAccent("user");

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

      <div className="grid min-h-0 flex-1 grid-cols-1 lg:grid-cols-2">
        <PreflightColumn
          title="SYSTEM · system-промпт"
          parts={preview.parts}
          onToggle={preview.togglePart}
          onTextChange={preview.setPartText}
        />
        <section
          aria-label="USER · user-промпт запуска"
          className="flex min-h-0 flex-1 flex-col gap-3 overflow-y-auto border-l-[3px] px-3 py-3"
          style={{
            backgroundColor: userAccent.tint,
            borderLeftColor: userAccent.stripe
          }}
        >
          <h4 className="m-0 text-xs font-semibold uppercase tracking-wide text-base-content/55">
            USER · user-промпт запуска
          </h4>
          <TaskBodyFrame preview={preview} />
          <label className="flex flex-col gap-1 text-xs text-base-content/70">
            <span>Свободная вставка на эту сессию</span>
            <AutoTextarea
              aria-label="Дополнительный ввод оператора"
              data-testid="agent-terminal-free-input"
              className="textarea textarea-bordered min-h-20 resize-none overflow-hidden text-xs"
              value={preview.freeInput}
              onChange={(e) => {
                preview.setFreeInput(e.target.value);
              }}
            />
          </label>
          <SkillToggleList
            skills={preview.skills}
            onToggle={preview.toggleSkill}
          />
          <WorkspaceMapFrame workspaceMap={preview.workspaceMap} />
          <PreflightSummary
            systemPrompt={preview.systemPrompt}
            workspaceMap={preview.workspaceMap}
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
            onLaunch(preview.buildPayload(launch, reviewBindingId));
          }}
        >
          {isSubmitting ? "Запускаем…" : actionLabel}
        </Button>
      </footer>
    </Modal>
  );
}

function WorkspaceMapFrame({ workspaceMap }: { workspaceMap: string }) {
  if (!workspaceMap.includes("\nСвязи:\n")) return null;

  return (
    <section className="flex flex-col gap-1 rounded-md border border-base-300 bg-base-100 px-2 py-2">
      <span className="text-[11px] font-semibold uppercase tracking-wide text-base-content/55">
        workspace map
      </span>
      <pre
        data-testid="agent-terminal-workspace-map-context"
        className="m-0 max-h-40 overflow-auto whitespace-pre-wrap break-words rounded border border-base-300 bg-base-200/40 px-2 py-1.5 font-mono text-[11px] leading-snug text-base-content/80"
      >
        {workspaceMap}
      </pre>
    </section>
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
      <AutoTextarea
        aria-label="Тело интента"
        data-testid="agent-terminal-task-body"
        className="textarea textarea-bordered min-h-32 w-full resize-none overflow-hidden text-xs"
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
