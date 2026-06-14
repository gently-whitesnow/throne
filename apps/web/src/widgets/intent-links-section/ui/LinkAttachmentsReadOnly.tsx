import { Image } from "lucide-react";
import { useEffect, useState } from "react";

import { useIntentAttachments, type IntentAttachment } from "@/entities/intent";
import { apiUrl, httpGetBlob, intentsEndpoints } from "@/shared/api";
import { errorMessage } from "@/shared/lib";

interface LinkAttachmentsReadOnlyProps {
  intentId: string;
}

const EMPTY_ATTACHMENTS: readonly IntentAttachment[] = [];

export function LinkAttachmentsReadOnly({
  intentId
}: LinkAttachmentsReadOnlyProps) {
  const attachmentsQuery = useIntentAttachments(intentId);
  const attachments: readonly IntentAttachment[] =
    attachmentsQuery.data ?? EMPTY_ATTACHMENTS;
  const [previews, setPreviews] = useState<Partial<Record<string, string>>>({});

  useEffect(() => {
    if (!attachmentsQuery.isSuccess) return;
    const controller = new AbortController();
    const urls: string[] = [];
    setPreviews({});
    for (const attachment of attachments) {
      if (!attachment.content_type.startsWith("image/")) continue;
      httpGetBlob(
        intentsEndpoints.downloadIntentAttachment(intentId, attachment.id),
        controller.signal
      )
        .then((blob) => {
          const url = URL.createObjectURL(blob);
          urls.push(url);
          setPreviews((current) => ({ ...current, [attachment.id]: url }));
        })
        .catch(() => {
          /* aborted */
        });
    }
    return () => {
      controller.abort();
      for (const url of urls) URL.revokeObjectURL(url);
    };
  }, [intentId, attachmentsQuery.isSuccess, attachments]);

  if (attachmentsQuery.isPending) {
    return (
      <p className="m-0 text-[12px] text-base-content/50">Загрузка вложений…</p>
    );
  }
  if (attachmentsQuery.isError) {
    const message = errorMessage(attachmentsQuery.error, {
      base: "Не удалось загрузить вложения"
    });
    return (
      <p role="alert" className="m-0 text-[12px] text-error">
        {message}
      </p>
    );
  }
  if (attachments.length === 0) return null;

  return (
    <ul
      className="m-0 grid list-none gap-2 p-0 [grid-template-columns:repeat(auto-fill,minmax(140px,1fr))]"
      aria-label="Вложения связанного intent"
    >
      {attachments.map((attachment) => {
        const preview = previews[attachment.id];
        const contentUrl =
          preview ??
          apiUrl(
            intentsEndpoints.downloadIntentAttachment(intentId, attachment.id)
          );
        return (
          <li
            key={attachment.id}
            className="flex flex-col gap-1 rounded-md border border-base-300 bg-base-100 p-1.5"
          >
            <a
              className="flex aspect-[4/3] items-center justify-center overflow-hidden rounded bg-base-200 text-base-content/60 focus-visible:outline-2 focus-visible:outline-primary focus-visible:outline-offset-2"
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
                <Image aria-hidden size={18} strokeWidth={1.8} />
              )}
            </a>
            <span className="truncate text-[11px] font-medium">
              {attachment.file_name}
            </span>
          </li>
        );
      })}
    </ul>
  );
}
