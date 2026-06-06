import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import {
  putRepositoryDocument,
  repositoriesQueryKeys,
  type RepositoryCoordinate,
  type RepositoryDocument
} from "@/entities/repository";
import { HttpError } from "@/shared/api";
import { Button, MarkdownView } from "@/shared/ui";

interface SchemaDocumentEditorProps {
  coordinate: RepositoryCoordinate;
  slug: string;
  /** Existing page when editing; `null` when authoring the first version. */
  current: RepositoryDocument | null;
  onSaved: (next: RepositoryDocument) => void;
  onCancel: () => void;
}

const DEFAULT_TITLE = "Карта схемы БД";
const STARTER_BODY =
  "# Карта схемы БД\n\n```mermaid\nerDiagram\n  EXAMPLE {\n    string id\n  }\n```\n";

export function SchemaDocumentEditor({
  coordinate,
  slug,
  current,
  onSaved,
  onCancel
}: SchemaDocumentEditorProps) {
  const queryClient = useQueryClient();
  const [title, setTitle] = useState(current?.title ?? DEFAULT_TITLE);
  const [body, setBody] = useState(current?.document ?? STARTER_BODY);
  const [preview, setPreview] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      const next = await putRepositoryDocument(coordinate, slug, {
        title: title.trim(),
        document: body,
        expected_version: current?.version ?? null
      });
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: repositoriesQueryKeys.document(coordinate, slug)
        }),
        queryClient.invalidateQueries({
          queryKey: repositoriesQueryKeys.documentVersions(coordinate, slug)
        })
      ]);
      onSaved(next);
    } catch (err: unknown) {
      setError(formatError(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        void submit();
      }}
      className="flex flex-col gap-3"
    >
      <input
        type="text"
        value={title}
        onChange={(e) => {
          setTitle(e.target.value);
        }}
        aria-label="Заголовок страницы"
        placeholder="Заголовок"
        className="input input-bordered w-full text-sm font-medium"
      />

      <div role="tablist" className="tabs tabs-boxed w-fit">
        <button
          type="button"
          role="tab"
          aria-selected={!preview}
          className={preview ? "tab" : "tab tab-active"}
          onClick={() => {
            setPreview(false);
          }}
        >
          Редактирование
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={preview}
          className={preview ? "tab tab-active" : "tab"}
          onClick={() => {
            setPreview(true);
          }}
        >
          Предпросмотр
        </button>
      </div>

      {preview ? (
        <div className="min-h-[16rem] rounded-md border border-base-300 bg-base-100 p-4">
          <MarkdownView markdown={body} />
        </div>
      ) : (
        <textarea
          value={body}
          onChange={(e) => {
            setBody(e.target.value);
          }}
          aria-label="Тело страницы (markdown)"
          spellCheck={false}
          className="textarea textarea-bordered min-h-[16rem] w-full resize-y font-mono text-[13px] leading-relaxed"
        />
      )}

      {error ? (
        <p role="alert" className="m-0 text-sm text-error">
          {error}
        </p>
      ) : null}

      <div className="flex gap-2">
        <Button type="submit" variant="primary" disabled={busy}>
          {busy ? "Сохраняем…" : "Сохранить"}
        </Button>
        <Button type="button" onClick={onCancel} disabled={busy}>
          Отмена
        </Button>
      </div>
    </form>
  );
}

function formatError(err: unknown): string {
  if (err instanceof HttpError) {
    if (err.status === 409) {
      return "Версия устарела — обновите страницу и повторите правку.";
    }
    if (err.status === 422) {
      return "Не удалось сохранить: проверьте заголовок и тело.";
    }
    return `Ошибка сохранения (${String(err.status)}).`;
  }
  return "Не удалось сохранить.";
}
