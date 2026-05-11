const RTF = new Intl.RelativeTimeFormat("ru", { numeric: "auto" });

const DIVISIONS: { amount: number; unit: Intl.RelativeTimeFormatUnit }[] = [
  { amount: 60, unit: "second" },
  { amount: 60, unit: "minute" },
  { amount: 24, unit: "hour" },
  { amount: 7, unit: "day" },
  { amount: 4.34524, unit: "week" },
  { amount: 12, unit: "month" },
  { amount: Number.POSITIVE_INFINITY, unit: "year" }
];

export function formatRelativeTime(
  input: string | Date,
  now: Date = new Date()
): string {
  const date = typeof input === "string" ? new Date(input) : input;
  let diff = (date.getTime() - now.getTime()) / 1000;
  for (const { amount, unit } of DIVISIONS) {
    if (Math.abs(diff) < amount) {
      return RTF.format(Math.round(diff), unit);
    }
    diff /= amount;
  }
  return date.toLocaleDateString("ru");
}

export function formatDateLabel(input: string | Date): string {
  const date = typeof input === "string" ? new Date(input) : input;
  const today = new Date();
  const yesterday = new Date(today);
  yesterday.setDate(today.getDate() - 1);

  const sameDay = (a: Date, b: Date) =>
    a.getFullYear() === b.getFullYear() &&
    a.getMonth() === b.getMonth() &&
    a.getDate() === b.getDate();

  if (sameDay(date, today)) return "Сегодня";
  if (sameDay(date, yesterday)) return "Вчера";
  return date.toLocaleDateString("ru", {
    day: "numeric",
    month: "long",
    year: date.getFullYear() === today.getFullYear() ? undefined : "numeric"
  });
}

export function dayKey(input: string | Date): string {
  const date = typeof input === "string" ? new Date(input) : input;
  return `${String(date.getFullYear())}-${String(date.getMonth())}-${String(date.getDate())}`;
}
