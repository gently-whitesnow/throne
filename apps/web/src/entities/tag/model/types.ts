import type { IntentsComponents, TagsComponents } from "@/shared/api";

export type Tag = TagsComponents["schemas"]["TagDto"];

export type TagListItem = TagsComponents["schemas"]["TagListItemDto"];

export type TagDetail = TagsComponents["schemas"]["TagDetailDto"];

export type TagDefaultRepository =
  TagsComponents["schemas"]["TagDefaultRepositoryDto"];

export type TagRef = IntentsComponents["schemas"]["TagRefDto"];
