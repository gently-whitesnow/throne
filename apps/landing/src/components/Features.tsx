import { getTranslations } from 'next-intl/server';

type Item = { title: string; text: string };

export async function Features() {
  const t = await getTranslations('features');
  const items = t.raw('items') as Item[];

  return (
    <section className="section" id="features">
      <div className="container">
        <div className="section__kicker">{t('kicker')}</div>
        <h2 className="section__title">{t('title')}</h2>
        <p className="section__body">{t('subtitle')}</p>
        <div className="grid grid-3" style={{ marginTop: 'var(--space-8)' }}>
          {items.map((item) => (
            <div className="card" key={item.title}>
              <h3>{item.title}</h3>
              <p>{item.text}</p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
