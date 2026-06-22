import { selectCapability, useCapabilities } from "@/entities/capability";
import {
  isProviderHealthy,
  useGitProvidersStatus
} from "@/entities/git-provider-status";
import {
  useTerminalVendorCatalogQuery,
  type TerminalVendorMetadata
} from "@/entities/terminal-setting";
import { useWorkspaceSettings } from "@/entities/workspace-setting";

export type ReadinessItemKey = "vendor" | "git" | "tmux" | "workspace";

export interface ReadinessItem {
  key: ReadinessItemKey;
  label: string;
  ok: boolean;
  detail: string;
  hintHref?: string;
}

export interface ThroneReadiness {
  ready: boolean;
  items: ReadinessItem[];
  isLoading: boolean;
}

const HINT = {
  vendor: "https://code.claude.com/docs/en/authentication",
  git: "https://docs.github.com/en/github-cli/github-cli/quickstart",
  tmux: "https://github.com/tmux/tmux/wiki/Installing"
} as const;

/**
 * Агрегирует «Throne готов» из четырёх независимых entity-источников. Живёт в
 * features (а не в виджете), потому что и AppShell-бейдж, и панель готовности
 * — два виджета — должны переиспользовать одну и ту же логику без cross-import
 * между виджетами (Steiger запретил бы widget→widget).
 */
export function useThroneReadiness(): ThroneReadiness {
  const caps = useCapabilities();
  const git = useGitProvidersStatus();
  const catalog = useTerminalVendorCatalogQuery();
  const workspace = useWorkspaceSettings();

  // isLoading — любой источник ещё грузит первый ответ (нет данных). Entity-хуки
  // уже маппят свои флаги в «первый fetch без данных»; у каталога `isLoading`
  // уже означает pending-без-данных.
  const isLoading =
    (caps.isLoading && caps.capabilities.length === 0) ||
    (git.isLoading && git.status === null) ||
    catalog.isLoading ||
    (workspace.isLoading && workspace.settings === null);

  const items: ReadinessItem[] = [
    buildVendorItem(catalog.data?.vendors ?? []),
    buildGitItem(git.status),
    buildTmuxItem(caps),
    buildWorkspaceItem(workspace.settings)
  ];

  return { ready: items.every((i) => i.ok), items, isLoading };
}

function buildVendorItem(
  vendors: readonly TerminalVendorMetadata[]
): ReadinessItem {
  const ready = vendors.find((v) => v.login_status === "ready");
  return {
    key: "vendor",
    label: "Агент залогинен",
    ok: ready !== undefined,
    detail:
      ready !== undefined
        ? `${ready.label} залогинен`
        : "Залогиньтесь в claude или codex",
    hintHref: HINT.vendor
  };
}

function buildGitItem(
  status: ReturnType<typeof useGitProvidersStatus>["status"]
): ReadinessItem {
  // Селектор entity терпит undefined-провайдера (бэкенд может вернуть только
  // один из github/gitlab), поэтому не падаем на частичном ответе.
  const github = isProviderHealthy(status?.github);
  const gitlab = isProviderHealthy(status?.gitlab);
  const ok = github || gitlab;
  return {
    key: "git",
    label: "Git-провайдер авторизован",
    ok,
    detail: ok
      ? github
        ? "GitHub авторизован"
        : "GitLab авторизован"
      : "Авторизуйтесь через gh или glab",
    hintHref: HINT.git
  };
}

function buildTmuxItem(
  caps: ReturnType<typeof useCapabilities>
): ReadinessItem {
  const terminal = selectCapability(caps.capabilities, "terminal");
  const ok = terminal?.detected === true;
  return {
    key: "tmux",
    label: "tmux установлен",
    ok,
    detail: ok ? "tmux найден на хосте" : "Установите tmux на хосте",
    hintHref: HINT.tmux
  };
}

function buildWorkspaceItem(
  settings: ReturnType<typeof useWorkspaceSettings>["settings"]
): ReadinessItem {
  const ok = settings?.writable === true;
  return {
    key: "workspace",
    label: "Workspace доступен на запись",
    ok,
    detail: ok
      ? "Корень workspace доступен на запись"
      : "Корень workspace недоступен на запись"
  };
}
