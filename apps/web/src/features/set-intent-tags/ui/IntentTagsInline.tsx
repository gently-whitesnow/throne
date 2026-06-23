import { useCallback, useEffect, useState } from "react";

import type { IntentDetail } from "@/entities/intent";
import { useTagPicker } from "@/entities/tag";
import { httpPut, intentsEndpoints } from "@/shared/api";
import { errorMessage } from "@/shared/lib";
import { TagMultiSelect } from "@/shared/ui";

interface IntentTagsInlineProps {
  intent: IntentDetail;
  onSaved: (next: IntentDetail) => void;
}

export function IntentTagsInline({ intent, onSaved }: IntentTagsInlineProps) {
  const initial = intent.tags.map((t) => t.name);
  const [draft, setDraft] = useState<string[]>(initial);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const tagPicker = useTagPicker();

  useEffect(() => {
    setDraft(intent.tags.map((t) => t.name));
  }, [intent.tags]);

  const save = useCallback(
    async (next: string[]) => {
      setBusy(true);
      setError(null);
      try {
        const updated = await httpPut<IntentDetail>(
          intentsEndpoints.setIntentTags(intent.id),
          {
            tag_names: next,
            expected_version: intent.current_version
          }
        );
        onSaved(updated);
      } catch (err: unknown) {
        setError(
          errorMessage(err, {
            base: "Не удалось сохранить",
            byStatus: {
              409: "Версия устарела — обновите страницу.",
              422: "Недопустимое имя тега."
            }
          })
        );
        setDraft(intent.tags.map((t) => t.name));
      } finally {
        setBusy(false);
      }
    },
    [intent.current_version, intent.id, intent.tags, onSaved]
  );

  const handleChange = (next: string[]) => {
    setDraft(next);
    void save(next);
  };

  return (
    <div className="flex flex-col gap-1.5">
      <div className="flex items-center gap-2 text-[11px] font-bold uppercase tracking-wider text-base-content/60">
        <span>Теги</span>
        {busy ? (
          <span className="text-[10px] font-medium normal-case text-base-content/50">
            сохраняем…
          </span>
        ) : null}
      </div>
      <TagMultiSelect
        value={draft}
        onChange={handleChange}
        candidates={tagPicker.candidates}
        query={tagPicker.query}
        onQueryChange={tagPicker.setQuery}
        onRequestCreate={tagPicker.createTag}
        loadError={tagPicker.loadError}
        disabled={busy}
        placeholder="Добавить тег…"
        ariaLabel="Теги intent"
      />
      {error ? (
        <p role="alert" className="m-0 text-[11px] text-error">
          {error}
        </p>
      ) : null}
    </div>
  );
}
