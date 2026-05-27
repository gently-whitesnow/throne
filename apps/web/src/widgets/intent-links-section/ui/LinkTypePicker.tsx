import { useIntents } from "@/entities/intent";
import { HttpError } from "@/shared/api";
import { Button } from "@/shared/ui";

import { BUCKET_ORDER, bucketLabel, type DisplayBucket } from "../model/types";

interface LinkTypePickerProps {
  peerId: string;
  onPick: (bucket: DisplayBucket) => void;
  onCancel: () => void;
  /** Бакеты, в которых сейчас НЕЛЬЗЯ создать связь (дубликат и т.п.). */
  disabledBuckets?: ReadonlySet<DisplayBucket>;
}

/**
 * Когда пользователь дропает интент на секцию «Связи» целиком — мы не знаем,
 * какой тип связи он имел в виду. Этот поповер показывает кандидата (peer) и
 * предлагает выбрать тип. Аналогичен «add-link», но без поиска.
 */
export function LinkTypePicker({
  peerId,
  onPick,
  onCancel,
  disabledBuckets
}: LinkTypePickerProps) {
  const intentsQuery = useIntents();
  const peer = intentsQuery.data?.find((i) => i.id === peerId) ?? null;
  const error = intentsQuery.isError
    ? intentsQuery.error instanceof HttpError
      ? `Ошибка (${String(intentsQuery.error.status)}).`
      : "Не удалось загрузить intent."
    : null;

  const title = peer?.text_short.split(/\r?\n/, 1)[0] ?? peerId;

  return (
    <div className="flex flex-col gap-2">
      <h3 className="m-0 text-[13px] font-semibold text-base-content">
        Какой тип связи?
      </h3>
      <p className="m-0 line-clamp-2 text-[12px] text-base-content/60">
        {title}
      </p>
      {error && (
        <p role="alert" className="m-0 text-[11px] text-error">
          {error}
        </p>
      )}
      <ul className="m-0 flex list-none flex-col gap-1 p-0">
        {BUCKET_ORDER.map((bucket) => {
          const disabled = disabledBuckets?.has(bucket);
          return (
            <li key={bucket}>
              <button
                type="button"
                disabled={disabled}
                onClick={() => {
                  onPick(bucket);
                }}
                className="block w-full rounded border border-base-300 bg-base-100 px-2 py-1.5 text-left text-[12px] text-base-content transition-colors hover:border-primary/40 hover:bg-base-200 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {bucketLabel[bucket]}
              </button>
            </li>
          );
        })}
      </ul>
      <div className="flex justify-end">
        <Button onClick={onCancel}>Отмена</Button>
      </div>
    </div>
  );
}
