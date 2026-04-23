import type { ReactNode } from "react";
import RouteProviders from "@/app/route-providers";
import AdminLayoutClient from "./admin-layout-client";

export const dynamic = "force-dynamic";

export default function AdminLayout({
  children,
}: {
  children: ReactNode;
}) {
  return (
    <RouteProviders>
      <AdminLayoutClient>{children}</AdminLayoutClient>
    </RouteProviders>
  );
}
