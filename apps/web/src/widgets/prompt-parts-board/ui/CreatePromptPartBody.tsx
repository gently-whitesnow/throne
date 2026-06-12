import { useState } from "react";

import { useCreatePromptPart } from "@/entities/prompt-part";
import { Button } from "@/shared/ui";

import { formatCreateError } from "./PromptPartDialogErrors";

export function CreatePromptPartBody({ onClose }: { onClose: () => void }) {
  const create = useCreatePromptPart();
  const [key, setKey] = useState("");
  const [description, setDescription] = useState("");
  const [text, setText] = useState("");
  const [error, setError] = useState<string | null>(null);

  const submit = () => {
    setError(null);
    create.mutate(
      {
        key: key.trim(),
        text,
        description: description.trim() === "" ? null : description.trim()
      },
      {
        onSuccess: onClose,
        onError: (err) => {
          setError(formatCreateError(err));
        }
      }
    );
  };

  return (
    <form
      className="flex flex-col gap-3"
      onSubmit={(e) => {
        e.preventDefault();
        submit();
      }}
    >
      <label className="flex flex-col gap-1">
        <span className="text-[13px] font-semibold text-base-content">Key</span>
        <input
          className="input input-bordered font-mono text-[13px]"
          value={key}
          onChange={(e) => {
            setKey(e.target.value);
          }}
          aria-label="Key части"
          placeholder="my-optional-part"
        />
      </label>
      <label className="flex flex-col gap-1">
        <span className="text-[13px] font-semibold text-base-content">
          Описание{" "}
          <span className="font-normal text-base-content/60">
            (опционально)
          </span>
        </span>
        <textarea
          className="textarea textarea-bordered text-[13px]"
          value={description}
          onChange={(e) => {
            setDescription(e.target.value);
          }}
          rows={2}
          aria-label="Описание части"
        />
      </label>
      <label className="flex flex-col gap-1">
        <span className="text-[13px] font-semibold text-base-content">
          Текст
        </span>
        <textarea
          className="textarea textarea-bordered min-h-60 font-mono text-[13px] leading-relaxed"
          value={text}
          onChange={(e) => {
            setText(e.target.value);
          }}
          rows={14}
          aria-label="Текст части"
        />
      </label>
      {error ? (
        <p role="alert" className="m-0 text-sm text-error">
          {error}
        </p>
      ) : null}
      <div className="flex justify-end gap-2">
        <Button type="button" onClick={onClose} disabled={create.isPending}>
          Отмена
        </Button>
        <Button
          type="submit"
          variant="primary"
          disabled={create.isPending || key.trim() === ""}
        >
          {create.isPending ? "Создаём…" : "Создать"}
        </Button>
      </div>
    </form>
  );
}
