import { getTranslations } from 'next-intl/server';

export async function SiteFooter() {
  const t = await getTranslations('footer');
  const year = new Date().getFullYear();
  return (
    <footer className="site-footer">
      <div className="container site-footer__inner">
        <span>
          © {year} · {t('rights')}
        </span>
        <span>{t('tagline')}</span>
      </div>
    </footer>
  );
}
