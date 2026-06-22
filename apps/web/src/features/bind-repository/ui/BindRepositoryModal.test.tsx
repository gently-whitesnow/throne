import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type {
  GitRepositoryRef,
  RepositoryBinding
} from "@/entities/repository-binding";

import { BindRepositoryModal } from "./BindRepositoryModal";

// Configured GitLab host comes from `git-providers/status`; stub it so the
// manual SSH-URL host validation has a fixed host to compare against. The
// `gitlab.authenticated=true` flag also re-enables the GitLab provider button.
vi.mock("@/entities/git-provider-status", () => ({
  useGitProvidersStatus: () => ({
    status: {
      providers: [
        {
          provider: "github",
          status: {
            authenticated: true,
            state: "authenticated",
            host: "github.com"
          }
        },
        {
          provider: "gitlab",
          status: {
            authenticated: true,
            state: "authenticated",
            host: "gitlab.ati.st"
          }
        }
      ]
    },
    isLoading: false,
    error: null,
    refresh: () => undefined
  }),
  gitProviderEntries: (status: { providers?: unknown[] } | null) =>
    status?.providers ?? [],
  findGitProviderStatus: (
    status: { providers?: { provider: string; status: unknown }[] } | null,
    provider: string
  ) => status?.providers?.find((entry) => entry.provider === provider)?.status
}));

const repositoryBindingMocks = vi.hoisted(() => ({
  listGitProviderRepositories:
    vi.fn<(provider?: string, limit?: number) => Promise<GitRepositoryRef[]>>(),
  searchGitProviderRepositories:
    vi.fn<(params: unknown) => Promise<GitRepositoryRef[]>>(),
  listGitProviderRepositoryBranches: vi
    .fn<() => Promise<unknown[]>>()
    .mockResolvedValue([]),
  listGitProviderRepositoryPullRequests: vi
    .fn<() => Promise<unknown[]>>()
    .mockResolvedValue([]),
  bindIntentRepository:
    vi.fn<
      (
        intentId: string,
        body: Record<string, unknown>
      ) => Promise<RepositoryBinding>
    >()
}));

vi.mock("@/entities/repository-binding/api/repository-bindings-api", () => ({
  listGitProviderRepositories: (provider?: string, limit?: number) =>
    repositoryBindingMocks.listGitProviderRepositories(provider, limit),
  searchGitProviderRepositories: (params: unknown) =>
    repositoryBindingMocks.searchGitProviderRepositories(params),
  listGitProviderRepositoryBranches:
    repositoryBindingMocks.listGitProviderRepositoryBranches,
  listGitProviderRepositoryPullRequests:
    repositoryBindingMocks.listGitProviderRepositoryPullRequests,
  bindIntentRepository: (intentId: string, body: Record<string, unknown>) =>
    repositoryBindingMocks.bindIntentRepository(intentId, body),
  listIntentRepositories: vi.fn().mockResolvedValue([]),
  unbindIntentRepository: vi.fn()
}));

vi.mock("@/shared/api", () => {
  class HttpError extends Error {
    public extensions = {};
    constructor(
      public status: number,
      public url: string,
      message: string
    ) {
      super(message);
    }
  }
  return { HttpError };
});

import { HttpError } from "@/shared/api";

const {
  listGitProviderRepositories,
  searchGitProviderRepositories,
  listGitProviderRepositoryBranches,
  listGitProviderRepositoryPullRequests,
  bindIntentRepository
} = repositoryBindingMocks;

function makeRef(overrides: Partial<GitRepositoryRef> = {}): GitRepositoryRef {
  return {
    provider: "github",
    owner: "octocat",
    repo: "hello-world",
    full_name: "octocat/hello-world",
    default_branch: "main",
    private: false,
    ...overrides
  };
}

function makeBinding(repo = "hello-world"): RepositoryBinding {
  return {
    id: `b-${repo}`,
    intent_id: "intent-1",
    provider: "github",
    host: "github.com",
    owner: "octocat",
    repo,
    default_branch: "main",
    workspace_path: `/tmp/throne/intent-1/${repo}`,
    clone_status: "pending",
    created_at: "2026-05-20T10:00:00Z",
    updated_at: "2026-05-20T10:00:00Z"
  };
}

