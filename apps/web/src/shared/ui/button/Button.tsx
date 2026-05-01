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
  const classes = ["button", variant === "primary" ? "button--primary" : ""]
    .filter(Boolean)
    .join(" ");

  return (
    <button
      className={className === undefined ? classes : `${classes} ${className}`}
      type={type}
      {...props}
    >
      {icon}
      {children}
    </button>
  );
}
