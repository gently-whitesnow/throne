import type { ButtonHTMLAttributes, ReactNode } from "react";

type ButtonVariant = "default" | "primary";

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  icon?: ReactNode;
  variant?: ButtonVariant;
};

export function Button({
  children,
  className,
  icon,
  variant = "default",
  type = "button",
  ...props
}: ButtonProps) {
  const press =
    "transition-[color,background-color,box-shadow,scale] active:scale-[0.96]";
  const base =
    variant === "primary"
      ? `btn btn-sm btn-primary ${press}`
      : `btn btn-sm btn-soft ${press}`;
  const merged = className ? `${base} ${className}` : base;

  return (
    <button className={merged} type={type} {...props}>
      {icon}
      {children}
    </button>
  );
}
