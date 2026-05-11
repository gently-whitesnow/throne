import { McpTokenCard } from "@/widgets/mcp-token-card";

export function McpTokenPage() {
  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-5 px-5 py-8">
      <header className="flex flex-col gap-1.5">
        <p className="m-0 text-xs font-bold uppercase tracking-wider text-primary">
          Профиль
        </p>
        <h1 className="m-0 text-2xl font-bold leading-tight">MCP Token</h1>
        <p className="m-0 max-w-[64ch] text-sm leading-relaxed text-base-content/70">
          Управляйте Personal Access Token для MCP-клиентов: текущая мета,
          генерация и перевыпуск.
        </p>
      </header>
      <McpTokenCard />
    </div>
  );
}
