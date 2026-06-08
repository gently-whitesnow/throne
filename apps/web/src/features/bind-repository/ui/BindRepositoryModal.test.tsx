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

// GitLab provider button is gated on the `gitlab` capability; force it enabled
// so the provider-switch assertions can reach the GitLab option.
vi.mock("@/entities/capability", () => ({
  useCapabilityEnabled: () => true
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

function makeBinding(): RepositoryBinding {
  return {
    id: "b1",
    intent_id: "intent-1",
    provider: "github",
    host: "github.com",
    owner: "octocat",
    repo: "hello-world",
    default_branch: "main",
    workspace_path: "/tmp/throne/intent-1/b1",
    clone_status: "pending",
    created_at: "2026-05-20T10:00:00Z",
    updated_at: "2026-05-20T10:00:00Z"
  };
}

describe("BindRepositoryModal", () => {
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

  it("при открытии грузит «мои» репозитории по умолчанию", async () => {
    listGitProviderRepositories.mockResolvedValue([makeRef()]);
    render(
      <BindRepositoryModal intentId="intent-1" open onClose={() => undefined} />
    );
    await flushDebounce();
    await waitFor(() => {
      expect(listGitProviderRepositories).toHaveBeenCalledWith("github", 50);
    });
    expect(searchGitProviderRepositories).not.toHaveBeenCalled();
    expect(
      screen.getByTestId("bind-repository-row-octocat/hello-world")
    ).toBeTruthy();
  });

  it("переключение чекбокса «involved» вызывает search со scope=involved", async () => {
    listGitProviderRepositories.mockResolvedValue([]);
    searchGitProviderRepositories.mockResolvedValue([
      makeRef({ owner: "acme", repo: "x", full_name: "acme/x" })
    ]);
    render(
      <BindRepositoryModal intentId="intent-1" open onClose={() => undefined} />
    );
    await flushDebounce();

    fireEvent.click(screen.getByTestId("bind-repository-scope-involved"));
    await flushDebounce();

    await waitFor(() => {
      expect(searchGitProviderRepositories).toHaveBeenCalledWith(
        expect.objectContaining({ scope: "involved" })
      );
    });
    expect(screen.getByTestId("bind-repository-row-acme/x")).toBeTruthy();
  });

  it("переключение провайдера на GitLab грузит мои репозитории GitLab", async () => {
    listGitProviderRepositories.mockResolvedValue([
      makeRef({
        provider: "gitlab",
        owner: "team/platform",
        repo: "core",
        full_name: "team/platform/core"
      })
    ]);
    render(
      <BindRepositoryModal intentId="intent-1" open onClose={() => undefined} />
    );
    await flushDebounce();

    fireEvent.click(screen.getByTestId("bind-repository-provider-gitlab"));
    await flushDebounce();

    await waitFor(() => {
      expect(listGitProviderRepositories).toHaveBeenLastCalledWith(
        "gitlab",
        50
      );
    });
    expect(
      screen.getByTestId("bind-repository-row-team/platform/core")
    ).toBeTruthy();
  });

  it("после выбора репо submit отправляет bindIntentRepository с branch и pull_request_number", async () => {
    listGitProviderRepositories.mockResolvedValue([makeRef()]);
    bindIntentRepository.mockResolvedValue(makeBinding());
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
    fireEvent.change(screen.getByTestId("bind-repository-pr-number"), {
      target: { value: "42" }
    });

    fireEvent.click(screen.getByTestId("bind-repository-submit"));

    await waitFor(() => {
      expect(bindIntentRepository).toHaveBeenCalledWith("intent-1", {
        provider: "github",
        owner: "octocat",
        repo: "hello-world",
        default_branch: "main",
        pull_request_number: 42
      });
    });
    expect(onBound).toHaveBeenCalled();
    expect(onClose).toHaveBeenCalled();
  });

  it("422 на невалидном PR-number блокирует submit", async () => {
    listGitProviderRepositories.mockResolvedValue([makeRef()]);
    render(
      <BindRepositoryModal intentId="intent-1" open onClose={() => undefined} />
    );
    await flushDebounce();
    fireEvent.click(
      screen.getByTestId("bind-repository-row-octocat/hello-world")
    );
    fireEvent.change(screen.getByTestId("bind-repository-pr-number"), {
      target: { value: "-5" }
    });
    const submit = screen.getByTestId("bind-repository-submit");
    expect(submit.hasAttribute("disabled")).toBe(true);
    expect(bindIntentRepository).not.toHaveBeenCalled();
  });

  it("409 от сервера показывает «уже привязан»", async () => {
    listGitProviderRepositories.mockResolvedValue([makeRef()]);
    bindIntentRepository.mockRejectedValue(
      new HttpError(409, "/x", "conflict")
    );
    render(
      <BindRepositoryModal intentId="intent-1" open onClose={() => undefined} />
    );
    await flushDebounce();
    fireEvent.click(
      screen.getByTestId("bind-repository-row-octocat/hello-world")
    );
    fireEvent.click(screen.getByTestId("bind-repository-submit"));
    await waitFor(() => {
      expect(screen.getByTestId("bind-repository-error").textContent).toMatch(
        /уже привязан/
      );
    });
  });

  it("submit без выбранного репо невозможен", async () => {
    listGitProviderRepositories.mockResolvedValue([makeRef()]);
    render(
      <BindRepositoryModal intentId="intent-1" open onClose={() => undefined} />
    );
    await flushDebounce();
    const submit = screen.getByTestId("bind-repository-submit");
    expect(submit.hasAttribute("disabled")).toBe(true);
  });
});
