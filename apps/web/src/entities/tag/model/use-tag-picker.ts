import { useCallback, useEffect, useState } from "react";

import { HttpError } from "@/shared/api";

import { createTag, fetchTags } from "../api/tags-api";
import type { Tag } from "./types";

interface TagPickerState {
  availableTags: string[];
  loadError: string | null;
  createTag: (slug: string) => Promise<string>;
}

export function useTagPicker(): TagPickerState {
  const [tags, setTags] = useState<Tag[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    fetchTags(controller.signal)
      .then((next) => {
        setTags(next);
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        setLoadError(
          err instanceof HttpError
            ? `Не удалось загрузить теги (${String(err.status)}).`
            : "Не удалось загрузить теги."
        );
      });
    return () => {
      controller.abort();
    };
  }, []);

  const createOrAdopt = useCallback(async (slug: string): Promise<string> => {
    try {
      const created = await createTag({ name: slug });
      setTags((current) =>
        current.some((t) => t.id === created.id)
          ? current
          : [...current, created]
      );
      return created.name;
    } catch (err: unknown) {
      // 409 = already exists; treat as adoptable
      if (err instanceof HttpError && err.status === 409) {
        return slug;
      }
      throw err instanceof HttpError
        ? new Error(`Не удалось создать тег (${String(err.status)}).`)
        : new Error("Не удалось создать тег.");
    }
  }, []);

  return {
    availableTags: tags.map((t) => t.name),
    loadError,
    createTag: createOrAdopt
  };
}
