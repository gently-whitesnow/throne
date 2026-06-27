import { getTranslations } from 'next-intl/server';

export async function Contacts() {
  const t = await getTranslations('contacts');

  return (
    <section className="section" id="contacts">
      <div className="container">
        <div className="section__kicker">{t('kicker')}</div>
        <h2 className="section__title">{t('title')}</h2>
        <p className="section__body" style={{ marginBottom: 'var(--space-6)' }}>
          {t('body')}
        </p>
        <div style={{ display: 'flex', gap: 'var(--space-3)', flexWrap: 'wrap' }}>
          <a
            className="btn btn-primary"
            href="https://github.com/gently-whitesnow/throne"
            target="_blank"
            rel="noreferrer noopener"
          >
            GitHub · {t('github')}
          </a>
          <a
            className="btn btn-secondary"
            href="https://t.me/gently_whitesnow"
            target="_blank"
            rel="noreferrer noopener"
          >
            Telegram · {t('telegram')}
          </a>
        </div>
      </div>
    </section>
  );
}
