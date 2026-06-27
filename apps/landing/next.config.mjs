import createNextIntlPlugin from 'next-intl/plugin';

const withNextIntl = createNextIntlPlugin('./src/i18n/request.ts');

// BUILD_STATIC=true → статический экспорт в out/ (для раздачи nginx'ом на VPS).
// По умолчанию остаётся standalone (Docker/Caddy-сборка не ломается).
const buildStatic = process.env.BUILD_STATIC === 'true';

/** @type {import('next').NextConfig} */
const nextConfig = {
  output: buildStatic ? 'export' : 'standalone',
  reactStrictMode: true,
  poweredByHeader: false,
  // export-only: next/image без оптимизатора + папки вида /en/index.html,
  // чтобы nginx try_files $uri $uri/ отдавал маршруты без .html.
  ...(buildStatic ? { images: { unoptimized: true }, trailingSlash: true } : {}),
};

export default withNextIntl(nextConfig);
