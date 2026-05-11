interface TagBadgeProps {
  name: string;
}

export function TagBadge({ name }: TagBadgeProps) {
  return (
    <span className="badge badge-sm badge-outline border-base-300 text-base-content/80">
      #{name}
    </span>
  );
}
