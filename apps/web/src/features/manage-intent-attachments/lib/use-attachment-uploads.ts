import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";

import {
  intentsQueryKeys,
  useIntentAttachments,
  type IntentAttachment
} from "@/entities/intent";
import {
  INTENT_ATTACHMENTS_CHANGED_EVENT,
  httpDelete,
  httpGetBlob,
  httpPostForm,
  intentsEndpoints
} from "@/shared/api";
import { errorMessage } from "@/shared/lib";

export const MAX_ATTACHMENTS = 10;
const MAX_ATTACHMENT_BYTES = 10 * 1024 * 1024;
const EMPTY_ATTACHMENTS: readonly IntentAttachment[] = [];

interface AttachmentChangedDetail {
  intentId: string;
  error?: string;
}

export function useAttachmentUploads(intentId: string) {
  const queryClient = useQueryClient();
  const attachmentsQuery = useIntentAttachments(intentId);
  const attachments: readonly IntentAttachment[] =
    attachmentsQuery.data ?? EMPTY_ATTACHMENTS;
  const [busyDeleteId, setBusyDeleteId] = useState<string | null>(null);
  const [uploadingCount, setUploadingCount] = useState(0);
  const [actionError, setActionError] = useState<string | null>(null);
  const [previews, setPreviews] = useState<Partial<Record<string, string>>>({});

  const invalidateAttachments = () => {
    void queryClient.invalidateQueries({
      queryKey: intentsQueryKeys.attachments(intentId)
    });
  };

  useEffect(() => {
    const controller = new AbortController();
    const urls: string[] = [];
    setPreviews({});

    for (const attachment of attachments) {
      if (!attachment.content_type.startsWith("image/")) continue;
      httpGetBlob(
        intentsEndpoints.downloadIntentAttachment(intentId, attachment.id),
        controller.signal
      )
        .then((blob) => {
          const url = URL.createObjectURL(blob);
          urls.push(url);
          setPreviews((current) => ({ ...current, [attachment.id]: url }));
        })
        .catch(() => {
          if (controller.signal.aborted) return;
        });
    }

    return () => {
      controller.abort();
      for (const url of urls) URL.revokeObjectURL(url);
    };
  }, [attachments, intentId]);

  useEffect(() => {
    const listener = (event: Event) => {
      const detail = (event as CustomEvent<AttachmentChangedDetail>).detail;
      if (detail.intentId !== intentId) return;
      if (detail.error) setActionError(detail.error);
      void queryClient.invalidateQueries({
        queryKey: intentsQueryKeys.attachments(intentId)
      });
    };

    window.addEventListener(INTENT_ATTACHMENTS_CHANGED_EVENT, listener);
    return () => {
      window.removeEventListener(INTENT_ATTACHMENTS_CHANGED_EVENT, listener);
    };
  }, [intentId, queryClient]);

  const deleteAttachment = async (attachment: IntentAttachment) => {
    if (!window.confirm(`Удалить вложение «${attachment.file_name}»?`)) return;

    setBusyDeleteId(attachment.id);
    setActionError(null);
    try {
      await httpDelete(
        intentsEndpoints.deleteIntentAttachment(intentId, attachment.id)
      );
      invalidateAttachments();
    } catch (err: unknown) {
      setActionError(
        errorMessage(err, { base: "Не удалось удалить вложение" })
      );
    } finally {
      setBusyDeleteId(null);
    }
  };

  const uploadFiles = async (nextFiles: Iterable<File> | null) => {
    if (!nextFiles || !attachmentsQuery.isSuccess) return;

    const accepted: File[] = [];
    const problems: string[] = [];
    const remainingSlots = MAX_ATTACHMENTS - attachments.length;

    for (const file of Array.from(nextFiles)) {
      if (accepted.length >= remainingSlots) {
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

    if (accepted.length === 0) {
      setActionError(problems.length > 0 ? unique(problems).join(" ") : null);
      return;
    }

    setUploadingCount(accepted.length);
    setActionError(problems.length > 0 ? unique(problems).join(" ") : null);

    for (const file of accepted) {
      const form = new FormData();
      form.append("file", file, file.name);
      try {
        await httpPostForm<IntentAttachment>(
          intentsEndpoints.uploadIntentAttachment(intentId),
          form
        );
        invalidateAttachments();
      } catch (err: unknown) {
        setActionError(
          errorMessage(err, { base: `Не удалось загрузить ${file.name}` })
        );
      } finally {
        setUploadingCount((count) => Math.max(0, count - 1));
      }
    }
  };

  // Свежий uploadFiles для window-listener'ов, чтобы не пересоздавать подписку
  // на каждый рендер и не ловить устаревшее замыкание.
  const uploadFilesRef = useRef(uploadFiles);
  uploadFilesRef.current = uploadFiles;

  const canUpload =
    attachmentsQuery.isSuccess &&
    attachments.length < MAX_ATTACHMENTS &&
    uploadingCount === 0;

  const loadErrorMessage = attachmentsQuery.isError
    ? errorMessage(attachmentsQuery.error, {
        base: "Не удалось загрузить вложения"
      })
    : null;

  return {
    attachmentsQuery,
    attachments,
    previews,
    busyDeleteId,
    uploadingCount,
    actionError,
    canUpload,
    loadErrorMessage,
    deleteAttachment,
    uploadFiles,
    uploadFilesRef
  };
}

function unique(values: string[]): string[] {
  return [...new Set(values)];
}
