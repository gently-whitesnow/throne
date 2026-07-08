import {
  changeRequestRefLabel,
  repositoryFullName,
  type RepositoryBinding
} from "@/entities/repository-binding";

interface ReviewTargetSelectProps {
  targets: readonly RepositoryBinding[];
  value: string;
  disabled: boolean;
  onChange: (bindingId: string) => void;
}

export function ReviewTargetSelect({
  targets,
  value,
  disabled,
  onChange
}: ReviewTargetSelectProps) {
  if (targets.length < 2) return null;

  return (
    <label className="flex items-center gap-2 text-xs text-base-content/70">
      <span>PR/MR</span>
      <select
        aria-label="Pull request для review"
        data-testid="agent-terminal-review-binding"
        className="select select-sm select-bordered max-w-72"
        value={value}
        disabled={disabled}
        onChange={(event) => {
          onChange(event.target.value);
        }}
      >
        {targets.map((binding) => (
          <option key={binding.id} value={binding.id}>
            {optionLabel(binding)}
          </option>
        ))}
      </select>
    </label>
  );
}

function optionLabel(binding: RepositoryBinding): string {
  const ref = changeRequestRefLabel(binding) ?? "PR/MR";
  return `${ref} · ${repositoryFullName(binding)}`;
}
