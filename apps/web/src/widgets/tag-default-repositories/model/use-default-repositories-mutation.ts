import { useMutation, useQueryClient } from "@tanstack/react-query";

import {
  setTagDefaultRepositories,
  tagsQueryKeys,
  type TagDefaultRepository,
  type TagDetail
} from "@/entities/tag";

interface MutationArgs {
  tagId: string;
  expectedVersion: number;
  defaultRepositories: TagDefaultRepository[];
}

/**
 * Whole-list replace mutator с оптимистичным обновлением `tagsQueryKeys.detail`.
 * Возвращает обновлённый `TagDetail`, чтобы вызывающий компонент мог обновить
 * локальное состояние `expected_version` без повторного fetch.
 */
export function useDefaultRepositoriesMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      tagId,
      expectedVersion,
      defaultRepositories
    }: MutationArgs) =>
      setTagDefaultRepositories(tagId, {
        expected_version: expectedVersion,
        default_repositories: defaultRepositories
      }),
    onSuccess: (updated, variables) => {
      queryClient.setQueryData<TagDetail | undefined>(
        tagsQueryKeys.detail(variables.tagId),
        updated
      );
    }
  });
}
