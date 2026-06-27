import type { MetadataRoute } from 'next';
import { routing } from '@/i18n/routing';
import { siteUrl, alternatesFor } from '@/lib/seo';

// output: 'export' — генерируется статически на этапе сборки.
export const dynamic = 'force-static';

export default function sitemap(): MetadataRoute.Sitemap {
  const base = siteUrl();
  const alts = alternatesFor('/');

  return routing.locales.map((locale) => ({
    url: alts.languages[locale] ?? base,
    lastModified: new Date(),
    changeFrequency: 'monthly',
    priority: locale === routing.defaultLocale ? 1 : 0.8,
    alternates: { languages: alts.languages },
  }));
}
