import Image from 'next/image';
import { getLocale, getTranslations } from 'next-intl/server';
import { Link } from '@/i18n/navigation';
import { LocaleSwitch } from './LocaleSwitch';
import { ThemeToggle } from './ThemeToggle';

export async function SiteHeader() {
  const t = await getTranslations('nav');
  const locale = await getLocale();

  return (
    <header className="site-header">
      <a className="skip-link" href="#main">
        {t('skipToContent')}
      </a>
      <div className="container site-header__inner">
        <Link href="/" className="brand" aria-label="Throne">
          <Image
            src="/logo-light.png"
            alt=""
            width={28}
            height={28}
            priority
            className="brand__logo brand__logo--light"
          />
          <Image
            src="/logo-dark.png"
            alt=""
            width={28}
            height={28}
            priority
            className="brand__logo brand__logo--dark"
          />
          <span>throne</span>
        </Link>
        <nav className="site-nav" aria-label="Primary">
          <a className="btn btn-ghost site-nav__connect" href="#connect">
            {t('connect')}
          </a>
          <a
            className="btn btn-ghost"
            href="https://github.com/gently-whitesnow/throne"
            target="_blank"
            rel="noreferrer noopener"
          >
            {t('github')}
          </a>
          <LocaleSwitch currentLocale={locale} />
          <ThemeToggle
            labels={{ toLight: t('switchToLight'), toDark: t('switchToDark') }}
          />
        </nav>
      </div>
    </header>
  );
}
