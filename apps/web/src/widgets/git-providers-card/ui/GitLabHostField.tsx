import { useEffect, useState } from "react";

import { useSetGitLabHost } from "@/entities/git-provider-status";
import { Button } from "@/shared/ui";

interface GitLabHostFieldProps {
  initial: string;
}

export function GitLabHostField({ initial }: GitLabHostFieldProps) {
  const mutation = useSetGitLabHost();
  const [value, setValue] = useState(initial);
  const [validationError, setValidationError] = useState<string | null>(null);

  useEffect(() => {
    setValue(initial);
  }, [initial]);

  const handleSave = () => {
    const trimmed = value.trim();
    const validation = validateHost(trimmed);
    if (validation !== null) {
      setValidationError(validation);
      return;
    }
    setValidationError(null);
    setValue(trimmed);
    if (trimmed === initial) return;
    mutation.mutate(trimmed);
  };

  const dirty = value.trim() !== initial;

  return (
    <div className="flex flex-col gap-1">
      <label
        htmlFor="gitlab-host-input"
        className="text-xs font-semibold text-base-content/70"
      >
        Хост GitLab
      </label>
      <div className="flex flex-wrap items-center gap-2">
        <input
          id="gitlab-host-input"
          data-testid="gitlab-host-input"
          type="text"
          className="input input-sm input-bordered flex-1 font-mono text-xs"
          value={value}
          placeholder="gitlab.com"
          onChange={(event) => {
            setValue(event.target.value);
            if (validationError !== null) setValidationError(null);
          }}
          onBlur={handleSave}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.preventDefault();
              handleSave();
            }
          }}
          disabled={mutation.isPending}
        />
        <Button
          aria-label="Сохранить GitLab host"
          data-testid="gitlab-host-save"
          disabled={mutation.isPending || !dirty}
          onClick={handleSave}
        >
          {mutation.isPending ? "Сохраняем…" : "Сохранить"}
        </Button>
      </div>
      {validationError !== null ? (
        <p
          role="alert"
          data-testid="gitlab-host-error"
          className="m-0 text-xs text-error"
        >
          {validationError}
        </p>
      ) : mutation.error instanceof Error ? (
        <p
          role="alert"
          data-testid="gitlab-host-mutation-error"
          className="m-0 text-xs text-error"
        >
          Не удалось сохранить host: {mutation.error.message}
        </p>
      ) : null}
    </div>
  );
}

function validateHost(value: string): string | null {
  if (value.length === 0) return "Введите имя хоста, например gitlab.com.";
  if (/^https?:\/\//i.test(value))
    return "Укажите host без схемы (без https://).";
  if (/\s/.test(value)) return "Host не должен содержать пробелы.";
  if (value.includes("/")) return "Host не должен содержать путь.";
  return null;
}
