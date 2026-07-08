import { Plus } from "lucide-react";
import { useState } from "react";

import { Button } from "@/shared/ui";

import { AttachCardModal } from "./AttachCardModal";

interface AttachCardButtonProps {
  intentId: string;
}

/**
 * Тонкий триггер: монтирует `AttachCardModal` по клику и владеет его open/close.
 * Список освежается через query-invalidation внутри attach-мутации.
 */
export function AttachCardButton({ intentId }: AttachCardButtonProps) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <Button
        aria-label="Приложить карточку"
        icon={<Plus aria-hidden size={14} strokeWidth={2} />}
        variant="primary"
        onClick={() => {
          setOpen(true);
        }}
        data-testid="attach-card-open"
      >
        Приложить
      </Button>
      <AttachCardModal
        intentId={intentId}
        open={open}
        onClose={() => {
          setOpen(false);
        }}
      />
    </>
  );
}
