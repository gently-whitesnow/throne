import {
  useMutation,
  useQuery,
  useQueryClient,
  type UseMutationResult,
  type UseQueryResult
} from "@tanstack/react-query";

import { createPromptPart } from "./create-prompt-part";
import { deletePromptPart } from "./delete-prompt-part";
import { getPromptPart } from "./get-prompt-part";
import { listPromptParts } from "./list-prompt-parts";
import { replacePromptPartText } from "./replace-prompt-part-text";
import { setPromptPartRoles } from "./set-prompt-part-roles";
import type {
  CreatePromptPartRequest,
  DeletePromptPartResponse,
  PromptPart,
  PromptPartListItem,
  PromptPartModeRole
} from "../model/types";

export const promptPartsQueryKeys = {
  all: ["prompt-parts"] as const,
  lists: () => [...promptPartsQueryKeys.all, "list"] as const,
  details: () => [...promptPartsQueryKeys.all, "detail"] as const,
  detail: (id: string) => [...promptPartsQueryKeys.details(), id] as const
};

export function useListPromptParts(): UseQueryResult<PromptPartListItem[]> {
  return useQuery({
    queryKey: promptPartsQueryKeys.lists(),
    queryFn: ({ signal }) => listPromptParts(signal)
  });
}

export function usePromptPart(
  id: string | null,
  enabled = true
): UseQueryResult<PromptPart> {
  return useQuery({
    queryKey: promptPartsQueryKeys.detail(id ?? ""),
    queryFn: ({ signal }) => getPromptPart(id ?? "", signal),
    enabled: enabled && id !== null
  });
}

export function useCreatePromptPart(): UseMutationResult<
  PromptPart,
  unknown,
  CreatePromptPartRequest
> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreatePromptPartRequest) => createPromptPart(body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: promptPartsQueryKeys.lists() });
    }
  });
}

export interface ReplacePromptPartTextArgs {
  id: string;
  expectedVersion: number;
  oldText: string;
  newText: string;
}

export function useReplacePromptPartText(): UseMutationResult<
  PromptPart,
  unknown,
  ReplacePromptPartTextArgs
> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (args: ReplacePromptPartTextArgs) =>
      replacePromptPartText(args.id, {
        expected_version: args.expectedVersion,
        old_text: args.oldText,
        new_text: args.newText
      }),
    onSuccess: (part) => {
      void qc.invalidateQueries({ queryKey: promptPartsQueryKeys.lists() });
      void qc.invalidateQueries({
        queryKey: promptPartsQueryKeys.detail(part.id)
      });
    }
  });
}

export interface SetPromptPartRolesArgs {
  id: string;
  modeRoles: PromptPartModeRole[];
}

export function useSetPromptPartRoles(): UseMutationResult<
  PromptPart,
  unknown,
  SetPromptPartRolesArgs
> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (args: SetPromptPartRolesArgs) =>
      setPromptPartRoles(args.id, args.modeRoles),
    onSuccess: (part) => {
      void qc.invalidateQueries({ queryKey: promptPartsQueryKeys.lists() });
      void qc.invalidateQueries({
        queryKey: promptPartsQueryKeys.detail(part.id)
      });
    }
  });
}

export function useDeletePromptPart(): UseMutationResult<
  DeletePromptPartResponse,
  unknown,
  string
> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => deletePromptPart(id),
    onSuccess: (res) => {
      void qc.invalidateQueries({ queryKey: promptPartsQueryKeys.lists() });
      void qc.invalidateQueries({
        queryKey: promptPartsQueryKeys.detail(res.prompt_part_id)
      });
    }
  });
}
