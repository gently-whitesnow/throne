import { routing } from '@/i18n/routing';

export function siteUrl(): string {
  return process.env.NEXT_PUBLIC_SITE_URL ?? 'https://throne.example';
}

function localePath(locale: string, path: string): string {
  const normalized = path === '/' ? '' : path.replace(/^\//, '/');
  if (locale === routing.defaultLocale) return normalized || '/';
  return `/${locale}${normalized}`;
}

export function alternatesFor(path: string) {
  const base = siteUrl();
  const languages: Record<string, string> = {};
  for (const locale of routing.locales) {
    languages[locale] = `${base}${localePath(locale, path)}`;
  }
  languages['x-default'] = `${base}${localePath(routing.defaultLocale, path)}`;
  return {
    canonical: `${base}${localePath(routing.defaultLocale, path)}`,
    languages,
  };
}
