import {
  apiUrl,
  chatUploadsEndpoints,
  httpDelete,
  httpGet
} from "@/shared/api";

import type { ChatUpload } from "../model/types";

export function fetchChatUploads(signal?: AbortSignal): Promise<ChatUpload[]> {
  return httpGet<ChatUpload[]>(chatUploadsEndpoints.listChatUploads(), signal);
}

export function deleteChatUpload(
  id: string,
  signal?: AbortSignal
): Promise<void> {
  return httpDelete(chatUploadsEndpoints.deleteChatUpload(id), signal);
}

export function chatUploadDownloadHref(id: string): string {
  return apiUrl(chatUploadsEndpoints.downloadChatUpload(id));
}
