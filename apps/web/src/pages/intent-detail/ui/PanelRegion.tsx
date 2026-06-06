import { Fragment } from "react";

import { isCapabilityEnabled, useCapabilities } from "@/entities/capability";
import type { IntentDetail } from "@/entities/intent";

import { intentDetailPanels } from "../model/panel-registry";
import { selectPanels, type PanelPlacement } from "../model/select-panels";

interface PanelRegionProps {
  placement: PanelPlacement;
  intent: IntentDetail;
}

export function PanelRegion({ placement, intent }: PanelRegionProps) {
  const { capabilities } = useCapabilities();
  const panels = selectPanels(intentDetailPanels, placement, (capability) =>
    isCapabilityEnabled(capabilities, capability)
  );

  if (panels.length === 0) return null;

  return (
    <>
      {panels.map((panel) =>
        panel.title ? (
          <section key={panel.id} className="mt-6 flex flex-col gap-2">
            <h2 className="m-0 text-sm font-semibold text-base-content">
              {panel.title}
            </h2>
            {panel.Component({ intent })}
          </section>
        ) : (
          <Fragment key={panel.id}>{panel.Component({ intent })}</Fragment>
        )
      )}
    </>
  );
}
