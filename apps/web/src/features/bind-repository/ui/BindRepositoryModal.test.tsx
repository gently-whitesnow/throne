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

const listMyGithubRepositories =
  vi.fn<(limit?: number) => Promise<GitRepositoryRef[]>>();
const searchGithubRepositories =
  vi.fn<(params: unknown) => Promise<GitRepositoryRef[]>>();
const bindIntentRepository =
  vi.fn<
    (
      intentId: string,
      body: Record<string, unknown>
    ) => Promise<RepositoryBinding>
  >();

vi.mock("@/entities/repository-binding/api/repository-bindings-api", () => ({
  listMyGithubRepositories: (limit?: number) => listMyGithubRepositories(limit),
  searchGithubRepositories: (params: unknown) =>
    searchGithubRepositories(params),
  bindIntentRepository: (intentId: string, body: Record<string, unknown>) =>
    bindIntentRepository(intentId, body),
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
    listMyGithubRepositories.mockReset();
    searchGithubRepositories.mockReset();
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
    listMyGithubRepositories.mockResolvedValue([makeRef()]);
    render(
      <BindRepositoryModal intentId="intent-1" open onClose={() => undefined} />
    );
    await flushDebounce();
    await waitFor(() => {
      expect(listMyGithubRepositories).toHaveBeenCalledTimes(1);
    });
    expect(searchGithubRepositories).not.toHaveBeenCalled();
    expect(
      screen.getByTestId("bind-repository-row-octocat/hello-world")
    ).toBeTruthy();
  });

  it("переключение чекбокса «involved» вызывает search со scope=involved", async () => {
    listMyGithubRepositories.mockResolvedValue([]);
    searchGithubRepositories.mockResolvedValue([
      makeRef({ owner: "acme", repo: "x", full_name: "acme/x" })
    ]);
    render(
      <BindRepositoryModal intentId="intent-1" open onClose={() => undefined} />
    );
    await flushDebounce();

    fireEvent.click(screen.getByTestId("bind-repository-scope-involved"));
    await flushDebounce();

    await waitFor(() => {
      expect(searchGithubRepositories).toHaveBeenCalledWith(
        expect.objectContaining({ scope: "involved" })
      );
    });
    expect(screen.getByTestId("bind-repository-row-acme/x")).toBeTruthy();
  });

  it("после выбора репо submit отправляет bindIntentRepository с branch и pull_request_number", async () => {
    listMyGithubRepositories.mockResolvedValue([makeRef()]);
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
    listMyGithubRepositories.mockResolvedValue([makeRef()]);
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
    listMyGithubRepositories.mockResolvedValue([makeRef()]);
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
    listMyGithubRepositories.mockResolvedValue([makeRef()]);
    render(
      <BindRepositoryModal intentId="intent-1" open onClose={() => undefined} />
    );
    await flushDebounce();
    const submit = screen.getByTestId("bind-repository-submit");
    expect(submit.hasAttribute("disabled")).toBe(true);
  });
});
