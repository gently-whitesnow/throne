import { setRequestLocale } from 'next-intl/server';
import { Hero } from '@/components/Hero';
import { Theory } from '@/components/Theory';
import { VideoDemo } from '@/components/VideoDemo';
import { Features } from '@/components/Features';
import { Connect } from '@/components/Connect';
import { Roadmap } from '@/components/Roadmap';
import { Contacts } from '@/components/Contacts';

export default async function HomePage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);

  return (
    <>
      <Hero />
      <Theory />
      <VideoDemo />
      <Features />
      <Connect />
      <Roadmap />
      <Contacts />
    </>
  );
}
