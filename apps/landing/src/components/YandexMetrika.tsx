'use client';

import Script from 'next/script';
import { usePathname, useSearchParams } from 'next/navigation';
import { Suspense, useEffect } from 'react';

const METRIKA_ID = process.env.NEXT_PUBLIC_YANDEX_METRIKA_ID;

type YmFn = (id: number, action: string, ...args: unknown[]) => void;

function MetrikaPageView({ id }: { id: number }) {
  const pathname = usePathname();
  const searchParams = useSearchParams();

  useEffect(() => {
    const ym = (window as unknown as { ym?: YmFn }).ym;
    if (typeof ym !== 'function') return;
    const qs = searchParams?.toString();
    const url = pathname + (qs ? `?${qs}` : '');
    ym(id, 'hit', url);
  }, [id, pathname, searchParams]);

  return null;
}

export function YandexMetrika() {
  if (!METRIKA_ID) return null;
  const id = Number(METRIKA_ID);

  return (
    <>
      <Script id="yandex-metrika" strategy="afterInteractive">
        {`(function(m,e,t,r,i,k,a){m[i]=m[i]||function(){(m[i].a=m[i].a||[]).push(arguments)};m[i].l=1*new Date();for(var j=0;j<document.scripts.length;j++){if(document.scripts[j].src===r){return;}}k=e.createElement(t),a=e.getElementsByTagName(t)[0],k.async=1,k.src=r,a.parentNode.insertBefore(k,a)})(window,document,'script','https://mc.yandex.ru/metrika/tag.js?id=${id}','ym');ym(${id},'init',{ssr:true,webvisor:true,clickmap:true,ecommerce:"dataLayer",accurateTrackBounce:true,trackLinks:true});`}
      </Script>
      <Suspense fallback={null}>
        <MetrikaPageView id={id} />
      </Suspense>
      <noscript>
        <div>
          <img
            src={`https://mc.yandex.ru/watch/${id}`}
            style={{ position: 'absolute', left: '-9999px' }}
            alt=""
          />
        </div>
      </noscript>
    </>
  );
}
