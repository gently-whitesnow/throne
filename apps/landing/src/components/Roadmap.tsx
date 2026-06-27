import { getTranslations } from 'next-intl/server';

type Item = { title: string; text: string };

export async function Roadmap() {
  const t = await getTranslations('roadmap');
  const items = t.raw('items') as Item[];

  return (
    <section className="section" id="roadmap">
      <div className="container">
        <div className="section__kicker">{t('kicker')}</div>
        <h2 className="section__title">{t('title')}</h2>
        <p className="section__body" style={{ marginBottom: 'var(--space-8)' }}>
          {t('intro')}
        </p>
        <ol className="roadmap__grid">
          {items.map((item, index) => (
            <li className="card roadmap__item" key={item.title}>
              <span className="roadmap__num" aria-hidden="true">
                {String(index + 1).padStart(2, '0')}
              </span>
              <h3>{item.title}</h3>
              <p>{item.text}</p>
            </li>
          ))}
        </ol>
      </div>
    </section>
  );
}
