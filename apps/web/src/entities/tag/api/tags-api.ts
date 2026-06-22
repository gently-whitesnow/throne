import type { TagsComponents } from "@/shared/api";
import {
  httpDelete,
  httpGet,
  httpPost,
  httpPut,
  tagsEndpoints
} from "@/shared/api";

import type {
  Tag,
  TagDefaultRepository,
  TagDetail,
  TagUsage
} from "../model/types";

type CreateTagBody = TagsComponents["schemas"]["CreateTagRequest"];
type RenameTagBody = TagsComponents["schemas"]["RenameTagRequest"];

export function fetchTags(signal?: AbortSignal): Promise<Tag[]> {
  return httpGet<Tag[]>(tagsEndpoints.listTags(), signal);
}

export function createTag(
  body: CreateTagBody,
  signal?: AbortSignal
): Promise<Tag> {
  return httpPost<Tag>(tagsEndpoints.createTag(), body, signal);
}

export async function renameTag(
  id: string,
  body: RenameTagBody,
  signal?: AbortSignal
): Promise<Tag> {
  const url = tagsEndpoints.renameTag(id);
  const response = await fetch(`/api/v1${url}`, {
    method: "PATCH",
    headers: { Accept: "application/json", "Content-Type": "application/json" },
    body: JSON.stringify(body),
    signal
  });
  if (!response.ok) {
    const text = await response.text();
    throw new Error(
      `PATCH ${url} failed (${String(response.status)}): ${text}`
    );
  }
  return (await response.json()) as Tag;
}

export function fetchTagUsage(
  id: string,
  signal?: AbortSignal
): Promise<TagUsage> {
  return httpGet<TagUsage>(tagsEndpoints.getTagUsage(id), signal);
}

export async function deleteTag(
  id: string,
  detach: boolean,
  signal?: AbortSignal
): Promise<void> {
  const path = `${tagsEndpoints.deleteTag(id)}?detach=${detach ? "true" : "false"}`;
  await httpDelete(path, signal);
}

export function fetchTag(id: string, signal?: AbortSignal): Promise<TagDetail> {
  return httpGet<TagDetail>(tagsEndpoints.getTag(id), signal);
}

export function setTagDefaultRepositories(
  id: string,
  body: {
    expected_version: number;
    default_repositories: TagDefaultRepository[];
  },
  signal?: AbortSignal
): Promise<TagDetail> {
  return httpPut<TagDetail>(
    tagsEndpoints.setTagDefaultRepositories(id),
    body,
    signal
  );
}
