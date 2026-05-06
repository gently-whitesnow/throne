import { Outlet, useParams } from "react-router-dom";

import { IntentBoard } from "@/widgets/intent-board";

export function IntentsSectionPage() {
  const { id } = useParams<{ id?: string }>();

  return (
    <div className="grid h-screen overflow-hidden max-md:grid-cols-1 max-md:grid-rows-[minmax(180px,40vh)_1fr] md:grid-cols-[320px_1fr] md:grid-rows-1">
      <IntentBoard />
      <section
        className="flex min-h-0 min-w-0 flex-col overflow-hidden"
        aria-label="Детали Intent"
      >
        {id ? (
          <Outlet />
        ) : (
          <div className="flex h-full flex-col items-center justify-center gap-1 text-sm text-base-content/60">
            <p className="m-0">Выберите Intent слева</p>
            <p className="m-0 text-xs text-base-content/60">
              Или создайте новый кнопкой «Создать»
            </p>
          </div>
        )}
      </section>
    </div>
  );
}
