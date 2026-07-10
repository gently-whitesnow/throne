import { getTranslations } from 'next-intl/server';
import { siteUrl } from '@/lib/seo';

export async function StructuredData({ locale }: { locale: string }) {
  const t = await getTranslations({ locale, namespace: 'meta' });
  const url = siteUrl();

  const data = [
    {
      '@context': 'https://schema.org',
      '@type': 'SoftwareApplication',
      name: 'Throne',
      url,
      applicationCategory: 'DeveloperApplication',
      operatingSystem: 'Linux, macOS, Windows',
      description: t('description'),
      offers: { '@type': 'Offer', price: '0', priceCurrency: 'USD' },
      author: { '@type': 'Person', name: 'gently-whitesnow' },
      sameAs: [
        'https://github.com/gently-whitesnow/throne',
        'https://t.me/throne_whitesnow_tech',
      ],
    },
    {
      '@context': 'https://schema.org',
      '@type': 'Person',
      name: 'gently-whitesnow',
      url: 'https://github.com/gently-whitesnow',
      sameAs: ['https://github.com/gently-whitesnow'],
    },
  ];

  return (
    <script
      type="application/ld+json"
      dangerouslySetInnerHTML={{ __html: JSON.stringify(data) }}
    />
  );
}
