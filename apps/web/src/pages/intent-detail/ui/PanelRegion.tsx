import { Fragment } from "react";

import type { IntentDetail } from "@/entities/intent";

import { intentDetailPanels } from "../model/panel-registry";
import { selectPanels, type PanelPlacement } from "../model/select-panels";

interface PanelRegionProps {
  placement: PanelPlacement;
  intent: IntentDetail;
}

/**
 * Внутренний ритм региона: связанное (review, context) держим плотнее, чем
 * расстояние между регионами (его задаёт gap каркаса). Шкала 12/16 по
 * иерархии, см. DESIGN.md «Layout».
 */
const regionGap: Record<PanelPlacement, string> = {
  terminal: "gap-4",
  primary: "gap-4",
  review: "gap-4",
  context: "gap-3"
};

export function PanelRegion({ placement, intent }: PanelRegionProps) {
  const panels = selectPanels(intentDetailPanels, placement);

  if (panels.length === 0) return null;

  return (
    <div className={`flex flex-col ${regionGap[placement]}`}>
      {panels.map((panel) => (
        <Fragment key={panel.id}>{panel.Component({ intent })}</Fragment>
      ))}
    </div>
  );
}
