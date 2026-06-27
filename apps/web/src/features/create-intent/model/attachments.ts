import type { IntentAttachment } from "@/entities/intent";
import {
  INTENT_ATTACHMENTS_CHANGED_EVENT,
  httpPostForm,
  intentsEndpoints
} from "@/shared/api";
import { errorMessage } from "@/shared/lib";

export const MAX_ATTACHMENTS = 10;
export const MAX_ATTACHMENT_BYTES = 10 * 1024 * 1024;

/**
 * Фильтрует входящие файлы по лимитам (≤10 файлов, ≤10 МБ),
 * добавляя к уже выбранным. Проблемы дедуплицируются в одно сообщение.
 */
export function collectFiles(
  current: File[],
  incoming: Iterable<File>
): { files: File[]; error: string | null } {
  const accepted = [...current];
  const problems: string[] = [];
  for (const file of incoming) {
    if (accepted.length >= MAX_ATTACHMENTS) {
      problems.push(
        `Можно приложить максимум ${String(MAX_ATTACHMENTS)} файлов.`
      );
      break;
    }
    if (file.size > MAX_ATTACHMENT_BYTES) {
      problems.push(`${file.name}: файл больше 10 МБ.`);
      continue;
    }
    accepted.push(file);
  }
  return {
    files: accepted,
    error: problems.length > 0 ? [...new Set(problems)].join(" ") : null
  };
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${String(bytes)} Б`;
  const mb = bytes / (1024 * 1024);
  if (mb >= 1) return `${mb.toFixed(mb >= 10 ? 0 : 1)} МБ`;
  return `${(bytes / 1024).toFixed(0)} КБ`;
}

/**
 * Догружает вложения после создания интента и сигналит UI через
 * INTENT_ATTACHMENTS_CHANGED_EVENT (успех или ошибка по каждому файлу).
 */
export async function uploadAttachments(
  intentId: string,
  files: File[]
): Promise<void> {
  for (const file of files) {
    const form = new FormData();
    form.append("file", file, file.name);
    try {
      await httpPostForm<IntentAttachment>(
        intentsEndpoints.uploadIntentAttachment(intentId),
        form
      );
      window.dispatchEvent(
        new CustomEvent(INTENT_ATTACHMENTS_CHANGED_EVENT, {
          detail: { intentId }
        })
      );
    } catch (err) {
      const message = errorMessage(err, {
        base: `Не удалось загрузить ${file.name}`
      });
      window.dispatchEvent(
        new CustomEvent(INTENT_ATTACHMENTS_CHANGED_EVENT, {
          detail: { intentId, error: message }
        })
      );
    }
  }
}
