import {
  AlertCircle,
  CheckCircle2,
  Cpu,
  RefreshCw,
  XCircle
} from "lucide-react";

import {
  localModelStatusMeta,
  useLocalModelCatalog,
  type LocalModelCatalog
} from "@/entities/local-model-catalog";
import { Button } from "@/shared/ui";

/**
 * Settings → «Локальные модели».
 *
 * Показывает адрес `Throne:LocalModel:BaseUrl` и результат discovery через
 * `GET {BaseUrl}/v1/models`: статус-pill семантическими токенами плюс список
 * найденных моделей. Каждое состояние discovery рендерится как состояние, а не
 * как краш — endpoint пустой/недоступный остаётся понятной карточкой.
 */
export function LocalModelCard() {
  const { catalog, isLoading, error, refresh } = useLocalModelCatalog();

  return (
    <section
      aria-label="Локальные модели"
      data-testid="local-model-card"
      className="flex flex-col gap-4 rounded-lg border border-base-300 bg-base-100 p-5"
    >
      <header className="flex items-start justify-between gap-3">
        <div className="flex items-start gap-3">
          <span
            aria-hidden
            className="inline-flex h-9 w-9 items-center justify-center rounded-md bg-primary/10 text-primary"
          >
            <Cpu size={18} strokeWidth={2} />
          </span>
          <div className="flex flex-col gap-1">
            <h3 className="m-0 text-base font-bold leading-tight">
              Локальные модели
            </h3>
            <p className="m-0 max-w-[60ch] text-sm leading-relaxed text-base-content/70">
              Модели локального OpenAI-совместимого endpoint (
              <code className="font-mono">Throne:LocalModel:BaseUrl</code>),
              прочитанные через <code className="font-mono">/v1/models</code>.
            </p>
          </div>
        </div>
        <Button
          aria-label="Перепроверить локальные модели"
          icon={
            <RefreshCw
              aria-hidden
              size={16}
              strokeWidth={2}
              className={isLoading ? "animate-spin" : undefined}
            />
          }
          onClick={refresh}
        >
          {isLoading ? "Проверяем…" : "Проверить"}
        </Button>
      </header>

      <CatalogBody isLoading={isLoading} error={error} catalog={catalog} />
    </section>
  );
}

interface CatalogBodyProps {
  isLoading: boolean;
  error: Error | null;
  catalog: LocalModelCatalog | null;
}

function CatalogBody({ isLoading, error, catalog }: CatalogBodyProps) {
  if (error) {
    return (
      <p
        role="alert"
        className="m-0 flex items-start gap-2 rounded-md border border-error/30 bg-error/10 px-3 py-2 text-sm text-error"
      >
        <AlertCircle aria-hidden size={16} strokeWidth={2} className="mt-0.5" />
        <span>Не удалось получить статус discovery: {error.message}</span>
      </p>
    );
  }

  if (!catalog && isLoading) {
    return (
      <p className="m-0 text-sm text-base-content/60">Загружаем статус…</p>
    );
  }

  if (!catalog) {
    return <p className="m-0 text-sm text-base-content/60">Нет данных.</p>;
  }

  const meta = localModelStatusMeta[catalog.status];
  const healthy = catalog.status === "ready";

  return (
    <div className="flex flex-col gap-3 rounded-md border border-base-300 bg-base-200/40 p-3">
      <div className="flex items-start justify-between gap-3">
        <p className="m-0 text-xs text-base-content/60">
          {catalog.base_url ? (
            <code className="font-mono">{catalog.base_url}</code>
          ) : (
            "BaseUrl не задан"
          )}
        </p>
        <span
          data-testid="local-model-status-pill"
          className={`inline-flex w-fit items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold ${meta.className}`}
        >
          {healthy ? (
            <CheckCircle2 aria-hidden size={14} strokeWidth={2.25} />
          ) : catalog.status === "unreachable" ? (
            <XCircle aria-hidden size={14} strokeWidth={2.25} />
          ) : (
            <AlertCircle aria-hidden size={14} strokeWidth={2.25} />
          )}
          {meta.label}
        </span>
      </div>

      <CatalogState catalog={catalog} />
    </div>
  );
}

function CatalogState({ catalog }: { catalog: LocalModelCatalog }) {
  switch (catalog.status) {
    case "ready":
      return (
        <ul
          data-testid="local-model-list"
          className="m-0 flex list-none flex-wrap gap-1.5 p-0"
        >
          {catalog.models.map((id) => (
            <li
              key={id}
              className="rounded border border-base-300 bg-base-100 px-1.5 py-0.5 font-mono text-xs"
            >
              {id}
            </li>
          ))}
        </ul>
      );
    case "unreachable":
      return (
        <p className="m-0 text-sm text-base-content/70">
          Endpoint недоступен{catalog.error ? `: ${catalog.error}` : "."}
        </p>
      );
    case "empty":
      return (
        <p className="m-0 text-sm text-base-content/70">
          Endpoint ответил, но не объявил ни одной модели.
        </p>
      );
    case "not_configured":
    default:
      return (
        <p className="m-0 text-sm text-base-content/70">
          Задайте <code className="font-mono">Throne:LocalModel:BaseUrl</code> в
          конфигурации, чтобы Throne читал модели из{" "}
          <code className="font-mono">/v1/models</code>.
        </p>
      );
  }
}
