import { Outlet, useParams } from "react-router-dom";

import { IntentBoard } from "@/widgets/intent-board";

export function IntentsSectionPage() {
  const { id } = useParams<{ id?: string }>();

  return (
    <div className="section-pane">
      <IntentBoard />
      <section className="detail-pane" aria-label="Детали Intent">
        {id ? (
          <Outlet />
        ) : (
          <div className="detail-pane__placeholder">
            <p>Выберите Intent слева</p>
            <p className="detail-pane__hint-muted">
              Или создайте новый кнопкой «Создать»
            </p>
          </div>
        )}
      </section>
    </div>
  );
}
