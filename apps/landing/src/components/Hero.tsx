import { getTranslations } from 'next-intl/server';

export async function Hero() {
  const t = await getTranslations('hero');
  return (
    <section
      className="section"
      style={{ paddingTop: 'var(--space-16)', paddingBottom: 'var(--space-16)' }}
    >
      <div className="container">
        <h1
          style={{
            fontSize: 'clamp(2rem, 4vw + 1rem, 3.25rem)',
            fontWeight: 600,
            lineHeight: 1.1,
            letterSpacing: '-0.02em',
            margin: 'var(--space-4) 0 var(--space-5)',
            maxWidth: '880px',
          }}
        >
          {t('title')}
        </h1>
        <p
          className="section__body"
          style={{ fontSize: '1.0625rem', marginBottom: 'var(--space-6)' }}
        >
          {t('subtitle')}
        </p>
        <div style={{ display: 'flex', gap: 'var(--space-3)', flexWrap: 'wrap' }}>
          <a className="btn btn-primary" href="#connect">
            {t('ctaPrimary')}
          </a>
          <a
            className="btn btn-secondary"
            href="https://github.com/gently-whitesnow/throne"
            target="_blank"
            rel="noreferrer noopener"
          >
            {t('ctaSecondary')}
          </a>
        </div>
      </div>
    </section>
  );
}
