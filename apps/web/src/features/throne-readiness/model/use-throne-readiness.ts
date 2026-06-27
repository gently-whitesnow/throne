import { useCallback } from "react";

import {
  gitProviderEntries,
  isProviderHealthy,
  useGitProvidersStatus
} from "@/entities/git-provider-status";
import {
  useTerminalVendorCatalogQuery,
  type TerminalVendorCatalog,
  type TerminalVendorMetadata
} from "@/entities/terminal-setting";
import { useWorkspaceSettings } from "@/entities/workspace-setting";

export type ReadinessItemKey = "vendor" | "tmux" | "git" | "workspace";

export interface ReadinessItem {
  key: ReadinessItemKey;
  label: string;
  ok: boolean;
  detail: string;
  /** Copy-paste remediation command for an unmet item, when one exists. */
  command?: string;
  hintHref?: string;
}

export interface ThroneReadiness {
  ready: boolean;
  items: ReadinessItem[];
  isLoading: boolean;
  /** Re-run every probe live (the «Перепроверить» button after a fix). */
  refresh: () => void;
}

const HINT = {
  vendor: "https://code.claude.com/docs/en/authentication",
  tmux: "https://github.com/tmux/tmux/wiki/Installing",
  git: "https://docs.github.com/en/github-cli/github-cli/quickstart"
} as const;

const VENDOR_INSTALL_COMMAND: Record<string, string> = {
  claude: "npm install -g @anthropic-ai/claude-code",
  codex: "npm install -g @openai/codex"
};

const VENDOR_LOGIN_COMMAND: Record<string, string> = {
  claude: "claude  # запустит сессию — затем выполните /login",
  codex: "codex login"
};

/**
 * Агрегирует «Throne готов» — полный путь до Run: агент установлен И залогинен,
 * tmux установлен, git-провайдер авторизован, workspace writable. Живёт в
 * features (а не в виджете), потому что и AppShell-бейдж, и панель готовности, и
 * экран /start переиспользуют одну и ту же логику без cross-import между
 * виджетами (Steiger запретил бы widget→widget).
 */
export function useThroneReadiness(): ThroneReadiness {
  const git = useGitProvidersStatus();
  const catalog = useTerminalVendorCatalogQuery();
  const workspace = useWorkspaceSettings();

  const isLoading =
    (git.isLoading && git.status === null) ||
    catalog.isLoading ||
    (workspace.isLoading && workspace.settings === null);

  const items: ReadinessItem[] = [
    buildVendorItem(catalog.data?.vendors ?? []),
    buildTmuxItem(catalog.data?.runtime),
    buildGitItem(git.status),
    buildWorkspaceItem(workspace.settings)
  ];

  const refresh = useCallback(() => {
    git.refresh();
    workspace.refresh();
    void catalog.refetch();
  }, [git, workspace, catalog]);

  return { ready: items.every((i) => i.ok), items, isLoading, refresh };
}

function buildVendorItem(
  vendors: readonly TerminalVendorMetadata[]
): ReadinessItem {
  const base = {
    key: "vendor",
    label: "Агент установлен и залогинен"
  } as const;

  const ready = vendors.find((v) => v.login_status === "ready");
  if (ready !== undefined) {
    return { ...base, ok: true, detail: `${ready.label} залогинен` };
  }

  // «Установлен, но не залогинен» — отдельный случай от «не установлен»:
  // login_status уже различает их (CliLoginProbe), осталось дать верную команду.
  const loggedOut = vendors.find((v) => v.login_status === "logged_out");
  if (loggedOut !== undefined) {
    return {
      ...base,
      ok: false,
      detail: `${loggedOut.label} установлен, но вы не залогинены`,
      command:
        VENDOR_LOGIN_COMMAND[loggedOut.vendor] ?? VENDOR_LOGIN_COMMAND.claude,
      hintHref: HINT.vendor
    };
  }

  return {
    ...base,
    ok: false,
    detail: "CLI агента (claude или codex) не установлен",
    command: VENDOR_INSTALL_COMMAND.claude,
    hintHref: HINT.vendor
  };
}

function buildTmuxItem(
  runtime: TerminalVendorCatalog["runtime"] | undefined
): ReadinessItem {
  const tmux = runtime?.tmux;
  if (tmux?.detected === true) {
    return {
      key: "tmux",
      label: "tmux установлен",
      ok: true,
      detail: tmux.detail ?? "tmux найден"
    };
  }
  return {
    key: "tmux",
    label: "tmux установлен",
    ok: false,
    detail: "tmux не найден — без него «Запустить агента» не сработает",
    command: "brew install tmux",
    hintHref: HINT.tmux
  };
}

function buildGitItem(
  status: ReturnType<typeof useGitProvidersStatus>["status"]
): ReadinessItem {
  const ready = gitProviderEntries(status).find((entry) =>
    isProviderHealthy(entry.status)
  );
  const ok = ready !== undefined;
  return {
    key: "git",
    label: "Git-провайдер авторизован",
    ok,
    detail: ok
      ? `${providerLabel(ready.provider)} авторизован`
      : "Авторизуйтесь в Git provider CLI",
    command: ok ? undefined : "gh auth login",
    hintHref: HINT.git
  };
}

function providerLabel(provider: string): string {
  if (provider === "github") return "GitHub";
  if (provider === "gitlab") return "GitLab";
  return provider;
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