describe("BindRepositoryModal (мультивыбор)", () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    listGitProviderRepositories.mockReset();
    searchGitProviderRepositories.mockReset();
    listGitProviderRepositoryBranches.mockReset();
    listGitProviderRepositoryPullRequests.mockReset();
    listGitProviderRepositoryBranches.mockResolvedValue([]);
    listGitProviderRepositoryPullRequests.mockResolvedValue([]);
    bindIntentRepository.mockReset();
  });

  afterEach(() => {
    vi.useRealTimers();
    cleanup();
  });

  async function flushDebounce() {
    await act(async () => {
      await vi.advanceTimersByTimeAsync(400);
    });
  }

  it("выбор двух репозиториев оставляет оба чипами и не сбрасывает их при новом поиске", async () => {
    listGitProviderRepositories.mockResolvedValue([
      makeRef(),
      makeRef({ owner: "octocat", repo: "spoon", full_name: "octocat/spoon" })
    ]);
    render(
      <BindRepositoryModal intentId="intent-1" open onClose={() => undefined} />
    );
    await flushDebounce();

    fireEvent.click(
      screen.getByTestId("bind-repository-row-octocat/hello-world")
    );
    fireEvent.click(screen.getByTestId("bind-repository-row-octocat/spoon"));

    expect(
      screen.getByTestId("bind-repository-chip-octocat/hello-world")
    ).toBeTruthy();
    expect(
      screen.getByTestId("bind-repository-chip-octocat/spoon")
    ).toBeTruthy();

    // Новый поиск не должен ронять уже выбранные чипы.
    searchGitProviderRepositories.mockResolvedValue([]);
    fireEvent.change(screen.getByLabelText("Поиск репозитория"), {
      target: { value: "nothing" }
    });
    await flushDebounce();

    expect(
      screen.getByTestId("bind-repository-chip-octocat/hello-world")
    ).toBeTruthy();
    expect(
      screen.getByTestId("bind-repository-chip-octocat/spoon")
    ).toBeTruthy();
  });

  it("одно подтверждение привязывает все выбранные репозитории", async () => {
    listGitProviderRepositories.mockResolvedValue([
      makeRef(),
      makeRef({ owner: "octocat", repo: "spoon", full_name: "octocat/spoon" })
    ]);
    bindIntentRepository.mockImplementation((_id, body) =>
      Promise.resolve(makeBinding(String(body.repo)))
    );
    const onClose = vi.fn();
    const onBound = vi.fn();
    render(
      <BindRepositoryModal
        intentId="intent-1"
        open
        onClose={onClose}
        onBound={onBound}
      />
    );
    await flushDebounce();

    fireEvent.click(
      screen.getByTestId("bind-repository-row-octocat/hello-world")
    );
    fireEvent.click(screen.getByTestId("bind-repository-row-octocat/spoon"));
    fireEvent.click(screen.getByTestId("bind-repository-submit"));

    await waitFor(() => {
      expect(bindIntentRepository).toHaveBeenCalledTimes(2);
    });
    expect(bindIntentRepository).toHaveBeenCalledWith(
      "intent-1",
      expect.objectContaining({ repo: "hello-world", default_branch: "main" })
    );
    expect(bindIntentRepository).toHaveBeenCalledWith(
      "intent-1",
      expect.objectContaining({ repo: "spoon" })
    );
    expect(onBound).toHaveBeenCalledTimes(2);
    await waitFor(() => {
      expect(onClose).toHaveBeenCalled();
    });
  });

  it("частичная ошибка оставляет неуспешный чип и не мешает успешным", async () => {
    listGitProviderRepositories.mockResolvedValue([
      makeRef(),
      makeRef({ owner: "octocat", repo: "spoon", full_name: "octocat/spoon" })
    ]);
    bindIntentRepository.mockImplementation((_id, body) =>
      body.repo === "spoon"
        ? Promise.reject(new HttpError(409, "/x", "conflict"))
        : Promise.resolve(makeBinding(String(body.repo)))
    );
    const onClose = vi.fn();
    render(<BindRepositoryModal intentId="intent-1" open onClose={onClose} />);
    await flushDebounce();

    fireEvent.click(
      screen.getByTestId("bind-repository-row-octocat/hello-world")
    );
    fireEvent.click(screen.getByTestId("bind-repository-row-octocat/spoon"));
    fireEvent.click(screen.getByTestId("bind-repository-submit"));

    await waitFor(() => {
      expect(
        screen.getByTestId("bind-repository-chip-error-octocat/spoon")
          .textContent
      ).toMatch(/привязан/);
    });
    // Успешный чип удалён, неуспешный остался, модалка открыта.
    expect(
      screen.queryByTestId("bind-repository-chip-octocat/hello-world")
    ).toBe(null);
    expect(
      screen.getByTestId("bind-repository-chip-octocat/spoon")
    ).toBeTruthy();
    expect(onClose).not.toHaveBeenCalled();
  });

  it("добавление gitlab-репозитория вставкой SSH-URL биндит namespace/repo", async () => {
    listGitProviderRepositories.mockResolvedValue([]);
    bindIntentRepository.mockResolvedValue(makeBinding("bugget-ui"));
    render(
      <BindRepositoryModal intentId="intent-1" open onClose={() => undefined} />
    );
    await flushDebounce();

    fireEvent.change(screen.getByTestId("bind-repository-ssh-url"), {
      target: { value: "git@gitlab.ati.st:trucks/bugget/bugget-ui.git" }
    });
    fireEvent.click(screen.getByTestId("bind-repository-ssh-add"));

    expect(
      screen.getByTestId("bind-repository-chip-trucks/bugget/bugget-ui")
    ).toBeTruthy();
    expect(
      screen.queryByTestId(
        "bind-repository-chip-host-error-trucks/bugget/bugget-ui"
      )
    ).toBe(null);

    fireEvent.click(screen.getByTestId("bind-repository-submit"));
    await waitFor(() => {
      expect(bindIntentRepository).toHaveBeenCalledWith("intent-1", {
        provider: "gitlab",
        host: "gitlab.ati.st",
        owner: "trucks/bugget",
        repo: "bugget-ui"
      });
    });
  });

  it("SSH-URL с чужим gitlab-хостом даёт ошибку на чипе и не биндится", async () => {
    listGitProviderRepositories.mockResolvedValue([]);
    render(
      <BindRepositoryModal intentId="intent-1" open onClose={() => undefined} />
    );
    await flushDebounce();

    fireEvent.change(screen.getByTestId("bind-repository-ssh-url"), {
      target: { value: "git@gitlab.othercorp.com:foo/bar.git" }
    });
    fireEvent.click(screen.getByTestId("bind-repository-ssh-add"));

    expect(
      screen.getByTestId("bind-repository-chip-host-error-foo/bar").textContent
    ).toMatch(/не совпадает/);
    // Чип невалиден → submit недоступен, бинд не уходит.
    expect(
      screen.getByTestId("bind-repository-submit").hasAttribute("disabled")
    ).toBe(true);
  });

  it("малформленный SSH-URL показывает inline-ошибку и не добавляет чип", async () => {
    listGitProviderRepositories.mockResolvedValue([]);
    render(
      <BindRepositoryModal intentId="intent-1" open onClose={() => undefined} />
    );
    await flushDebounce();

    fireEvent.change(screen.getByTestId("bind-repository-ssh-url"), {
      target: { value: "https://github.com/owner/repo" }
    });
    fireEvent.click(screen.getByTestId("bind-repository-ssh-add"));

    expect(screen.getByTestId("bind-repository-ssh-error")).toBeTruthy();
    expect(screen.queryByTestId("bind-repository-selected-list")).toBe(null);
  });

  it("submit без выбранных репозиториев недоступен", async () => {
    listGitProviderRepositories.mockResolvedValue([makeRef()]);
    render(
      <BindRepositoryModal intentId="intent-1" open onClose={() => undefined} />
    );
    await flushDebounce();
    expect(
      screen.getByTestId("bind-repository-submit").hasAttribute("disabled")
    ).toBe(true);
  });

  it("повторный клик по выбранной строке убирает чип", async () => {
    listGitProviderRepositories.mockResolvedValue([makeRef()]);
    render(
      <BindRepositoryModal intentId="intent-1" open onClose={() => undefined} />
    );
    await flushDebounce();

    fireEvent.click(
      screen.getByTestId("bind-repository-row-octocat/hello-world")
    );
    expect(
      screen.getByTestId("bind-repository-chip-octocat/hello-world")
    ).toBeTruthy();

    fireEvent.click(
      screen.getByTestId("bind-repository-row-octocat/hello-world")
    );
    expect(
      screen.queryByTestId("bind-repository-chip-octocat/hello-world")
    ).toBe(null);
  });
});
