import { useLayoutEffect, useRef } from "react";
import type { TextareaHTMLAttributes } from "react";

type AutoTextareaProps = TextareaHTMLAttributes<HTMLTextAreaElement> & {
  value: string;
};

/**
 * Textarea, которая сама подгоняет высоту под содержимое — оператору не нужно
 * растягивать рамку руками, длинная часть видна целиком при открытии. Высота
 * пересчитывается на каждое изменение value (правка, перезагрузка preview).
 */
export function AutoTextarea({
  value,
  className,
  ...props
}: AutoTextareaProps) {
  const ref = useRef<HTMLTextAreaElement>(null);

  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    el.style.height = "auto";
    el.style.height = `${String(el.scrollHeight)}px`;
  }, [value]);

  return <textarea ref={ref} value={value} className={className} {...props} />;
}
