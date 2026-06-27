import { getTranslations } from 'next-intl/server';

type SnippetTab = { label: string; description: string; snippet: string };
type RunBlock = { title: string; description: string; snippet: string };
type TextBlock = { title: string; description: string };

export async function Connect() {
  const t = await getTranslations('connect');
  const run = t.raw('run') as RunBlock;
  const embedded = t.raw('embedded') as TextBlock;
  const claudeDesktop = t.raw('tabs.claudeDesktop') as SnippetTab;
  const claudeCode = t.raw('tabs.claudeCode') as SnippetTab;
  const cursor = t.raw('tabs.cursor') as SnippetTab;
  const codex = t.raw('tabs.codex') as SnippetTab;

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
              <h3 className="step__title">{run.title}</h3>
              <p className="step__desc">{run.description}</p>
              <pre className="code-block">
                <code>{run.snippet}</code>
              </pre>
            </div>
          </li>

          <li className="step">
            <span className="step__num" aria-hidden="true">
              2
            </span>
            <div className="step__body">
              <h3 className="step__title">{embedded.title}</h3>
              <p className="step__desc">{embedded.description}</p>
            </div>
          </li>

          <li className="step">
            <span className="step__num" aria-hidden="true">
              3
            </span>
            <div className="step__body">
              <h3 className="step__title">{t('clientsTitle')}</h3>
              <p className="step__desc">{t('clientsDescription')}</p>
              <div className="step__clients">
                <ClientDisclosure tab={claudeDesktop} defaultOpen />
                <ClientDisclosure tab={claudeCode} />
                <ClientDisclosure tab={cursor} />
                <ClientDisclosure tab={codex} />
              </div>
            </div>
          </li>
        </ol>
      </div>
    </section>
  );
}

function ClientDisclosure({ tab, defaultOpen }: { tab: SnippetTab; defaultOpen?: boolean }) {
  return (
    <details className="disclosure" open={defaultOpen}>
      <summary className="disclosure__summary">{tab.label}</summary>
      <div className="disclosure__body">
        <p className="disclosure__desc">{tab.description}</p>
        <pre className="code-block">
          <code>{tab.snippet}</code>
        </pre>
      </div>
    </details>
  );
}
