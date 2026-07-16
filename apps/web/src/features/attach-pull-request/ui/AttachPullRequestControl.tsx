import { useQueryClient } from "@tanstack/react-query";
import { GitPullRequest, X } from "lucide-react";
import { useState } from "react";

import {
  attachIntentRepositoryPullRequest,
  intentRepositoriesQueryKeys
} from "@/entities/repository-binding";
import { httpErrorStatus } from "@/shared/lib";
import { Button } from "@/shared/ui";

interface AttachPullRequestControlProps {
  intentId: string;
  bindingId: string;
}

type Parsed =
  { kind: "empty" } | { kind: "value"; value: number } | { kind: "invalid" };

function parsePrNumber(raw: string): Parsed {
  const trimmed = raw.trim();
  if (trimmed.length === 0) return { kind: "empty" };
  if (!/^\d+$/.test(trimmed)) return { kind: "invalid" };
  const value = Number(trimmed);
  if (!Number.isInteger(value) || value < 1) return { kind: "invalid" };
  return { kind: "value", value };
}

/**
 * Inline-привязка существующего PR к уже привязанному репозиторию, у которого
 * PR пока нет (ручной аналог фонового auto-bind). Кнопка «Привязать PR»
 * раскрывает компактное числовое поле; submit вызывает endpoint и инвалидирует
 * список bindings'ов для немедленной обратной связи (помимо realtime-события).
 */
export function AttachPullRequestControl({
  intentId,
  bindingId
}: AttachPullRequestControlProps) {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [value, setValue] = useState("");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const parsed = parsePrNumber(value);
  const canSubmit = parsed.kind === "value" && !pending;

  function reset() {
    setOpen(false);
    setValue("");
    setPending(false);
    setError(null);
  }

  function handleSubmit() {
    if (parsed.kind !== "value") return;
    setPending(true);
    setError(null);
    void (async () => {
      try {
        await attachIntentRepositoryPullRequest(
          intentId,
          bindingId,
          parsed.value
        );
        await queryClient.invalidateQueries({
          queryKey: intentRepositoriesQueryKeys.list(intentId)
        });
        reset();
      } catch (err) {
        setPending(false);
        if (httpErrorStatus(err) === 409) {
          setError("PR уже привязан к этому репозиторию.");
        } else {
          setError("Не удалось привязать PR.");
        }
      }
    })();
  }

  if (!open) {
    return (
      <Button
        aria-label="Привязать PR"
        data-testid={`attach-pr-open-${bindingId}`}
        icon={<GitPullRequest aria-hidden size={14} strokeWidth={2} />}
        onClick={() => {
          setOpen(true);
        }}
      >
        Привязать PR
      </Button>
    );
  }

  return (
    <div className="flex flex-col items-end gap-1">
      <div className="flex items-center gap-2">
        <input
          type="text"
          inputMode="numeric"
          aria-label="Номер PR"
          data-testid={`attach-pr-input-${bindingId}`}
          className="input input-sm w-24 font-mono"
          placeholder="№ PR"
          value={value}
          disabled={pending}
          autoFocus
          onChange={(event) => {
            setValue(event.target.value);
            setError(null);
          }}
          onKeyDown={(event) => {
            if (event.key === "Enter" && canSubmit) {
              event.preventDefault();
              handleSubmit();
            }
            if (event.key === "Escape") {
              reset();
            }
          }}
        />
        <Button
          variant="primary"
          data-testid={`attach-pr-submit-${bindingId}`}
          disabled={!canSubmit}
          onClick={handleSubmit}
        >
          {pending ? "Привязываем…" : "Привязать"}
        </Button>
        <Button
          aria-label="Отмена"
          data-testid={`attach-pr-cancel-${bindingId}`}
          icon={<X aria-hidden size={14} strokeWidth={2} />}
          disabled={pending}
          onClick={reset}
        />
      </div>
      {error !== null ? (
        <span
          role="alert"
          data-testid={`attach-pr-error-${bindingId}`}
          className="text-xs text-error"
        >
          {error}
        </span>
      ) : null}
    </div>
  );
}
