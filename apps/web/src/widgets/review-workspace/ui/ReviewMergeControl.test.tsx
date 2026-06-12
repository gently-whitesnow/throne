import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { PullRequestMergeStatus } from "@/entities/review-workspace";

import { ReviewMergeControl } from "./ReviewMergeControl";

afterEach(cleanup);

function mergeable(): PullRequestMergeStatus {
  return {
    mergeability: "mergeable",
    checks: "passing",
    html_url: "https://github.com/o/r/pull/42"
  };
}

describe("ReviewMergeControl", () => {
  it("мержит выбранной стратегией; удаление ветки включено по умолчанию", () => {
    const onMerge = vi.fn();
    render(
      <ReviewMergeControl
        kind="PR"
        status={mergeable()}
        statusLoading={false}
        merging={false}
        mergeError={null}
        onMerge={onMerge}
      />
    );

    const deleteBranch =
      screen.getByLabelText<HTMLInputElement>("Удалить ветку");
    expect(deleteBranch.checked).toBe(true);

    fireEvent.change(screen.getByLabelText("Стратегия мержа"), {
      target: { value: "squash" }
    });
    fireEvent.click(screen.getByRole("button", { name: /Смержить/ }));

    // «Удалить ветку» и «Завершить сессию после мержа» включены по умолчанию → true.
    expect(onMerge).toHaveBeenCalledWith("squash", true, true);
  });

  it("снятие «Удалить ветку» передаёт false", () => {
    const onMerge = vi.fn();
    render(
      <ReviewMergeControl
        kind="PR"
        status={mergeable()}
        statusLoading={false}
        merging={false}
        mergeError={null}
        onMerge={onMerge}
      />
    );

    fireEvent.click(screen.getByLabelText("Удалить ветку"));
    fireEvent.click(screen.getByRole("button", { name: /Смержить/ }));

    expect(onMerge).toHaveBeenCalledWith("merge", false, true);
  });

  it("очистка состояния включена по умолчанию; снятие передаёт false", () => {
    const onMerge = vi.fn();
    render(
      <ReviewMergeControl
        kind="PR"
        status={mergeable()}
        statusLoading={false}
        merging={false}
        mergeError={null}
        onMerge={onMerge}
      />
    );

    const cleanup = screen.getByLabelText<HTMLInputElement>(
      "Очистить состояние после мержа"
    );
    expect(cleanup.checked).toBe(true);

    fireEvent.click(cleanup);
    fireEvent.click(screen.getByRole("button", { name: /Смержить/ }));

    expect(onMerge).toHaveBeenCalledWith("merge", true, false);
  });

  it("блокирует мерж и показывает ссылку на провайдера, когда мерж недоступен", () => {
    const onMerge = vi.fn();
    render(
      <ReviewMergeControl
        kind="PR"
        status={{
          mergeability: "conflicting",
          checks: "failing",
          html_url: "https://github.com/o/r/pull/42"
        }}
        statusLoading={false}
        merging={false}
        mergeError={null}
        onMerge={onMerge}
      />
    );

    expect(
      screen.getByRole("button", { name: /Смержить/ }).hasAttribute("disabled")
    ).toBe(true);
    const link = screen.getByRole("link", { name: /Открыть PR/ });
    expect(link.getAttribute("href")).toBe("https://github.com/o/r/pull/42");
    expect(screen.getByText("Конфликты")).toBeTruthy();
  });

  it("показывает причину отказа мержа", () => {
    render(
      <ReviewMergeControl
        kind="MR"
        status={mergeable()}
        statusLoading={false}
        merging={false}
        mergeError="Provider refused: branch protection"
        onMerge={vi.fn()}
      />
    );

    expect(screen.getByRole("alert").textContent).toContain(
      "branch protection"
    );
  });
});
