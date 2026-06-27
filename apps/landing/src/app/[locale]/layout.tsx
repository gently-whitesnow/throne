import type { Metadata } from 'next';
import type { ReactNode } from 'react';
import { NextIntlClientProvider, hasLocale } from 'next-intl';
import { getTranslations, setRequestLocale } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { Mona_Sans } from 'next/font/google';
import { routing } from '@/i18n/routing';
import { SiteHeader } from '@/components/SiteHeader';
import { SiteFooter } from '@/components/SiteFooter';
import { StructuredData } from '@/components/StructuredData';
import { YandexMetrika } from '@/components/YandexMetrika';
import { siteUrl, alternatesFor } from '@/lib/seo';
import '../globals.css';

const monaSans = Mona_Sans({
  subsets: ['latin', 'latin-ext'],
  display: 'swap',
  variable: '--font-mona-sans',
});

export function generateStaticParams() {
  return routing.locales.map((locale) => ({ locale }));
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ locale: string }>;
}): Promise<Metadata> {
  const { locale } = await params;
  if (!hasLocale(routing.locales, locale)) return {};

  const t = await getTranslations({ locale, namespace: 'meta' });
  const alternates = alternatesFor('/');

  return {
    metadataBase: new URL(siteUrl()),
    title: t('title'),
    description: t('description'),
    alternates,
    openGraph: {
      type: 'website',
      title: t('title'),
      description: t('description'),
      url: alternates.canonical,
      siteName: 'Throne',
      locale: locale === 'ru' ? 'ru_RU' : 'en_US',
    },
    twitter: {
      card: 'summary_large_image',
      title: t('title'),
      description: t('description'),
    },
    robots: { index: true, follow: true },
    icons: {
      icon: [
        { url: '/favicon-light.png', media: '(prefers-color-scheme: light)', type: 'image/png' },
        { url: '/favicon-dark.png', media: '(prefers-color-scheme: dark)', type: 'image/png' },
      ],
      apple: '/apple-icon.png',
    },
  };
}

export default async function LocaleLayout({
  children,
  params,
}: {
  children: ReactNode;
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  if (!hasLocale(routing.locales, locale)) notFound();
  setRequestLocale(locale);

  return (
    <html lang={locale} className={monaSans.variable} data-theme="light" suppressHydrationWarning>
      <head>
        <script
          // Применяем сохранённую тему до гидрации, чтобы избежать вспышки фона.
          dangerouslySetInnerHTML={{
            __html: `(function(){try{var t=localStorage.getItem('throne-site-theme');if(t==='dark'||t==='light'){document.documentElement.setAttribute('data-theme',t);}}catch(e){}})();`,
          }}
        />
      </head>
      <body>
        <NextIntlClientProvider>
          <StructuredData locale={locale} />
          <SiteHeader />
          <main id="main">{children}</main>
          <SiteFooter />
        </NextIntlClientProvider>
        <YandexMetrika />
      </body>
    </html>
  );
}
