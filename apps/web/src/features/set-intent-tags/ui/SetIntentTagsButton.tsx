import { useState } from "react";

import type { IntentDetail } from "@/entities/intent";
import { intentsEndpoints } from "@/shared/api";
import { Button } from "@/shared/ui";

interface SetIntentTagsButtonProps {
  intent: IntentDetail;
}

export function SetIntentTagsButton({ intent }: SetIntentTagsButtonProps) {
  const [busy, setBusy] = useState(false);

  const handleClick = async () => {
    const initial = intent.tags.map((t) => t.name).join(", ");
    const proposed = window.prompt(
      "Теги через запятую (slug-style; пустая строка снимет все теги):",
      initial
    );
    if (proposed === null) return;
    const names = proposed
      .split(",")
      .map((s) => s.trim())
      .filter(Boolean);

    setBusy(true);
    try {
      const url = `/api/v1${intentsEndpoints.setIntentTags(intent.id)}`;
      const response = await fetch(url, {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json"
        },
        body: JSON.stringify({
          tag_names: names,
          expected_version: intent.current_version
        })
      });
      if (!response.ok) {
        const text = await response.text();
        throw new Error(
          `PUT ${url} failed (${String(response.status)}): ${text}`
        );
      }
    } catch (err: unknown) {
      window.alert(
        err instanceof Error ? err.message : "Не удалось сохранить."
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <Button
      variant="default"
      onClick={() => void handleClick()}
      disabled={busy}
    >
      Изменить теги
    </Button>
  );
}
