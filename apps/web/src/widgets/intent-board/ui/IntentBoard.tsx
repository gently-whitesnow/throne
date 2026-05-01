import { IntentCard } from "@/entities/intent";
import { CreateIntentButton } from "@/features/create-intent";

import { intentBoardItems } from "../model/fixtures";

export function IntentBoard() {
  return (
    <section className="intent-board" aria-labelledby="intent-board-title">
      <div className="intent-board__toolbar">
        <h2 className="intent-board__title" id="intent-board-title">
          Intent cloud
        </h2>
        <CreateIntentButton />
      </div>
      <div className="intent-board__grid">
        {intentBoardItems.map((intent) => (
          <IntentCard intent={intent} key={intent.id} />
        ))}
      </div>
    </section>
  );
}
