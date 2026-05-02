import { Trash2, Pencil, Plus } from "lucide-react";
import { useCallback, useEffect, useState } from "react";

import {
  type Tag,
  createTag,
  deleteTag,
  fetchTagUsage,
  fetchTags,
  renameTag
} from "@/entities/tag";
import { useRealtimeEvent } from "@/shared/realtime";
import { Button } from "@/shared/ui";

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; items: Tag[] }
  | { kind: "error"; message: string };

export function TagsBoard() {
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [reloadKey, setReloadKey] = useState(0);
  const [newName, setNewName] = useState("");
  const [createError, setCreateError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    fetchTags(controller.signal)
      .then((items) => {
        setState({ kind: "ready", items });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        setState({
          kind: "error",
          message:
            err instanceof Error ? err.message : "Не удалось загрузить теги."
        });
      });
    return () => {
      controller.abort();
    };
  }, [reloadKey]);

  const reload = useCallback(() => {
    setReloadKey((v) => v + 1);
  }, []);

  useRealtimeEvent("tag.created", reload);
  useRealtimeEvent("tag.updated", reload);
  useRealtimeEvent("tag.deleted", reload);

  const handleCreate = (e: React.SyntheticEvent<HTMLFormElement>) => {
    e.preventDefault();
    const trimmed = newName.trim();
    if (!trimmed) return;
    setCreateError(null);
    void (async () => {
      try {
        await createTag({ name: trimmed });
        setNewName("");
      } catch (err: unknown) {
        setCreateError(
          err instanceof Error ? err.message : "Не удалось создать тег."
        );
      }
    })();
  };

  const handleRename = async (tag: Tag) => {
    const proposed = window.prompt(`Новое имя для #${tag.name}`, tag.name);
    if (proposed === null) return;
    const trimmed = proposed.trim();
    if (!trimmed || trimmed === tag.name) return;
    setBusyId(tag.id);
    try {
      await renameTag(tag.id, {
        name: trimmed,
        expected_version: tag.current_version
      });
    } catch (err: unknown) {
      window.alert(
        err instanceof Error ? err.message : "Не удалось переименовать."
      );
    } finally {
      setBusyId(null);
    }
  };

  const handleDelete = async (tag: Tag) => {
    setBusyId(tag.id);
    try {
      const usage = await fetchTagUsage(tag.id);
      const detach = usage.intents_count > 0;
      const message = detach
        ? `Тег #${tag.name} назначен ${String(usage.intents_count)} intent'у/ам. Открепить и удалить?`
        : `Удалить тег #${tag.name}?`;
      if (!window.confirm(message)) return;
      await deleteTag(tag.id, detach);
    } catch (err: unknown) {
      window.alert(err instanceof Error ? err.message : "Не удалось удалить.");
    } finally {
      setBusyId(null);
    }
  };

  return (
    <section className="master-pane" aria-label="Список тегов">
      <div className="master-pane__header">
        <h2 className="master-pane__title">Tags</h2>
      </div>
      <form className="master-pane__search" onSubmit={handleCreate}>
        <Plus aria-hidden size={14} strokeWidth={2} />
        <input
          type="text"
          placeholder="Новый тег (slug)"
          value={newName}
          onChange={(e) => {
            setNewName(e.target.value);
          }}
          aria-label="Имя нового тега"
        />
        <Button type="submit" variant="primary" disabled={!newName.trim()}>
          Создать
        </Button>
      </form>
      {createError && (
        <p role="alert" className="master-pane__hint">
          {createError}
        </p>
      )}
      <div className="master-pane__body">
        {state.kind === "loading" && (
          <p className="master-pane__hint">Загрузка…</p>
        )}
        {state.kind === "error" && (
          <p role="alert" className="master-pane__hint">
            {state.message}
          </p>
        )}
        {state.kind === "ready" && state.items.length === 0 && (
          <p className="master-pane__hint">Нет тегов.</p>
        )}
        {state.kind === "ready" && state.items.length > 0 && (
          <ul className="tag-list">
            {state.items.map((tag) => (
              <li key={tag.id} className="tag-list__row">
                <span className="tag-chip">#{tag.name}</span>
                <span className="tag-list__meta">
                  v{String(tag.current_version)}
                </span>
                <div className="tag-list__actions">
                  <Button
                    variant="default"
                    onClick={() => void handleRename(tag)}
                    disabled={busyId === tag.id}
                    aria-label="Переименовать"
                  >
                    <Pencil size={14} aria-hidden /> Rename
                  </Button>
                  <Button
                    variant="default"
                    onClick={() => void handleDelete(tag)}
                    disabled={busyId === tag.id}
                    aria-label="Удалить"
                  >
                    <Trash2 size={14} aria-hidden /> Delete
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}
