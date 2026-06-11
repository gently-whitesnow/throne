import { Image, X } from "lucide-react";

import { type IntentAttachment } from "@/entities/intent";
import { apiUrl, intentsEndpoints } from "@/shared/api";

interface AttachmentChipProps {
  intentId: string;
  attachment: IntentAttachment;
  preview: string | undefined;
  deleting: boolean;
  onDelete: () => void;
}

export function AttachmentChip({
  intentId,
  attachment,
  preview,
  deleting,
  onDelete
}: AttachmentChipProps) {
  const contentUrl =
    preview ??
    apiUrl(intentsEndpoints.downloadIntentAttachment(intentId, attachment.id));

  return (
    <li className="inline-flex max-w-[220px] items-center gap-2 rounded-full border border-base-300 bg-base-100 py-1 pl-1 pr-2 text-[12px]">
      <a
        className="inline-flex h-6 w-6 shrink-0 items-center justify-center overflow-hidden rounded-full bg-base-200 text-base-content/60 focus-visible:outline-2 focus-visible:outline-primary focus-visible:outline-offset-1"
        href={contentUrl}
        target="_blank"
        rel="noreferrer"
        aria-label={`Открыть ${attachment.file_name}`}
      >
        {preview ? (
          <img
            src={preview}
            alt={attachment.file_name}
            className="h-full w-full object-cover"
          />
        ) : (
          <Image aria-hidden size={13} strokeWidth={1.8} />
        )}
      </a>
      <span className="truncate font-medium" title={attachment.file_name}>
        {attachment.file_name}
      </span>
      <span className="shrink-0 tabular-nums text-base-content/50">
        {formatBytes(attachment.size_bytes)}
      </span>
      <button
        type="button"
        onClick={onDelete}
        disabled={deleting}
        aria-label={`Удалить ${attachment.file_name}`}
        className="inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-full text-base-content/50 transition-colors hover:bg-error hover:text-error-content focus-visible:outline-2 focus-visible:outline-primary disabled:cursor-not-allowed disabled:opacity-50"
      >
        <X aria-hidden size={13} strokeWidth={2.5} />
      </button>
    </li>
  );
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${String(bytes)} Б`;
  const mb = bytes / (1024 * 1024);
  if (mb >= 1) return `${mb.toFixed(mb >= 10 ? 0 : 1)} МБ`;
  return `${(bytes / 1024).toFixed(0)} КБ`;
}
