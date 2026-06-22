import { AlertCircle, TerminalSquare } from "lucide-react";

import {
  useSetDefaultTerminalVendor,
  useTerminalSettingsQuery,
  useTerminalVendorCatalogQuery
} from "@/entities/terminal-setting";

/**
 * Settings → «Терминал»: выбор дефолтного вендора агента (claude | codex) для
 * новых сессий встроенного терминала. Модель и усилие — нативные дефолты
 * вендора и в настройках не выносятся (выбираются per-сессия на странице интента).
 */
export function TerminalDefaultsCard() {
  const query = useTerminalSettingsQuery();
  const catalogQuery = useTerminalVendorCatalogQuery();
  const mutation = useSetDefaultTerminalVendor();

  if (query.error !== null) {
    return (
      <div
        role="alert"
        className="flex items-start gap-2 rounded-lg border border-error/30 bg-error/10 px-4 py-3 text-sm text-error"
      >
        <AlertCircle aria-hidden size={16} strokeWidth={2} className="mt-0.5" />
        <span>
          Не удалось загрузить настройки терминала: {query.error.message}
        </span>
      </div>
    );
  }

  const current = query.data?.default_vendor;
  // Только selectable вендоры — дефолтным запуском нельзя выбрать «в разработке».
  const vendors = (catalogQuery.data?.vendors ?? []).filter(
    (v) => v.selectable
  );
  const selectDisabled =
    query.isLoading ||
    catalogQuery.isLoading ||
    catalogQuery.isError ||
    mutation.isPending;

  return (
    <section
      data-testid="terminal-defaults-card"
      className="flex flex-col gap-3 rounded-lg border border-base-300 bg-base-100 p-4"
    >
      <header className="flex min-w-0 items-start gap-3">
        <span
          aria-hidden
          className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary"
        >
          <TerminalSquare size={18} strokeWidth={2} />
        </span>
        <div className="flex min-w-0 flex-col gap-1">
          <h3 className="m-0 text-base font-semibold leading-tight">
            Агент по умолчанию
          </h3>
          <p className="m-0 text-sm leading-relaxed text-base-content/70">
            Каким агентом предзаполнять запуск новых сессий терминала. Модель и
            усилие выбираются на странице интента.
          </p>
        </div>
      </header>

      <label className="flex items-center gap-2 text-sm text-base-content/70">
        <span>Вендор</span>
        <select
          aria-label="Вендор агента по умолчанию"
          data-testid="terminal-default-vendor"
          className="select select-sm select-bordered"
          value={current ?? ""}
          disabled={selectDisabled}
          onChange={(event) => {
            mutation.mutate(event.target.value);
          }}
        >
          {current === undefined ? <option value="" disabled hidden /> : null}
          {vendors.map((v) => (
            <option key={v.vendor} value={v.vendor}>
              {v.label}
            </option>
          ))}
        </select>
        {mutation.isPending ? (
          <span className="text-xs text-base-content/50">Сохраняем…</span>
        ) : null}
      </label>

      {catalogQuery.isError ? (
        <p role="alert" className="m-0 text-xs text-error">
          Не удалось загрузить список агентов.
        </p>
      ) : null}

      {mutation.error !== null ? (
        <p role="alert" className="m-0 text-xs text-error">
          Не удалось сохранить вендор по умолчанию.
        </p>
      ) : null}
    </section>
  );
}
