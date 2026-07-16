import {
  PROMPT_PART_MODE_LABELS,
  PROMPT_PART_ROLE_LABELS,
  PROMPT_PART_UI_ROLE_OPTIONS,
  useSetPromptPartRoles,
  type PromptPartListItem,
  type PromptPartMode,
  type PromptPartUiRole
} from "@/entities/prompt-part";
import { errorMessage } from "@/shared/lib";

import { mergeRoleForMode } from "../model/composition";

interface RoleSelectProps {
  part: PromptPartListItem;
  mode: PromptPartMode;
  /** Order to assign when attaching the part to this mode for the first time. */
  orderForNew: number;
  onError?: (message: string | null) => void;
}

/**
 * Inline role picker for an optional part within one mode. Every change rebuilds
 * the whole mode_roles list (preserving the other modes) and issues a
 * whole-replace PUT via setPromptPartRoles.
 */
export function RoleSelect({
  part,
  mode,
  orderForNew,
  onError
}: RoleSelectProps) {
  const setRoles = useSetPromptPartRoles();
  const current: PromptPartUiRole =
    (part.mode_roles.find((r) => r.mode === mode)?.role as
      PromptPartUiRole | undefined) ?? "none";

  const handleChange = (nextRole: PromptPartUiRole) => {
    onError?.(null);
    const modeRoles = mergeRoleForMode(
      part.mode_roles,
      mode,
      nextRole,
      orderForNew
    );
    setRoles.mutate(
      { id: part.id, modeRoles },
      {
        onError: (err) => {
          onError?.(errorMessage(err, { base: "Не удалось изменить роль" }));
        }
      }
    );
  };

  return (
    <select
      className="select select-bordered select-xs font-medium"
      value={current}
      disabled={setRoles.isPending}
      aria-label={`Роль в режиме «${PROMPT_PART_MODE_LABELS[mode]}»`}
      onChange={(e) => {
        handleChange(e.target.value as PromptPartUiRole);
      }}
    >
      {PROMPT_PART_UI_ROLE_OPTIONS.map((role) => (
        <option key={role} value={role}>
          {PROMPT_PART_ROLE_LABELS[role]}
        </option>
      ))}
    </select>
  );
}
