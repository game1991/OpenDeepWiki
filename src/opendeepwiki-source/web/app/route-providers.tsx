import type { ReactNode } from "react";
import { NextIntlClientProvider } from "next-intl";
import { getLocale, getMessages } from "next-intl/server";
import ClientProviders from "@/app/client-providers";

export default async function RouteProviders({ children }: { children: ReactNode }) {
  const [messages, locale] = await Promise.all([getMessages(), getLocale()]);

  return (
    <NextIntlClientProvider locale={locale} messages={messages}>
      <ClientProviders>{children}</ClientProviders>
    </NextIntlClientProvider>
  );
}
