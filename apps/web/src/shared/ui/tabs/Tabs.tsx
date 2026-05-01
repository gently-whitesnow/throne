export interface TabItem<TValue extends string> {
  value: TValue;
  label: string;
}

export interface TabsProps<TValue extends string> {
  items: readonly TabItem<TValue>[];
  value: TValue;
  onChange: (value: TValue) => void;
  ariaLabel?: string;
}

export function Tabs<TValue extends string>({
  items,
  value,
  onChange,
  ariaLabel
}: TabsProps<TValue>) {
  return (
    <div className="tabs" role="tablist" aria-label={ariaLabel}>
      {items.map((item) => {
        const active = item.value === value;
        return (
          <button
            key={item.value}
            type="button"
            role="tab"
            aria-selected={active}
            className={active ? "tabs__item tabs__item--active" : "tabs__item"}
            onClick={() => {
              onChange(item.value);
            }}
          >
            {item.label}
          </button>
        );
      })}
    </div>
  );
}
