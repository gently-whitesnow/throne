import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { Modal } from "./Modal";

describe("Modal", () => {
  afterEach(() => {
    cleanup();
    document.body.style.overflow = "";
  });

  it("renders a labelled dialog into document.body and locks scroll", () => {
    render(
      <Modal onClose={vi.fn()} labelledBy="title">
        <h2 id="title">Заголовок</h2>
      </Modal>
    );

    const dialog = screen.getByRole("dialog", { name: "Заголовок" });
    expect(dialog.getAttribute("aria-modal")).toBe("true");
    expect(dialog.closest("body")).toBe(document.body);
    expect(document.body.style.overflow).toBe("hidden");
  });

  it("closes on Escape and on overlay click, but not on box click", () => {
    const onClose = vi.fn();
    render(
      <Modal onClose={onClose} ariaLabel="dlg">
        <button type="button">внутри</button>
      </Modal>
    );

    fireEvent.click(screen.getByText("внутри"));
    expect(onClose).not.toHaveBeenCalled();

    fireEvent.keyDown(window, { key: "Escape" });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("does not subscribe Escape when closeOnEscape is false", () => {
    const onClose = vi.fn();
    render(
      <Modal onClose={onClose} ariaLabel="dlg" closeOnEscape={false}>
        <span>тело</span>
      </Modal>
    );

    fireEvent.keyDown(window, { key: "Escape" });
    expect(onClose).not.toHaveBeenCalled();
  });

  it("restores body scroll on unmount", () => {
    const { unmount } = render(
      <Modal onClose={vi.fn()} ariaLabel="dlg">
        <span>тело</span>
      </Modal>
    );
    expect(document.body.style.overflow).toBe("hidden");
    unmount();
    expect(document.body.style.overflow).toBe("");
  });
});
