import type { Metadata } from "next"
import { NextIntlClientProvider } from "next-intl"
import { getLocale, getMessages } from "next-intl/server"
import "@/app/globals.css"

export const metadata: Metadata = {
  title: "AI Chat Share",
}

export default async function ShareLayout({ children }: { children: React.ReactNode }) {
  const [locale, messages] = await Promise.all([getLocale(), getMessages()])

  return (
    <NextIntlClientProvider locale={locale} messages={messages}>
      <div className="min-h-screen bg-slate-950 text-white">
        <div className="pointer-events-none fixed inset-0 -z-10">
          <div className="absolute inset-0 bg-[radial-gradient(circle_at_top,_rgba(59,130,246,0.25),_transparent_55%)]" />
          <div className="absolute inset-0 bg-[radial-gradient(circle_at_bottom,_rgba(236,72,153,0.2),_transparent_60%)]" />
          <div className="absolute inset-0 bg-slate-950/80 backdrop-blur" />
        </div>
        <main >
          {children}
        </main>
      </div>
    </NextIntlClientProvider>
  )
}