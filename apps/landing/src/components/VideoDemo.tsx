import { getTranslations } from 'next-intl/server';

const VIDEO_URL = process.env.NEXT_PUBLIC_DEMO_VIDEO_URL;

export async function VideoDemo() {
  const t = await getTranslations('video');

  return (
    <section className="section" id="demo">
      <div className="container">
        <div className="section__kicker">{t('kicker')}</div>
        <h2 className="section__title">{t('title')}</h2>
        <p className="section__body" style={{ marginBottom: 'var(--space-6)' }}>
          {t('body')}
        </p>
        <div
          style={{
            position: 'relative',
            width: '100%',
            aspectRatio: '16 / 9',
            background: 'var(--color-neutral-soft)',
            border: '1px solid var(--color-border)',
            borderRadius: 'var(--rounded-lg)',
            overflow: 'hidden',
          }}
        >
          {VIDEO_URL ? (
            <iframe
              src={VIDEO_URL}
              title={t('placeholder')}
              loading="lazy"
              allow="accelerometer; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
              allowFullScreen
              style={{
                position: 'absolute',
                inset: 0,
                width: '100%',
                height: '100%',
                border: 0,
              }}
            />
          ) : (
            <div
              role="presentation"
              style={{
                position: 'absolute',
                inset: 0,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: 'var(--color-text-subtle)',
                fontSize: '0.9375rem',
                letterSpacing: '0.04em',
              }}
            >
              {t('placeholder')}
            </div>
          )}
        </div>
      </div>
    </section>
  );
}
