import { getTranslations } from 'next-intl/server';

type CodeStep = { title: string; description: string; snippet: string };
type TextStep = { title: string; description: string };
type ReleaseChannel = { title: string; description: string; primary: string; secondary: string };

export async function Connect() {
  const t = await getTranslations('connect');
  const install = t.raw('install') as CodeStep;
  const open = t.raw('open') as CodeStep;
  const embedded = t.raw('embedded') as TextStep;
  const releaseChannel = t.raw('releaseChannel') as ReleaseChannel;

  return (
    <section className="section" id="connect">
      <div className="container">
        <div className="section__kicker">{t('kicker')}</div>
        <h2 className="section__title">{t('title')}</h2>
        <p className="section__body" style={{ marginBottom: 'var(--space-8)' }}>
          {t('intro')}
        </p>

        <ol className="steps">
          <li className="step">
            <span className="step__num" aria-hidden="true">
              1
            </span>
            <div className="step__body">
              <h3 className="step__title">{install.title}</h3>
              <p className="step__desc">{install.description}</p>
              <pre className="code-block">
                <code>{install.snippet}</code>
              </pre>
            </div>
          </li>

          <li className="step">
            <span className="step__num" aria-hidden="true">
              2
            </span>
            <div className="step__body">
              <h3 className="step__title">{open.title}</h3>
              <p className="step__desc">{open.description}</p>
              <pre className="code-block">
                <code>{open.snippet}</code>
              </pre>
            </div>
          </li>

          <li className="step">
            <span className="step__num" aria-hidden="true">
              3
            </span>
            <div className="step__body">
              <h3 className="step__title">{embedded.title}</h3>
              <p className="step__desc">{embedded.description}</p>
            </div>
          </li>
        </ol>

        <div
          aria-labelledby="release-channel-title"
          style={{
            marginTop: 'var(--space-8)',
            paddingTop: 'var(--space-6)',
            borderTop: '1px solid var(--color-border)',
            maxWidth: 'var(--max-w-prose)',
          }}
        >
          <h3 className="step__title" id="release-channel-title">
            {releaseChannel.title}
          </h3>
          <p className="step__desc">{releaseChannel.description}</p>
          <div style={{ display: 'flex', gap: 'var(--space-3)', flexWrap: 'wrap' }}>
            <a
              className="btn btn-primary"
              href="https://t.me/throne_whitesnow_tech"
              target="_blank"
              rel="noreferrer noopener"
            >
              {releaseChannel.primary}
            </a>
            <a
              className="btn btn-secondary"
              href="https://github.com/gently-whitesnow/throne/releases"
              target="_blank"
              rel="noreferrer noopener"
            >
              {releaseChannel.secondary}
            </a>
          </div>
        </div>
      </div>
    </section>
  );
}
