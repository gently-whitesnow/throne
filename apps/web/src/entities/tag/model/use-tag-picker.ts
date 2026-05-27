import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useMemo } from "react";

import { HttpError } from "@/shared/api";

import { createTag } from "../api/tags-api";
import { tagsQueryKeys, useTags } from "../api/tags-queries";
import type { Tag } from "./types";

interface TagPickerState {
  availableTags: string[];
  loadError: string | null;
  createTag: (slug: string) => Promise<string>;
}

export function useTagPicker(): TagPickerState {
  const tagsQuery = useTags();
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
        // Оптимистичный апдейт кеша: realtime tag.created долетит позже и
        // повторно инвалидирует, но UI не ждёт сети.
        queryClient.setQueryData<Tag[]>(tagsQueryKeys.list(), (prev) => {
          if (!prev) return prev;
          if (prev.some((t) => t.id === created.id)) return prev;
          return [...prev, created];
        });
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
