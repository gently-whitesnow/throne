import { FileText, MessageSquarePlus } from "lucide-react";
import type { ReactNode } from "react";

/**
 * Slot 2 — the user-prompt. Explanatory only: its two pieces are edited where
 * they live (intent body on the intent page, free insert in the launch window),
 * so here we just name them so the assembly reads coherently.
 */
export function UserPromptSlot() {
  return (
    <div className="flex flex-col gap-2">
      <PieceCard
        icon={<FileText aria-hidden size={15} strokeWidth={2} />}
        title="Тело интента · задача"
        body="Основная постановка. Правится на странице интента или прямо в окне «Что уйдёт агенту» перед запуском."
      />
      <PieceCard
        icon={<MessageSquarePlus aria-hidden size={15} strokeWidth={2} />}
        title="Свободная вставка"
        body="Разовый ввод оператора на конкретную сессию. Задаётся в окне запуска и не сохраняется в интент."
      />
      <p className="m-0 text-[11px] leading-relaxed text-base-content/45">
        Карта workspace дописывается доставкой поверх user-промпта на спавне — к
        запуску все репозитории уже склонированы.
      </p>
    </div>
  );
}

function PieceCard({
  icon,
  title,
  body
}: {
  icon: ReactNode;
  title: string;
  body: string;
}) {
  return (
    <div className="flex items-start gap-3 rounded-md border border-base-300 bg-base-200/30 px-3 py-2.5">
      <span
        aria-hidden
        className="mt-0.5 inline-flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-md bg-base-200 text-base-content/60"
      >
        {icon}
      </span>
      <div className="flex min-w-0 flex-col gap-0.5">
        <span className="text-[13px] font-semibold text-base-content">
          {title}
        </span>
        <span className="text-xs leading-relaxed text-base-content/65">
          {body}
        </span>
      </div>
    </div>
  );
}
