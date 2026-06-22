import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useMemo } from "react";

import { HttpError } from "@/shared/api";

import { createTag } from "../api/tags-api";
import { tagsQueryKeys, useAllTags } from "../api/tags-queries";

interface TagPickerState {
  availableTags: string[];
  loadError: string | null;
  createTag: (slug: string) => Promise<string>;
}

export function useTagPicker(): TagPickerState {
  const tagsQuery = useAllTags();
  const queryClient = useQueryClient();

  const loadError = useMemo(() => {
    if (!tagsQuery.isError) return null;
    const err = tagsQuery.error;
    return err instanceof HttpError
      ? `Не удалось загрузить теги (${String(err.status)}).`
      : "Не удалось загрузить теги.";
  }, [tagsQuery.isError, tagsQuery.error]);

  const createOrAdopt = useCallback(
    async (slug: string): Promise<string> => {
      try {
        const created = await createTag({ name: slug });
        // Свежесозданный тег появится в списке после рефетча; realtime
        // tag.created тоже инвалидирует ключ, но не ждём сети — инвалидируем сразу.
        void queryClient.invalidateQueries({ queryKey: tagsQueryKeys.all });
        return created.name;
      } catch (err: unknown) {
        if (err instanceof HttpError && err.status === 409) {
          return slug;
        }
        throw err instanceof HttpError
          ? new Error(`Не удалось создать тег (${String(err.status)}).`)
          : new Error("Не удалось создать тег.");
      }
    },
    [queryClient]
  );

  const availableTags = useMemo(
    () => (tagsQuery.data ?? []).map((t) => t.name),
    [tagsQuery.data]
  );

  return {
    availableTags,
    loadError,
    createTag: createOrAdopt
  };
}
