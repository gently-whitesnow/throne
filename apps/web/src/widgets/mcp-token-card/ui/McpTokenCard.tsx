import { AlertTriangle, Check, Copy, KeyRound } from "lucide-react";
import { useState } from "react";

import type { McpTokenIssued, McpTokenMeta } from "@/entities/mcp-token";
import { GenerateMcpTokenButton } from "@/features/generate-mcp-token";
import { RegenerateMcpTokenButton } from "@/features/regenerate-mcp-token";
import { Button } from "@/shared/ui";

import { useMcpTokenMeta } from "../model/use-mcp-token-meta";

const dateFormatter = new Intl.DateTimeFormat("ru-RU", {
  dateStyle: "medium",
  timeStyle: "short"
});

export function McpTokenCard() {
  const { state, setMeta } = useMcpTokenMeta();
  const [issued, setIssued] = useState<McpTokenIssued | null>(null);

  const handleIssued = (next: McpTokenIssued) => {
    setIssued(next);
    setMeta({
      has_token: true,
      created_at: next.created_at,
      last_four: next.last_four
    });
  };

  if (state.kind === "loading") {
    return (
      <section
        aria-label="MCP-токен"
        className="rounded-lg border border-base-300 bg-base-100 p-5"
      >
        <p className="m-0 text-sm text-base-content/60">Загрузка токена…</p>
      </section>
    );
  }

  if (state.kind === "error") {
    return (
      <section
        aria-label="MCP-токен"
        className="rounded-lg border border-base-300 bg-base-100 p-5"
      >
        <p role="alert" className="m-0 text-sm text-error">
          {state.message}
        </p>
      </section>
    );
  }

  return (
    <section
      aria-label="MCP-токен"
      className="flex flex-col gap-4 rounded-lg border border-base-300 bg-base-100 p-5"
    >
      <header className="flex items-start gap-3">
        <span
          aria-hidden
          className="inline-flex h-9 w-9 items-center justify-center rounded-md bg-primary/10 text-primary"
        >
          <KeyRound size={18} strokeWidth={2} />
        </span>
        <div className="flex flex-col gap-1">
          <h2 className="m-0 text-lg font-bold leading-tight">MCP-токен</h2>
          <p className="m-0 max-w-[60ch] text-sm leading-relaxed text-base-content/70">
            Personal Access Token для подключения MCP-клиентов к Throne.
            Используйте его как Bearer-токен.
          </p>
        </div>
      </header>

      <TokenStatus meta={state.data} />

      {issued ? (
        <IssuedSecretPanel
          issued={issued}
          onDismiss={() => {
            setIssued(null);
          }}
        />
      ) : null}

      <div className="flex flex-wrap gap-2">
        {state.data.has_token ? (
          <RegenerateMcpTokenButton onIssued={handleIssued} />
        ) : (
          <GenerateMcpTokenButton onIssued={handleIssued} />
        )}
      </div>
    </section>
  );
}

function TokenStatus({ meta }: { meta: McpTokenMeta }) {
  if (!meta.has_token) {
    return (
      <p className="m-0 text-sm text-base-content/70">
        Активного токена пока нет. Сгенерируйте новый, чтобы подключить
        MCP-клиента.
      </p>
    );
  }

  return (
    <dl className="grid grid-cols-[auto_1fr] items-baseline gap-x-3 gap-y-1.5 text-sm">
      <dt className="text-base-content/60">Создан</dt>
      <dd className="m-0 tabular-nums">
        {meta.created_at
          ? dateFormatter.format(new Date(meta.created_at))
          : "—"}
      </dd>
      <dt className="text-base-content/60">Окончание</dt>
      <dd className="m-0 font-mono">
        {meta.last_four ? `••••${meta.last_four}` : "—"}
      </dd>
    </dl>
  );
}

function IssuedSecretPanel({
  issued,
  onDismiss
}: {
  issued: McpTokenIssued;
  onDismiss: () => void;
}) {
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(issued.token);
      setCopied(true);
    } catch {
      setCopied(false);
    }
  };

  return (
    <div
      role="region"
      aria-label="Только что выпущенный токен"
      className="flex flex-col gap-3 rounded-md border border-warning/40 bg-warning/10 p-4"
    >
      <div className="flex items-start gap-2">
        <AlertTriangle
          aria-hidden
          size={18}
          strokeWidth={2}
          className="mt-0.5 shrink-0 text-warning"
        />
        <p className="m-0 text-sm font-semibold leading-relaxed">
          Скопируйте токен сейчас — повторно показан не будет. Закройте панель
          или уйдите со страницы — и токен исчезнет навсегда.
        </p>
      </div>
      <div className="grid grid-cols-[1fr_auto] items-stretch gap-2">
        <code
          aria-label="Значение MCP-токена"
          className="block break-all rounded-md border border-base-300 bg-base-100 px-3 py-2 font-mono text-sm"
        >
          {issued.token}
        </code>
        <Button
          variant="primary"
          icon={
            copied ? (
              <Check aria-hidden size={16} strokeWidth={2} />
            ) : (
              <Copy aria-hidden size={16} strokeWidth={2} />
            )
          }
          onClick={() => {
            void copy();
          }}
        >
          {copied ? "Скопировано" : "Скопировать"}
        </Button>
      </div>
      <div className="flex justify-end">
        <Button onClick={onDismiss}>Я сохранил, скрыть</Button>
      </div>
    </div>
  );
}
