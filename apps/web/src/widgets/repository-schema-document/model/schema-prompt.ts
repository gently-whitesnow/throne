/**
 * schema_map copy-prompt. The explicit `get_instruction_bundle({mode:
 * "schema_map"})` call is load-bearing — it is the phrase the MCP MiniRouter
 * keys on to pick the bundle. schema_map runs without an intent, so the
 * operator pastes this into a terminal sitting in a clone of the repository.
 */
export function buildSchemaMapPrompt(fullName: string): string {
  return (
    `Собери карту схемы БД для репозитория ${fullName}. ` +
    'Зачитай бандл schema_map через MCP get_instruction_bundle({mode: "schema_map"}) и следуй инструкции.'
  );
}
