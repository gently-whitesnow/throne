import Link from 'next/link';

export function LocaleSwitch({ currentLocale }: { currentLocale: string }) {
  // Статический экспорт отключает middleware, поэтому as-needed не убирает
  // префикс дефолтной локали: реальные страницы лежат на /ru/ и /en/, а / —
  // лишь редирект-заглушка без RSC-пейлоада. Ссылаемся на явные пути.
  const target = currentLocale === 'ru' ? '/en/' : '/ru/';
  const label = currentLocale === 'ru' ? 'EN' : 'RU';
  return (
    <Link href={target} className="btn btn-secondary" aria-label={`Switch language to ${label}`}>
      {label}
    </Link>
  );
}
