import type { IntentPreview } from "@/entities/intent";

export const intentBoardItems: IntentPreview[] = [
  {
    id: "intent-1",
    status: "active",
    title: "Собрать рабочий контекст",
    summary: "Единое место для задачи, версий текста и решений агента.",
    updatedAt: "сегодня",
    textVersion: 3
  },
  {
    id: "intent-2",
    status: "review",
    title: "Проверить изменения",
    summary: "Будущий поток review сохранит обратную связь рядом с intent.",
    updatedAt: "вчера",
    textVersion: 2
  },
  {
    id: "intent-3",
    status: "draft",
    title: "Описать инструкцию",
    summary: "Instruction будет жить как пользовательский источник правил.",
    updatedAt: "26 апреля",
    textVersion: 1
  }
];
