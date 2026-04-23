"use client";

import { FileCode2, ExternalLink } from "lucide-react";
import { useParams, useSearchParams } from "next/navigation";
import { useMemo } from "react";
import { decodeRouteSegment } from "@/lib/repo-route";

interface SourceFilesProps {
  files: string[];
  gitUrl?: string;
  branch?: string;
}

/**
 * 构建文件的 Git 平台链接
 */
function buildFileUrl(gitUrl: string, branch: string, filePath: string): string {
  // 规范化 URL
  let normalizedUrl = gitUrl.replace(/\.git$/, "").trim();
  
  // 转换 SSH 格式为 HTTPS
  if (normalizedUrl.startsWith("git@")) {
    normalizedUrl = normalizedUrl.replace("git@", "https://").replace(":", "/");
  }
  
  normalizedUrl = normalizedUrl.replace(/\/$/, "");
  
  // 根据平台构建 URL
  if (normalizedUrl.includes("github.com")) {
    return `${normalizedUrl}/blob/${branch}/${filePath}`;
  } else if (normalizedUrl.includes("gitlab.com") || normalizedUrl.includes("gitlab")) {
    return `${normalizedUrl}/-/blob/${branch}/${filePath}`;
  } else if (normalizedUrl.includes("gitee.com")) {
    return `${normalizedUrl}/blob/${branch}/${filePath}`;
  } else if (normalizedUrl.includes("bitbucket.org")) {
    return `${normalizedUrl}/src/${branch}/${filePath}`;
  }
  
  // 默认使用 GitHub 格式
  return `${normalizedUrl}/blob/${branch}/${filePath}`;
}

export function SourceFiles({ files, gitUrl, branch }: SourceFilesProps) {
  const params = useParams();
  const searchParams = useSearchParams();
  
  // 从 URL 参数获取仓库信息
  const owner = decodeRouteSegment(params.owner as string);
  const repo = decodeRouteSegment(params.repo as string);
  const currentBranch = searchParams.get("branch") || branch || "main";
  
  // 构建默认的 Git URL
  const defaultGitUrl = gitUrl || `https://github.com/${owner}/${repo}`;
  
  // 对文件进行分组（按目录）
  const groupedFiles = useMemo(() => {
    const groups: Record<string, string[]> = {};
    
    files.forEach(file => {
      const parts = file.split("/");
      const dir = parts.length > 1 ? parts.slice(0, -1).join("/") : "(root)";
      
      if (!groups[dir]) {
        groups[dir] = [];
      }
      groups[dir].push(file);
    });
    
    // 按目录名排序
    return Object.entries(groups).sort(([a], [b]) => a.localeCompare(b));
  }, [files]);
  
  if (!files || files.length === 0) {
    return null;
  }
  
  return (
    <div className="mt-12 pt-8 border-t border-fd-border">
      <div className="flex items-center gap-2 mb-4">
        <FileCode2 className="h-5 w-5 text-fd-muted-foreground" />
        <h3 className="text-lg font-semibold text-fd-foreground">
          Sources
        </h3>
        <span className="text-sm text-fd-muted-foreground">
          ({files.length} files)
        </span>
      </div>
      
      <div className="space-y-4">
        {groupedFiles.map(([dir, dirFiles]) => (
          <div key={dir} className="space-y-1">
            {groupedFiles.length > 1 && (
              <div className="text-xs font-medium text-fd-muted-foreground uppercase tracking-wide">
                {dir}
              </div>
            )}
            <div className="flex flex-wrap gap-2">
              {dirFiles.map(file => {
                const fileName = file.split("/").pop() || file;
                const fileUrl = buildFileUrl(defaultGitUrl, currentBranch, file);
                
                return (
                  <a
                    key={file}
                    href={fileUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="inline-flex items-center gap-1.5 px-2.5 py-1 text-sm bg-fd-secondary hover:bg-fd-accent rounded-md text-fd-foreground transition-colors group"
                    title={file}
                  >
                    <FileCode2 className="h-3.5 w-3.5 text-fd-muted-foreground" />
                    <span className="max-w-[200px] truncate">{fileName}</span>
                    <ExternalLink className="h-3 w-3 text-fd-muted-foreground opacity-0 group-hover:opacity-100 transition-opacity" />
                  </a>
                );
              })}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
