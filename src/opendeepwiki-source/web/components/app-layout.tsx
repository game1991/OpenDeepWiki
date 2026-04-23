"use client";

import React from "react";
import { AppSidebar } from "@/app/sidebar";
import { SidebarInset, SidebarProvider } from "@/components/animate-ui/components/radix/sidebar";
import { Header } from "@/components/header";
import { useTranslations } from "@/hooks/use-translations";

interface HeaderSearchBoxProps {
  value: string;
  onChange: (value: string) => void;
  visible: boolean;
}

interface AppLayoutProps {
  children: React.ReactNode;
  activeItem?: string;
  onItemClick?: (item: string) => void;
  searchBox?: HeaderSearchBoxProps;
}

export function AppLayout({ children, activeItem, onItemClick, searchBox }: AppLayoutProps) {
  const t = useTranslations();
  const defaultActiveItem = activeItem || t("sidebar.explore");

  // Get current weekday
  const now = new Date();
  const dayIndex = now.getDay();
  const weekdays = ["sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"];
  const weekdayKey = weekdays[dayIndex];
  const currentWeekday = t(`common.weekdays.${weekdayKey}`);

  return (
    <SidebarProvider defaultOpen={true}>
      <AppSidebar activeItem={defaultActiveItem} onItemClick={onItemClick} className="!flex" />
      <SidebarInset>
        <Header
          title={defaultActiveItem}
          currentWeekday={currentWeekday}
          searchBox={searchBox}
        />
        {children}
      </SidebarInset>
    </SidebarProvider>
  );
}
