import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useMemo, useState } from "react";

import { HttpError } from "@/shared/api";
import { useDebouncedValue } from "@/shared/lib";

import { createTag } from "../api/tags-api";
import { tagsQueryKeys, useTagsTypeahead } from "../api/tags-queries";
import type { TagListItem } from "./types";

interface TagPickerState {
  query: string;
  setQuery: (next: string) => void;
  candidates: readonly TagListItem[];
  loadError: string | null;
  createTag: (slug: string) => Promise<string>;
}

const TYPEAHEAD_DEBOUNCE_MS = 200;
const TYPEAHEAD_LIMIT = 25;

export function useTagPicker(): TagPickerState {
  const [query, setQuery] = useState("");
  const debounced = useDebouncedValue(query.trim(), TYPEAHEAD_DEBOUNCE_MS);
  const tagsQuery = useTagsTypeahead(debounced, TYPEAHEAD_LIMIT);
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

  return {
    query,
    setQuery,
    candidates: tagsQuery.data,
    loadError,
    createTag: createOrAdopt
  };
}
