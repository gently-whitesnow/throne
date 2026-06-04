import { useCapabilitiesQuery } from "../api/capabilities-queries";
import type { Capability, CapabilityName } from "./types";

export interface CapabilitiesState {
  capabilities: readonly Capability[];
  isLoading: boolean;
  error: Error | null;
}

const EMPTY: readonly Capability[] = [];

/**
 * Тонкий wrapper над react-query. Источник правды для capability-gating UI:
 * страница `/settings` показывает карточки, intent-страница и retrofit Slice 1
 * включают/прячут секции по `isCapabilityEnabled`.
 */
export function useCapabilities(): CapabilitiesState {
  const query = useCapabilitiesQuery();
  return {
    capabilities: query.data ?? EMPTY,
    isLoading: query.isPending,
    error: query.error instanceof Error ? query.error : null
  };
}

export function selectCapability(
  capabilities: readonly Capability[],
  name: CapabilityName
): Capability | undefined {
  return capabilities.find((c) => c.name === name);
}

/**
 * Capability считается «полностью включённой», только если оператор включил тогл
 * И prerequisite задетектился. Toggle ON без detected — пользователь хотел
 * включить заранее, но UI-секции рендерить нельзя: соответствующая фича просто
 * не заработает (например, `code` CLI отсутствует в контейнере → кнопки скрыты).
 */
export function isCapabilityEnabled(
  capabilities: readonly Capability[],
  name: CapabilityName
): boolean {
  const cap = selectCapability(capabilities, name);
  return cap !== undefined && cap.enabled && cap.detected;
}

export function useCapabilityEnabled(name: CapabilityName): boolean {
  const { capabilities } = useCapabilities();
  return isCapabilityEnabled(capabilities, name);
}
