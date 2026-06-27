import Link from 'next/link';

export function LocaleSwitch({ currentLocale }: { currentLocale: string }) {
  const target = currentLocale === 'ru' ? '/en' : '/';
  const label = currentLocale === 'ru' ? 'EN' : 'RU';
  return (
    <Link href={target} className="btn btn-secondary" aria-label={`Switch language to ${label}`}>
      {label}
    </Link>
  );
}
