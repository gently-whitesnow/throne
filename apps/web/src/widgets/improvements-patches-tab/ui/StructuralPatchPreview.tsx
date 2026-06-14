import type { PromptPartPatchDetail } from "@/entities/prompt-part-patch";

export function StructuralPatchPreview({
  detail
}: {
  detail: PromptPartPatchDetail;
}) {
  if (detail.patch.operation === "delete") {
    return (
      <div className="rounded-md border border-error/30 bg-error/10 px-3 py-2 text-sm text-error">
        Будет удалена user-часть `{detail.patch.target_key}`. Apply сначала
        отвяжет её от всех режимов, затем удалит запись.
      </div>
    );
  }

  const roles = detail.patch.mode_roles ?? [];

  return (
    <div className="overflow-hidden rounded-md border border-base-300 bg-base-100">
      <header className="border-b border-base-300 px-3 py-2 text-xs font-semibold uppercase text-base-content/60">
        Mode roles
      </header>
      {roles.length === 0 ? (
        <p className="m-0 px-3 py-2 text-xs text-base-content/60">
          Ролей нет: часть будет недоступна во всех режимах.
        </p>
      ) : (
        <table className="w-full text-left text-sm">
          <thead className="bg-base-200 text-xs uppercase text-base-content/60">
            <tr>
              <th className="px-3 py-2 font-semibold">Mode</th>
              <th className="px-3 py-2 font-semibold">Role</th>
              <th className="px-3 py-2 font-semibold">Order</th>
            </tr>
          </thead>
          <tbody>
            {roles.map((role) => (
              <tr
                key={`${role.mode}-${role.role}-${String(role.order)}`}
                className="border-t border-base-300"
              >
                <td className="px-3 py-2 font-mono text-xs">{role.mode}</td>
                <td className="px-3 py-2 font-mono text-xs">{role.role}</td>
                <td className="px-3 py-2 font-mono text-xs">
                  {String(role.order)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
