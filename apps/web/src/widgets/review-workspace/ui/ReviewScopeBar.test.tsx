import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { RepositoryBinding } from "@/entities/repository-binding";

import { ReviewScopeBar } from "./ReviewScopeBar";

afterEach(cleanup);

function binding(): RepositoryBinding {
  return {
    id: "b1",
    intent_id: "intent-1",
    provider: "github",
    host: "github.com",
    owner: "octocat",
    repo: "hello-world",
    default_branch: "main",
    workspace_path: "/tmp/throne/intent-1/b1",
    clone_status: "ready",
    pull_request_number: 42,
    pull_request_state: "open",
    created_at: "2026-05-20T10:00:00Z",
    updated_at: "2026-05-20T10:00:00Z"
  };
}

describe("ReviewScopeBar", () => {
  it("рендерит слот openInVscode слева от merge-control", () => {
    render(
      <ReviewScopeBar
        binding={binding()}
        scope="request"
        selectedCommitSha={null}
        commits={[]}
        commitsLoading={false}
        openInVscode={
          <button type="button" data-testid="vscode-slot">
            VS Code
          </button>
        }
        mergeControl={
          <button type="button" data-testid="merge-slot">
            Смержить
          </button>
        }
        onSelectRequest={vi.fn()}
        onSelectCommit={vi.fn()}
        onClose={vi.fn()}
      />
    );

    const vscode = screen.getByTestId("vscode-slot");
    const merge = screen.getByTestId("merge-slot");
    expect(vscode).toBeTruthy();
    expect(merge).toBeTruthy();
    expect(
      vscode.compareDocumentPosition(merge) & Node.DOCUMENT_POSITION_FOLLOWING
    ).toBeGreaterThan(0);
  });
});
