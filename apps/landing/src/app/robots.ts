import type { MetadataRoute } from 'next';
import { siteUrl } from '@/lib/seo';

// output: 'export' — генерируется статически на этапе сборки.
export const dynamic = 'force-static';

export default function robots(): MetadataRoute.Robots {
  const base = siteUrl();
  return {
    rules: [{ userAgent: '*', allow: '/' }],
    sitemap: `${base}/sitemap.xml`,
    host: base,
  };
}
