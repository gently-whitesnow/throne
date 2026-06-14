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

interface PreflightModalProps {
  open: boolean;
  intentId: string;
  launch: TerminalLaunchArgs;
  actionLabel: string;
  isSubmitting: boolean;
  onClose: () => void;
  onLaunch: (payload: TerminalRunPayload) => void;
}

const ROLE_LABEL: Record<string, string> = {
  mandatory: "обязательная",
  default_on: "включена",
  default_off: "доступна"
};

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
    <Modal
      onClose={onClose}
      labelledBy={titleId}
      boxClassName="flex max-h-[min(820px,calc(100vh-32px))] w-full max-w-3xl flex-col gap-4"
    >
      <header className="flex items-start justify-between gap-4">
        <div className="flex flex-col gap-1">
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
          className="m-0 rounded-md border border-error/30 bg-error/10 px-3 py-2 text-xs text-error"
        >
          {preview.error}
        </p>
      ) : null}

      <div className="flex flex-col gap-4 overflow-y-auto">
        <SystemZone preview={preview} busy={busy} />
        <TaskZone preview={preview} />
      </div>

      <footer className="flex items-center justify-end gap-2">
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

function SystemZone({
  preview,
  busy
}: {
  preview: ReturnType<typeof usePreflightPreview>;
  busy: boolean;
}) {
  return (
    <section className="flex flex-col gap-2" aria-label="Правила (system)">
      <h4 className="m-0 text-sm font-semibold text-base-content">
        Правила · system
      </h4>
      <ul className="m-0 flex flex-col gap-1 p-0">
        {preview.parts.map((part) => (
          <PartRow
            key={part.part_id}
            part={part}
            busy={busy}
            onToggle={preview.togglePart}
          />
        ))}
      </ul>
      <label className="flex flex-col gap-1 text-xs text-base-content/70">
        <span>Итоговый system-блок (правка только на эту сессию)</span>
        <textarea
          aria-label="Итоговый system-промпт"
          data-testid="agent-terminal-system-prompt"
          className="textarea textarea-bordered min-h-28 font-mono text-xs"
          value={preview.systemPrompt}
          onChange={(e) => {
            preview.setSystemPrompt(e.target.value);
          }}
        />
      </label>
    </section>
  );
}

function PartRow({
  part,
  busy,
  onToggle
}: {
  part: PromptPartPreview;
  busy: boolean;
  onToggle: (partId: string) => void;
}) {
  const mandatory = part.role === "mandatory";
  return (
    <li className="flex items-center gap-2 text-xs">
      <input
        type="checkbox"
        className="checkbox checkbox-xs"
        checked={part.selected}
        disabled={mandatory || busy}
        aria-label={`Часть ${part.key}`}
        onChange={() => {
          onToggle(part.part_id);
        }}
      />
      <span className="font-medium text-base-content">{part.key}</span>
      <span className="text-base-content/50">{part.scope}</span>
      <span className="badge badge-ghost badge-sm">
        {ROLE_LABEL[part.role] ?? part.role}
      </span>
    </li>
  );
}

function TaskZone({
  preview
}: {
  preview: ReturnType<typeof usePreflightPreview>;
}) {
  return (
    <section className="flex flex-col gap-2" aria-label="Задача (user)">
      <h4 className="m-0 text-sm font-semibold text-base-content">
        Задача · user
      </h4>
      <label className="flex flex-col gap-1 text-xs text-base-content/70">
        <span>Тело интента</span>
        <textarea
          aria-label="Тело интента"
          data-testid="agent-terminal-task-body"
          className="textarea textarea-bordered min-h-32 text-xs"
          value={preview.body}
          onChange={(e) => {
            preview.setBody(e.target.value);
          }}
        />
      </label>
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
      <label className="flex flex-col gap-1 text-xs text-base-content/70">
        <span>Доп. ввод оператора на этот запуск</span>
        <textarea
          aria-label="Дополнительный ввод оператора"
          data-testid="agent-terminal-free-input"
          className="textarea textarea-bordered min-h-20 text-xs"
          value={preview.freeInput}
          onChange={(e) => {
            preview.setFreeInput(e.target.value);
          }}
        />
      </label>
    </section>
  );
}
