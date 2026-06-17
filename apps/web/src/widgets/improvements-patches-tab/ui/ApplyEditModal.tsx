import { useState } from "react";

import { Button } from "@/shared/ui";

import { SideBySideDiff } from "./SideBySideDiff";

export interface ApplyEditModalProps {
  open: boolean;
  initialText: string;
  currentText: string;
  onCancel: () => void;
  onConfirm: (finalText: string) => Promise<void> | void;
  busy?: boolean;
}

export function ApplyEditModal({
  open,
  initialText,
  currentText,
  onCancel,
  onConfirm,
  busy
}: ApplyEditModalProps) {
  const [draft, setDraft] = useState(initialText);

  if (!open) return null;

  return (
    <dialog className="modal modal-open" aria-modal="true">
      <div className="modal-box max-w-3xl">
        <h3 className="m-0 text-lg font-bold">Применить с правкой</h3>
        <p className="mt-2 text-sm text-base-content/70">
          Отредактируй текст и нажми «Применить». Если оставишь без изменений -
          патч применится как `applied`, иначе как `applied_edited` с
          сохранением исходного `patch_text` для истории.
        </p>
        <div className="mt-3">
          <SideBySideDiff
            leftText={currentText}
            rightText={draft}
            leftLabel="Current"
            rightLabel="Edited"
            maxHeightClass="max-h-[25vh]"
          />
        </div>
        <textarea
          aria-label="Edited patch text"
          className="textarea textarea-bordered mt-3 max-h-[40vh] w-full font-mono text-xs"
          rows={14}
          value={draft}
          onChange={(e) => {
            setDraft(e.target.value);
          }}
        />
        <div className="modal-action">
          <Button onClick={onCancel} disabled={busy}>
            Отмена
          </Button>
          <Button
            variant="primary"
            onClick={() => void onConfirm(draft)}
            disabled={busy}
          >
            {busy ? "Применяем..." : "Применить"}
          </Button>
        </div>
      </div>
    </dialog>
  );
}
