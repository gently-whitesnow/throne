import { AlertCircle, CheckCircle2, Code2, XCircle } from "lucide-react";

import {
  useCapabilities,
  useSetSelectedProvider,
  type Capability,
  type CapabilityName,
  type CapabilityProvider
} from "@/entities/capability";

/**
 * Settings → «Возможности».
 *
 * Renders one section per carrier capability (today only `open_in_ide`). The
 * radio group picks the IDE provider; clearing the radio sends `null` and the
 * server falls back to «whichever single provider is detected».
 */
export interface CapabilitiesCardProps {
  /** Restrict rendering to a subset of capabilities (in given order). */
  only?: CapabilityName[];
}

export function CapabilitiesCard({ only }: CapabilitiesCardProps = {}) {
  const { capabilities, isLoading, error } = useCapabilities();

  if (error !== null) {
    return (
      <div
        role="alert"
        className="flex items-start gap-2 rounded-lg border border-error/30 bg-error/10 px-4 py-3 text-sm text-error"
      >
        <AlertCircle aria-hidden size={16} strokeWidth={2} className="mt-0.5" />
        <span>Не удалось загрузить список возможностей: {error.message}</span>
      </div>
    );
  }

  if (isLoading && capabilities.length === 0) {
    return (
      <p className="m-0 text-sm text-base-content/60">Загружаем возможности…</p>
    );
  }

  if (capabilities.length === 0) {
    return (
      <p className="m-0 text-sm text-base-content/60">
        На сервере нет ни одной возможности.
      </p>
    );
  }

  const visible =
    only === undefined
      ? capabilities
      : only
          .map((name) =>
            capabilities.find((c) => (c.name as string) === (name as string))
          )
          .filter((c): c is Capability => c !== undefined);

  return (
    <div className="flex flex-col gap-3" data-testid="capabilities-list">
      {visible.map((capability) => (
        <CapabilitySection key={capability.name} capability={capability} />
      ))}
    </div>
  );
}

function CapabilitySection({ capability }: { capability: Capability }) {
  const mutation = useSetSelectedProvider();
  const selected = capability.selected_provider ?? null;

  const handleChange = (next: string | null) => {
    mutation.mutate({ name: capability.name, selectedProvider: next });
  };

  return (
    <section
      data-testid={`capability-card-${capability.name}`}
      className="flex flex-col gap-3 rounded-lg border border-base-300 bg-base-100 p-4"
    >
      <header className="flex min-w-0 items-start gap-3">
        <span
          aria-hidden
          className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary"
        >
          <Code2 size={18} strokeWidth={2} />
        </span>
        <div className="flex min-w-0 flex-col gap-1">
          <h3 className="m-0 text-base font-semibold leading-tight">
            {capability.title}
          </h3>
          <p className="m-0 max-w-[60ch] text-sm leading-relaxed text-base-content/70">
            {capability.description}
          </p>
        </div>
      </header>

      <ProviderRadioGroup
        capability={capability.name}
        providers={capability.providers}
        selected={selected}
        disabled={mutation.isPending}
        onChange={handleChange}
      />

      {mutation.error instanceof Error ? (
        <p
          role="alert"
          className="m-0 text-xs text-error"
          data-testid={`capability-error-${capability.name}`}
        >
          Не удалось сохранить выбор провайдера: {mutation.error.message}
        </p>
      ) : null}
    </section>
  );
}

interface ProviderRadioGroupProps {
  capability: CapabilityName;
  providers: readonly CapabilityProvider[];
  selected: string | null;
  disabled: boolean;
  onChange: (next: string | null) => void;
}

function ProviderRadioGroup({
  capability,
  providers,
  selected,
  disabled,
  onChange
}: ProviderRadioGroupProps) {
  const groupName = `capability-provider-${capability}`;
  return (
    <fieldset
      className="m-0 flex flex-col gap-2 p-0"
      data-testid={`capability-providers-${capability}`}
    >
      <legend className="sr-only">Выберите провайдера</legend>
      <ProviderRadioOption
        groupName={groupName}
        value=""
        checked={selected === null}
        disabled={disabled}
        title="Авто"
        description="Использовать единственный обнаруженный провайдер."
        onSelect={() => {
          onChange(null);
        }}
      />
      {providers.map((provider) => (
        <ProviderRadioOption
          key={provider.name}
          groupName={groupName}
          value={provider.name}
          checked={selected === provider.name}
          disabled={disabled}
          title={provider.title}
          provider={provider}
          onSelect={() => {
            onChange(provider.name);
          }}
        />
      ))}
    </fieldset>
  );
}

interface ProviderRadioOptionProps {
  groupName: string;
  value: string;
  checked: boolean;
  disabled: boolean;
  title: string;
  description?: string;
  provider?: CapabilityProvider;
  onSelect: () => void;
}

function ProviderRadioOption({
  groupName,
  value,
  checked,
  disabled,
  title,
  description,
  provider,
  onSelect
}: ProviderRadioOptionProps) {
  const id = `${groupName}-${value === "" ? "auto" : value}`;
  return (
    <label
      htmlFor={id}
      data-testid={`${groupName}-option-${value === "" ? "auto" : value}`}
      data-checked={checked}
      className={
        checked
          ? "flex cursor-pointer items-start gap-3 rounded-md border border-primary/60 bg-primary/5 px-3 py-2"
          : "flex cursor-pointer items-start gap-3 rounded-md border border-base-300 px-3 py-2 hover:bg-base-200/60"
      }
    >
      <input
        id={id}
        name={groupName}
        type="radio"
        value={value}
        className="radio radio-sm radio-primary mt-0.5"
        checked={checked}
        disabled={disabled}
        onChange={onSelect}
      />
      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-sm font-semibold leading-tight">{title}</span>
          {provider !== undefined ? (
            <DetectedPill detected={provider.detected} />
          ) : null}
        </div>
        {provider !== undefined ? (
          <ProviderDetail provider={provider} />
        ) : description !== undefined ? (
          <p className="m-0 text-xs leading-relaxed text-base-content/60">
            {description}
          </p>
        ) : null}
      </div>
    </label>
  );
}

function DetectedPill({ detected }: { detected: boolean }) {
  return (
    <span
      data-detected={detected}
      className={
        detected
          ? "inline-flex items-center gap-1 rounded-full bg-success/15 px-2 py-0.5 text-[11px] font-semibold text-success"
          : "inline-flex items-center gap-1 rounded-full bg-error/10 px-2 py-0.5 text-[11px] font-semibold text-error"
      }
    >
      {detected ? (
        <CheckCircle2 aria-hidden size={11} strokeWidth={2.25} />
      ) : (
        <XCircle aria-hidden size={11} strokeWidth={2.25} />
      )}
      {detected ? "Найден" : "Не найден"}
    </span>
  );
}

function ProviderDetail({ provider }: { provider: CapabilityProvider }) {
  const hasDetail =
    provider.detection_detail != null && provider.detection_detail.length > 0;
  const hasHint =
    provider.prerequisite_hint != null && provider.prerequisite_hint.length > 0;
  if (!hasDetail && !hasHint) return null;
  return (
    <p className="m-0 text-xs leading-relaxed text-base-content/60">
      {hasDetail ? provider.detection_detail : provider.prerequisite_hint}
    </p>
  );
}
