import { ImageResponse } from 'next/og';
import { getTranslations } from 'next-intl/server';
import { hasLocale } from 'next-intl';
import { routing } from '@/i18n/routing';

export const size = { width: 1200, height: 630 };
export const contentType = 'image/png';
export const alt = 'Throne';

// output: 'export' — рендерим картинку статически на сборке, по одной на локаль.
export const dynamic = 'force-static';
export function generateStaticParams() {
  return routing.locales.map((locale) => ({ locale }));
}

export default async function OgImage({ params }: { params: { locale: string } }) {
  const locale = hasLocale(routing.locales, params.locale) ? params.locale : routing.defaultLocale;
  const t = await getTranslations({ locale, namespace: 'meta' });
  const tagline =
    locale === 'ru'
      ? 'Локальное приложение агентной инженерии вокруг Intent'
      : 'A local app for agentic engineering, built around the Intent';

  return new ImageResponse(
    (
      <div
        style={{
          width: '100%',
          height: '100%',
          background: '#FFFFFF',
          color: '#202531',
          padding: 80,
          display: 'flex',
          flexDirection: 'column',
          justifyContent: 'space-between',
          fontFamily: 'sans-serif',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
          <div style={{ width: 36, height: 36, borderRadius: 8, background: '#3563F6' }} />
          <div style={{ fontSize: 36, fontWeight: 600 }}>throne</div>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <div
            style={{
              fontSize: 22,
              color: '#3563F6',
              fontWeight: 600,
              letterSpacing: '0.04em',
              textTransform: 'uppercase',
            }}
          >
            {tagline}
          </div>
          <div style={{ fontSize: 56, fontWeight: 600, lineHeight: 1.1 }}>{t('title')}</div>
        </div>
        <div style={{ fontSize: 22, color: '#7B8394' }}>
          github.com/gently-whitesnow/throne
        </div>
      </div>
    ),
    { ...size },
  );
}
