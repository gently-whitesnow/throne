import { getTranslations } from 'next-intl/server';

export async function Theory() {
  const t = await getTranslations('theory');

  return (
    <section className="section theory" id="theory" aria-labelledby="theory-title">
      <div className="container theory__container">
        <div className="theory__grid">
          <div className="theory__text">
            <div className="section__kicker">{t('kicker')}</div>
            <h2 className="section__title" id="theory-title">
              {t('title')}
            </h2>
            <p className="section__body theory__body">{t('body')}</p>
            <p className="theory__tagline">{t('tagline')}</p>
          </div>

          <svg
            className="theory__diagram"
            viewBox="0 0 600 420"
            role="img"
            aria-label={t('title')}
            focusable="false"
          >
            <defs>
              <marker
                id="theory-arrow"
                viewBox="0 0 10 10"
                refX="9"
                refY="5"
                markerWidth="7"
                markerHeight="7"
                orient="auto-start-reverse"
              >
                <path d="M0 0 L10 5 L0 10 Z" fill="currentColor" />
              </marker>
            </defs>

            <g className="theory__edges" markerEnd="url(#theory-arrow)">
              <path d="M210 80 L390 80" />
              <path d="M500 130 Q 590 240 410 320" />
              <path d="M190 320 Q 10 240 100 130" />
              <path className="theory__edge--inner" d="M300 280 Q 360 200 410 130" />
            </g>

            <g className="theory__nodes-svg">
              <a href="#demo" aria-label={t('nodes.interview')}>
                <rect
                  className="theory__node-rect"
                  x="20"
                  y="40"
                  width="190"
                  height="80"
                  rx="14"
                />
                <text className="theory__node-text" x="115" y="86" textAnchor="middle">
                  {t('nodes.interview')}
                </text>
              </a>

              <a href="#demo" aria-label={t('nodes.work')}>
                <rect
                  className="theory__node-rect"
                  x="390"
                  y="40"
                  width="190"
                  height="80"
                  rx="14"
                />
                <text className="theory__node-text" x="485" y="86" textAnchor="middle">
                  {t('nodes.work')}
                </text>
              </a>

              <a href="#demo" aria-label={t('nodes.knowledge')}>
                <rect
                  className="theory__node-rect theory__node-rect--anchor"
                  x="195"
                  y="280"
                  width="210"
                  height="90"
                  rx="14"
                />
                <text
                  className="theory__node-text theory__node-text--anchor"
                  x="300"
                  y="332"
                  textAnchor="middle"
                >
                  {t('nodes.knowledge')}
                </text>
              </a>
            </g>
          </svg>
        </div>

        <a className="theory__leadout" href="#demo" aria-label={t('scrollAria')}>
          <span className="theory__leadout-text">{t('scrollHint')}</span>
          <svg
            className="theory__leadout-arrow"
            viewBox="0 0 24 56"
            focusable="false"
            aria-hidden="true"
          >
            <path
              className="theory__leadout-line"
              d="M12 4 V44"
              stroke="currentColor"
              strokeWidth="1.5"
              strokeLinecap="round"
              fill="none"
            />
            <path
              d="M5 38 L12 50 L19 38"
              stroke="currentColor"
              strokeWidth="1.5"
              strokeLinecap="round"
              strokeLinejoin="round"
              fill="none"
            />
          </svg>
        </a>
      </div>
    </section>
  );
}
