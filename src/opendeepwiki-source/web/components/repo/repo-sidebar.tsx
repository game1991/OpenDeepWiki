"use client";

import React from "react";
import Link from "next/link";
import { useParams, useSearchParams } from "next/navigation";
import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail,
} from "@/components/animate-ui/components/radix/sidebar";
import { ScrollArea } from "@/components/ui/scroll-area";
import type { RepoTreeNode } from "@/types/repository";
import { buildRepoBasePath, encodeSlugPath } from "@/lib/repo-route";

interface RepoSidebarProps {
  owner: string;
  repo: string;
  nodes: RepoTreeNode[];
}

export function RepoSidebar({ owner, repo, nodes }: RepoSidebarProps) {
  const params = useParams<{ slug?: string | string[] }>();
  const searchParams = useSearchParams();
  const slugParam = params?.slug;
  const activeSlug = Array.isArray(slugParam) ? slugParam.join("/") : slugParam ?? "";
  const basePath = buildRepoBasePath(owner, repo);

  // 构建带查询参数的链接
  const buildHref = (slug: string) => {
    const path = `${basePath}/${encodeSlugPath(slug)}`;
    const queryString = searchParams.toString();
    return queryString ? `${path}?${queryString}` : path;
  };

  const renderNodes = (items: RepoTreeNode[], depth = 0) => {
    return items.map((node) => (
      <SidebarMenuItem key={node.slug}>
        <SidebarMenuButton
          asChild
          isActive={activeSlug === node.slug}
          tooltip={node.title}
        >
          <Link
            href={buildHref(node.slug)}
            className="flex w-full items-center gap-2"
            style={{ paddingLeft: 8 + depth * 12 }}
          >
            <span className="truncate">{node.title}</span>
          </Link>
        </SidebarMenuButton>
        {node.children.length > 0 && (
          <div className="mt-1 space-y-1">
            {renderNodes(node.children, depth + 1)}
          </div>
        )}
      </SidebarMenuItem>
    ));
  };

  return (
    <Sidebar collapsible="icon">
      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupLabel>{owner}/{repo}</SidebarGroupLabel>
          <SidebarGroupContent>
            <ScrollArea className="h-[calc(100vh-8rem)] pr-2">
              <SidebarMenu>{renderNodes(nodes)}</SidebarMenu>
            </ScrollArea>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>
      <SidebarRail />
    </Sidebar>
  );
}
