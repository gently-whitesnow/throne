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

/** Один вариант фикса невыполненного пункта; несколько → панель рисует вкладки. */
export interface ReadinessRemedy {
  label: string;
  command: string;
  hintHref: string;
}

export interface ReadinessItem {
  key: ReadinessItemKey;
  label: string;
  ok: boolean;
  detail: string;
  /** Варианты фикса (паритет провайдеров). Пусто для выполненного пункта. */
  remedies?: ReadinessRemedy[];
}

export interface ThroneReadiness {
  ready: boolean;
  items: ReadinessItem[];
  isLoading: boolean;
  /** Re-run every probe live (the «Перепроверить» button after a fix). */
  refresh: () => void;
}

const VENDORS = [
  {
    key: "claude",
    label: "Claude",
    install: "curl -fsSL https://claude.ai/install.sh | bash",
    login: "claude  # запустит сессию — затем выполните /login",
    doc: "https://code.claude.com/docs/en/quickstart#native-install-recommended"
  },
  {
    key: "codex",
    label: "Codex",
    install: "curl -fsSL https://chatgpt.com/codex/install.sh | sh",
    login: "codex login",
    doc: "https://developers.openai.com/codex/cli"
  }
] as const;

const GIT_REMEDIES: ReadinessRemedy[] = [
  {
    label: "GitHub",
    command: "gh auth login",
    hintHref: "https://cli.github.com/"
  },
  {
    label: "GitLab",
    command: "glab auth login",
    hintHref: "https://docs.gitlab.com/cli/"
  }
];

const TMUX_DOC = "https://github.com/tmux/tmux/wiki/Installing";

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
  const ready = vendors.find((v) => v.login_status === "ready");
  if (ready !== undefined) {
    return {
      key: "vendor",
      label: "Агент установлен и залогинен",
      ok: true,
      detail: `${ready.label} залогинен`
    };
  }

  // Паритет: оба агента — вкладками, чтобы новичок видел выбор. Команда зависит
  // от состояния: «установлен, но не залогинен» (login_status уже различает,
  // CliLoginProbe) → login, иначе → install.
  const anyLoggedOut = vendors.some((v) => v.login_status === "logged_out");
  const remedies = VENDORS.map((v) => {
    const meta = vendors.find((x) => x.vendor === v.key);
    const loggedOut = meta?.login_status === "logged_out";
    return {
      label: v.label,
      command: loggedOut ? v.login : v.install,
      hintHref: v.doc
    };
  });

  return {
    key: "vendor",
    label: "Агент установлен и залогинен",
    ok: false,
    detail: anyLoggedOut
      ? "Агент установлен, но вы не залогинены"
      : "CLI агента не установлен — выберите claude или codex",
    remedies
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
    detail: "tmux не найден — без него «Запустить агента» не получится",
    remedies: [
      { label: "tmux", command: "brew install tmux", hintHref: TMUX_DOC }
    ]
  };
}

function buildGitItem(
  status: ReturnType<typeof useGitProvidersStatus>["status"]
): ReadinessItem {
  const ready = gitProviderEntries(status).find((entry) =>
    isProviderHealthy(entry.status)
  );
  if (ready !== undefined) {
    return {
      key: "git",
      label: "Git-провайдер авторизован",
      ok: true,
      detail: `${providerLabel(ready.provider)} авторизован`
    };
  }
  return {
    key: "git",
    label: "Git-провайдер авторизован",
    ok: false,
    detail: "Авторизуйтесь в GitHub или GitLab CLI",
    remedies: GIT_REMEDIES
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
