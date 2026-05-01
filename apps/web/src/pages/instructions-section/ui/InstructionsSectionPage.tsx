import { Outlet, useParams } from "react-router-dom";

import { InstructionBoard } from "@/widgets/instruction-board";

export function InstructionsSectionPage() {
  const { id } = useParams<{ id?: string }>();

  return (
    <div className="section-pane">
      <InstructionBoard />
      <section className="detail-pane" aria-label="Детали Instruction">
        {id ? (
          <Outlet />
        ) : (
          <div className="detail-pane__placeholder">
            <p>Выберите инструкцию слева</p>
            <p className="detail-pane__hint-muted">
              Инструкции редактируются вручную и применяются агентом по kind
            </p>
          </div>
        )}
      </section>
    </div>
  );
}
