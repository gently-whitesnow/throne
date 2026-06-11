import type { ReactNode } from "react";

import type { IntentDetail } from "@/entities/intent";
import { IntentAttachmentsPanel } from "@/features/manage-intent-attachments";
import { AgentTerminalPanel } from "@/widgets/agent-terminal-panel";
import { IntentActivityTimeline } from "@/widgets/intent-activity-timeline";
import { IntentLinksSection } from "@/widgets/intent-links-section";
import { PullRequestCommentsSection } from "@/widgets/pull-request-comments";
import { RepositoryBindingsList } from "@/widgets/repository-bindings-list";
import { ReviewWorkspaceEntry } from "@/widgets/review-workspace";

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
    id: "repository-bindings",
    placement: "review",
    order: 10,
    capability: "repositories",
    Component: ({ intent }) => <RepositoryBindingsList intentId={intent.id} />
  },
  {
    id: "review-workspace",
    placement: "review",
    order: 15,
    capability: "repositories",
    Component: ({ intent }) => <ReviewWorkspaceEntry intentId={intent.id} />
  },
  {
    id: "pull-request-comments",
    placement: "review",
    order: 20,
    capability: "repositories",
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
      <AgentTerminalPanel intentId={intent.id} intentStatus={intent.status} />
    )
  }
];
