interface TagBadgeProps {
  name: string;
}

export function TagBadge({ name }: TagBadgeProps) {
  return <span className="tag-chip">#{name}</span>;
}
