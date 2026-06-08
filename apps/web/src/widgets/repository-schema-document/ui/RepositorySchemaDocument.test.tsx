import { cleanup, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { renderWithQuery } from "@/app/test-utils";
import type {
  RepositoryCoordinate,
  RepositoryDocument
} from "@/entities/repository";
import { HttpError } from "@/shared/api";

import { RepositorySchemaDocument } from "./RepositorySchemaDocument";

const getRepositoryDocument =
  vi.fn<
    (c: RepositoryCoordinate, slug: string) => Promise<RepositoryDocument>
  >();

// Mermaid pulls a heavy ESM bundle and needs real layout — stub it so the
// markdown surface renders deterministically in jsdom.
vi.mock("mermaid", () => ({
  default: {
    initialize: vi.fn(),
    render: vi.fn().mockResolvedValue({ svg: "<svg></svg>" })
  }
}));

vi.mock("@/entities/repository/api/repositories-api", () => ({
  getRepositoryDocument: (c: RepositoryCoordinate, slug: string) =>
    getRepositoryDocument(c, slug),
  getRepository: vi.fn(),
  listRepositories: vi.fn(),
  listRepositoryDocuments: vi.fn(),
  listRepositoryDocumentVersions: vi.fn(),
  putRepositoryDocument: vi.fn()
}));

const coordinate: RepositoryCoordinate = {
  provider: "github",
  host: "github.com",
  owner: "octocat",
  repo: "hello-world"
};

afterEach(() => {
  cleanup();
  getRepositoryDocument.mockReset();
});

describe("RepositorySchemaDocument", () => {
  it("рендерит markdown-тело существующей карты схемы", async () => {
    getRepositoryDocument.mockResolvedValue({
      provider: "github",
      host: "github.com",
      owner: "octocat",
      repo: "hello-world",
      slug: "db-schema-map",
      title: "Карта схемы БД",
      document: "## Таблица users\n\nХранит пользователей.",
      render_hint: "schema_map",
      version: 3,
      created_at: "2026-06-01T10:00:00Z",
      updated_at: "2026-06-02T10:00:00Z"
    });

    renderWithQuery(
      <RepositorySchemaDocument
        coordinate={coordinate}
        fullName="octocat/hello-world"
      />,
      { withBridge: false }
    );

    await waitFor(() => {
      expect(screen.getByText("Таблица users")).toBeTruthy();
    });
    expect(screen.getByRole("button", { name: /Править/ })).toBeTruthy();
  });

  it("показывает пустое состояние и copy-prompt при 404", async () => {
    getRepositoryDocument.mockRejectedValue(
      new HttpError(404, "/documents/db-schema-map", "not found")
    );

    renderWithQuery(
      <RepositorySchemaDocument
        coordinate={coordinate}
        fullName="octocat/hello-world"
      />,
      { withBridge: false }
    );

    await waitFor(() => {
      expect(screen.getByText(/ещё не сформирована/)).toBeTruthy();
    });
    expect(
      screen.getByRole("button", { name: /промпт для запуска schema_map/i })
    ).toBeTruthy();
  });
});
