import type { ReactNode } from "react";

import type { IntentDetail } from "@/entities/intent";
import { IntentAttachmentsPanel } from "@/features/manage-intent-attachments";
import { AgentTerminalPanel } from "@/widgets/intent-panels/agent-terminal-panel";
import { CardAttachmentsList } from "@/widgets/intent-panels/card-attachments-list";
import { IntentActivityTimeline } from "@/widgets/intent-panels/intent-activity-timeline";
import { IntentLinksSection } from "@/widgets/intent-panels/intent-links-section";
import { PullRequestCommentsSection } from "@/widgets/intent-panels/pull-request-comments";
import { RepositoryBindingsList } from "@/widgets/intent-panels/repository-bindings-list";

import type { PanelGating } from "./select-panels";

export interface IntentPanelContext {
  intent: IntentDetail;
}

export interface IntentPanelDescriptor extends PanelGating {
  id: string;
  Component: (ctx: IntentPanelContext) => ReactNode;
}

/**
 * Статический реестр панелей детальной страницы интента — единая точка
 * расширения. Новая фича (verification, project memory, diff) добавляет сюда
 * дескриптор с placement/order/capability, а не правит композицию страницы.
 * order оставлен с шагом 10, чтобы вставлять панели между существующими.
 */
export const intentDetailPanels: readonly IntentPanelDescriptor[] = [
  {
    id: "attachments",
    placement: "primary",
    order: 10,
    Component: ({ intent }) => <IntentAttachmentsPanel intentId={intent.id} />
  },
  {
    id: "card-attachments",
    placement: "primary",
    order: 20,
    Component: ({ intent }) => <CardAttachmentsList intentId={intent.id} />
  },
  {
    id: "repository-bindings",
    placement: "review",
    order: 10,
    Component: ({ intent }) => <RepositoryBindingsList intentId={intent.id} />
  },
  {
    id: "pull-request-comments",
    placement: "review",
    order: 20,
    Component: ({ intent }) => (
      <PullRequestCommentsSection intentId={intent.id} />
    )
  },
  {
    id: "links",
    placement: "context",
    order: 10,
    Component: ({ intent }) => <IntentLinksSection intentId={intent.id} />
  },
  {
    id: "activity",
    placement: "context",
    order: 20,
    Component: ({ intent }) => <IntentActivityTimeline intentId={intent.id} />
  },
  {
    id: "terminal",
    placement: "terminal",
    order: 10,
    Component: ({ intent }) => (
      // Remount per intent so the launch axis / mode draft never leaks across intents —
      // each intent restores its own persisted choice (ADR-0041).
      <AgentTerminalPanel
        key={intent.id}
        intentId={intent.id}
        intentStatus={intent.status}
      />
    )
  }
];
