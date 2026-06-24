import type { TagsComponents } from "@/shared/api";
import {
  httpDelete,
  httpGet,
  httpPost,
  httpPut,
  tagsEndpoints
} from "@/shared/api";

import type { Tag, TagDefaultRepository, TagDetail } from "../model/types";

type CreateTagBody = TagsComponents["schemas"]["CreateTagRequest"];
type RenameTagBody = TagsComponents["schemas"]["RenameTagRequest"];
type TagListPage = TagsComponents["schemas"]["TagListPageDto"];

export interface TagListParams {
  search?: string;
  limit?: number;
}

function buildTagsListUrl(
  params: TagListParams,
  cursor: string | undefined
): string {
  const qs = new URLSearchParams();
  if (cursor) qs.set("cursor", cursor);
  if (params.limit !== undefined) qs.set("limit", String(params.limit));
  if (params.search) qs.set("search", params.search);
  const base = tagsEndpoints.listTags();
  const suffix = qs.toString();
  return suffix.length > 0 ? `${base}?${suffix}` : base;
}

export function fetchTagsPage(
  params: TagListParams,
  cursor: string | undefined,
  signal?: AbortSignal
): Promise<TagListPage> {
  return httpGet<TagListPage>(buildTagsListUrl(params, cursor), signal);
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
