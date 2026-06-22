import { AlertCircle, Bot } from "lucide-react";

import {
  useTerminalVendorCatalogQuery,
  type TerminalVendorMetadata
} from "@/entities/terminal-setting";

type LoginStatus = TerminalVendorMetadata["login_status"];

interface BadgeMeta {
  label: string;
  className: string;
}

/** Light-first semantic tokens per login probe outcome. */
const BADGE: Record<LoginStatus, BadgeMeta> = {
  ready: { label: "Залогинен", className: "bg-success/15 text-success" },
  logged_out: {
    label: "Не залогинен",
    className: "bg-warning/20 text-warning"
  },
  missing: { label: "CLI не найден", className: "bg-error/10 text-error" },
  in_development: {
    label: "В разработке",
    className: "bg-base-300 text-base-content/70"
  }
};

/**
 * Settings → «Агенты»: информационная карточка со всеми вендорами каталога и их
 * login-статусом. Без тоглов — только статус. Несамовыбираемые вендоры (opencode
 * «в разработке») приглушены, но показаны, чтобы оператор видел задел.
 */
export function AgentVendorsCard() {
  const query = useTerminalVendorCatalogQuery();

  if (query.isError) {
    return (
      <div
        role="alert"
        className="flex items-start gap-2 rounded-lg border border-error/30 bg-error/10 px-4 py-3 text-sm text-error"
      >
        <AlertCircle aria-hidden size={16} strokeWidth={2} className="mt-0.5" />
        <span>Не удалось загрузить список агентов.</span>
      </div>
    );
  }

  if (query.isLoading) {
    return (
      <p className="m-0 text-sm text-base-content/60">Загружаем агентов…</p>
    );
  }

  const vendors = query.data?.vendors ?? [];

  return (
    <section
      data-testid="agent-vendors-card"
      className="flex flex-col gap-3 rounded-lg border border-base-300 bg-base-100 p-4"
    >
      <header className="flex min-w-0 items-start gap-3">
        <span
          aria-hidden
          className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary"
        >
          <Bot size={18} strokeWidth={2} />
        </span>
        <div className="flex min-w-0 flex-col gap-1">
          <h3 className="m-0 text-base font-semibold leading-tight">
            Агенты и логин
          </h3>
          <p className="m-0 text-sm leading-relaxed text-base-content/70">
            Статус логина CLI каждого вендора. Залогиньтесь хотя бы в одного,
            чтобы запускать сессии.
          </p>
        </div>
      </header>

      <ul className="m-0 flex list-none flex-col gap-2 p-0">
        {vendors.map((vendor) => (
          <VendorRow key={vendor.vendor} vendor={vendor} />
        ))}
      </ul>
    </section>
  );
}

function VendorRow({ vendor }: { vendor: TerminalVendorMetadata }) {
  const badge = BADGE[vendor.login_status];
  return (
    <li
      data-testid={`agent-vendor-${vendor.vendor}`}
      data-login-status={vendor.login_status}
      className={
        vendor.selectable
          ? "flex items-center justify-between gap-3 rounded-lg border border-base-300 px-3 py-2"
          : "flex items-center justify-between gap-3 rounded-lg border border-base-300 px-3 py-2 opacity-60"
      }
    >
      <div className="flex min-w-0 flex-col gap-0.5">
        <span className="text-sm font-semibold leading-tight">
          {vendor.label}
        </span>
        {vendor.login_detail !== null && vendor.login_detail !== undefined ? (
          <span className="text-xs leading-relaxed text-base-content/60">
            {vendor.login_detail}
          </span>
        ) : null}
      </div>
      <span
        className={`inline-flex shrink-0 items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${badge.className}`}
      >
        {badge.label}
      </span>
    </li>
  );
}
