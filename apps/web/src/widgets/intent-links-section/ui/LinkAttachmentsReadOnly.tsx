import { Image } from "lucide-react";
import { useEffect, useState } from "react";

import type { IntentAttachment } from "@/entities/intent";
import {
  HttpError,
  apiUrl,
  httpGet,
  httpGetBlob,
  intentsEndpoints
} from "@/shared/api";

interface LinkAttachmentsReadOnlyProps {
  intentId: string;
}

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; attachments: IntentAttachment[] }
  | { kind: "error"; message: string };

export function LinkAttachmentsReadOnly({
  intentId
}: LinkAttachmentsReadOnlyProps) {
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [previews, setPreviews] = useState<Partial<Record<string, string>>>({});

  useEffect(() => {
    const controller = new AbortController();
    setState({ kind: "loading" });
    httpGet<IntentAttachment[]>(
      intentsEndpoints.listIntentAttachments(intentId),
      controller.signal
    )
      .then((attachments) => {
        setState({ kind: "ready", attachments });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        setState({
          kind: "error",
          message:
            err instanceof HttpError
              ? `Не удалось загрузить вложения (${String(err.status)}).`
              : "Не удалось загрузить вложения."
        });
      });
    return () => {
      controller.abort();
    };
  }, [intentId]);

  useEffect(() => {
    if (state.kind !== "ready") return;
    const controller = new AbortController();
    const urls: string[] = [];
    setPreviews({});
    for (const attachment of state.attachments) {
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
  }, [intentId, state]);

  if (state.kind === "loading") {
    return (
      <p className="m-0 text-[12px] text-base-content/50">Загрузка вложений…</p>
    );
  }
  if (state.kind === "error") {
    return (
      <p role="alert" className="m-0 text-[12px] text-error">
        {state.message}
      </p>
    );
  }
  if (state.attachments.length === 0) return null;

  return (
    <ul
      className="m-0 grid list-none gap-2 p-0 [grid-template-columns:repeat(auto-fill,minmax(140px,1fr))]"
      aria-label="Вложения связанного intent"
    >
      {state.attachments.map((attachment) => {
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
