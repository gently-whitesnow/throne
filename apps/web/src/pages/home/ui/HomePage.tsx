import { useState } from "react";

import { Tabs, type TabItem } from "@/shared/ui";
import { IntentBoard } from "@/widgets/intent-board";
import { InstructionBoard } from "@/widgets/instruction-board";

type BoardKind = "intents" | "instructions";

const tabs: readonly TabItem<BoardKind>[] = [
  { value: "intents", label: "Intents" },
  { value: "instructions", label: "Instructions" }
];

export function HomePage() {
  const [active, setActive] = useState<BoardKind>("intents");

  return (
    <main className="page-shell home-page">
      <header className="home-page__header">
        <p className="home-page__eyebrow">Throne</p>
        <h1 className="home-page__title">
          Облако рабочих единиц для пользователя и агента
        </h1>
        <Tabs
          items={tabs}
          value={active}
          onChange={setActive}
          ariaLabel="Переключатель Intents / Instructions"
        />
      </header>
      {active === "intents" ? <IntentBoard /> : <InstructionBoard />}
    </main>
  );
}
