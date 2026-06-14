import { X } from "lucide-react";

import { formatBytes } from "../model/attachments";

interface AttachmentListProps {
  files: File[];
  busy: boolean;
  onRemove: (index: number) => void;
}

export function AttachmentList({ files, busy, onRemove }: AttachmentListProps) {
  if (files.length === 0) return null;
  return (
    <ul
      className="m-0 flex max-h-44 list-none flex-col gap-1.5 overflow-y-auto p-0"
      aria-label="Выбранные файлы"
    >
      {files.map((file, index) => (
        <li
          key={`${file.name}-${String(file.lastModified)}-${String(index)}`}
          className="grid grid-cols-[minmax(0,1fr)_auto_auto] items-center gap-2 rounded-md border border-base-300 bg-base-100 px-3 py-2 text-[13px]"
        >
          <span className="min-w-0 truncate">{file.name}</span>
          <span className="tabular-nums text-base-content/60">
            {formatBytes(file.size)}
          </span>
          <button
            type="button"
            className="btn btn-xs btn-ghost btn-circle"
            onClick={() => {
              onRemove(index);
            }}
            aria-label={`Убрать ${file.name}`}
            disabled={busy}
          >
            <X aria-hidden size={14} strokeWidth={2} />
          </button>
        </li>
      ))}
    </ul>
  );
}
