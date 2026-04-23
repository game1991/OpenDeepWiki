import type { ReactNode } from "react";
import RouteProviders from "@/app/route-providers";

export default function MainLayout({ children }: { children: ReactNode }) {
  return <RouteProviders>{children}</RouteProviders>;
}
