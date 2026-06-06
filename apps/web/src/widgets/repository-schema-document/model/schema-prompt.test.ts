import { describe, expect, it } from "vitest";

import { buildSchemaMapPrompt } from "./schema-prompt";

describe("buildSchemaMapPrompt", () => {
  it("включает имя репозитория и load-bearing вызов бандла schema_map", () => {
    const prompt = buildSchemaMapPrompt("octocat/hello-world");
    expect(prompt).toContain("octocat/hello-world");
    expect(prompt).toContain('get_instruction_bundle({mode: "schema_map"})');
  });
});
