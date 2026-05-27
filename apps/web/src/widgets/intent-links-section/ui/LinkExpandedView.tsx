import { useIntent } from "@/entities/intent";
import { HttpError } from "@/shared/api";

import { LinkAttachmentsReadOnly } from "./LinkAttachmentsReadOnly";

interface LinkExpandedViewProps {
  peerId: string;
}

export function LinkExpandedView({ peerId }: LinkExpandedViewProps) {
  const intentQuery = useIntent(peerId);

  if (intentQuery.isPending) {
    return (
      <div
        aria-busy="true"
        aria-label="Загрузка связанного intent"
        className="flex flex-col gap-2"
      >
        <div className="h-3 w-1/3 animate-pulse rounded bg-base-200" />
        <div className="h-3 w-full animate-pulse rounded bg-base-200" />
        <div className="h-3 w-5/6 animate-pulse rounded bg-base-200" />
        <div className="h-3 w-2/3 animate-pulse rounded bg-base-200" />
      </div>
    );
  }
  if (intentQuery.isError) {
    const err = intentQuery.error;
    const message =
      err instanceof HttpError
        ? `Ошибка загрузки (${String(err.status)}).`
        : "Не удалось загрузить связанный intent.";
    return (
      <p role="alert" className="m-0 text-[12px] text-error">
        {message}
      </p>
    );
  }

  const intent = intentQuery.data;
  return (
    <div className="flex flex-col gap-3">
      {intent.tags.length > 0 && (
        <ul
          className="m-0 flex list-none flex-wrap gap-1.5 p-0"
          aria-label="Теги связанного intent"
        >
          {intent.tags.map((t) => (
            <li
              key={t.id}
              className="rounded bg-primary/10 px-1.5 py-0.5 text-[11px] font-medium text-primary"
            >
              #{t.name}
            </li>
          ))}
        </ul>
      )}
      <pre className="m-0 whitespace-pre-wrap break-words font-mono text-[12.5px] leading-relaxed text-base-content">
        {intent.text}
      </pre>
      <LinkAttachmentsReadOnly intentId={peerId} />
    </div>
  );
}
