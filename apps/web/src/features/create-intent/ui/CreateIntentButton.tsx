import { Plus } from "lucide-react";

import { Button } from "@/shared/ui";

export function CreateIntentButton() {
  return (
    <Button
      aria-label="Создать intent"
      icon={<Plus aria-hidden size={18} strokeWidth={2.4} />}
      variant="primary"
    >
      Создать
    </Button>
  );
}
