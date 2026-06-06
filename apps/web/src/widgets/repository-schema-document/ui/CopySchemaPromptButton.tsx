import { Check, Copy } from "lucide-react";
import { useState } from "react";

import { buildSchemaMapPrompt } from "../model/schema-prompt";

interface CopySchemaPromptButtonProps {
  fullName: string;
}

export function CopySchemaPromptButton({
  fullName
}: CopySchemaPromptButtonProps) {
  const [copied, setCopied] = useState(false);

  return (
    <button
      type="button"
      className="btn btn-xs btn-outline"
      onClick={() => {
        void navigator.clipboard
          .writeText(buildSchemaMapPrompt(fullName))
          .then(() => {
            setCopied(true);
            window.setTimeout(() => {
              setCopied(false);
            }, 1500);
          });
      }}
      title="Скопировать промпт для запуска schema_map"
      aria-label="Скопировать промпт для запуска schema_map"
    >
      {copied ? (
        <>
          <Check aria-hidden size={14} strokeWidth={2} /> Скопировано
        </>
      ) : (
        <>
          <Copy aria-hidden size={14} strokeWidth={2} /> Промпт схемы
        </>
      )}
    </button>
  );
}
