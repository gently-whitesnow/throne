import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";

import { intentsQueryKeys, type IntentDetail } from "@/entities/intent";
import { ReplaceIntentTextForm } from "@/features/replace-intent-text";
import { IntentTagsInline } from "@/features/set-intent-tags";

import { IntentDetailHeader } from "./IntentDetailHeader";
import { PanelRegion } from "./PanelRegion";

interface IntentDetailShellProps {
  intent: IntentDetail;
}

/**
 * Оболочка детальной страницы интента. Держит фиксированный каркас (header,
 * первичный текст, регионы панелей) и состояние редактирования; конкретные
 * виджеты приходят из реестра панелей через PanelRegion, а не хардкодятся тут.
 */
export function IntentDetailShell({ intent }: IntentDetailShellProps) {
  const navigate = useNavigate();
  const location = useLocation();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);

  useEffect(() => {
    setEditing(false);
  }, [intent.id]);

  const patchCache = (next: IntentDetail) => {
    queryClient.setQueryData(intentsQueryKeys.detail(intent.id), next);
  };

  return (
    <>
      <IntentDetailHeader
        intent={intent}
        editing={editing}
        onEdit={() => {
          setEditing(true);
        }}
        onStatusSaved={patchCache}
        onClose={() => {
          void navigate({ pathname: "/intents", search: location.search });
        }}
        onDeleted={() => {
          void navigate("/intents");
        }}
      />

      <div className="flex min-h-0 flex-1 overflow-hidden">
        <div
          className={
            editing
              ? "flex min-h-0 flex-1 flex-col gap-3 px-6 pb-4 pt-4"
              : "flex min-h-0 flex-1 flex-col gap-6 overflow-y-auto px-6 pb-8 pt-4"
          }
        >
          <IntentTagsInline intent={intent} onSaved={patchCache} />
          {editing ? (
            <ReplaceIntentTextForm
              intent={intent}
              onSaved={(next) => {
                patchCache(next);
                setEditing(false);
              }}
              onCancel={() => {
                setEditing(false);
              }}
            />
          ) : (
            <>
              <pre className="m-0 whitespace-pre-wrap break-words font-mono text-[13px] leading-relaxed text-base-content">
                {intent.text}
              </pre>
              {/* Главный CTA «Запустить агента» сразу под текстом — один
                  очевидный primary-action, остальные секции ниже. */}
              <PanelRegion placement="terminal" intent={intent} />
              <PanelRegion placement="primary" intent={intent} />
              <PanelRegion placement="review" intent={intent} />
              <PanelRegion placement="context" intent={intent} />
            </>
          )}
        </div>
      </div>
    </>
  );
}
