import type { ReactNode } from "react";
import RouteProviders from "@/app/route-providers";

export default function AuthLayout({ children }: { children: ReactNode }) {
  return <RouteProviders>{children}</RouteProviders>;
}
