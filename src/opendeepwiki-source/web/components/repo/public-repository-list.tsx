"use client";

import { useEffect, useState, useCallback } from "react";
import { Skeleton } from "@/components/ui/skeleton";
import { Button } from "@/components/ui/button";
import { useTranslations } from "@/hooks/use-translations";
import { fetchRepositoryList } from "@/lib/repository-api";
import { PublicRepositoryCard } from "./public-repository-card";
import { LanguageTags } from "./language-tags";
import type { RepositoryItemResponse } from "@/types/repository";
import { GitBranch, XCircle, RefreshCw, Search, ChevronLeft, ChevronRight } from "lucide-react";
import { cn } from "@/lib/utils";

interface PublicRepositoryListProps {
  keyword: string;
  className?: string;
}

const PAGE_SIZE = 12;

function RepositoryGridSkeleton() {
  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      {[1, 2, 3, 4, 5, 6].map((i) => (
        <div key={i} className="p-4 border rounded-lg">
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <Skeleton className="h-5 w-40" />
              <Skeleton className="h-6 w-20 rounded-full" />
            </div>
            <Skeleton className="h-4 w-32" />
          </div>
        </div>
      ))}
    </div>
  );
}

export function PublicRepositoryList({ keyword, className }: PublicRepositoryListProps) {
  const t = useTranslations();
  const [repositories, setRepositories] = useState<RepositoryItemResponse[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedLanguage, setSelectedLanguage] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);

  const totalPages = Math.ceil(total / PAGE_SIZE);

  const loadRepositories = useCallback(async () => {
    try {
      setIsLoading(true);
      setError(null);

      const response = await fetchRepositoryList({
        isPublic: true,
        sortBy: "status",
        keyword: keyword || undefined,
        language: selectedLanguage || undefined,
        page,
        pageSize: PAGE_SIZE,
      });

      setRepositories(response.items);
      setTotal(response.total);
    } catch (err) {
      setError(t("home.publicRepository.loadError"));
      console.error("Failed to fetch public repositories:", err);
    } finally {
      setIsLoading(false);
    }
  }, [keyword, page, selectedLanguage, t]);

  useEffect(() => {
    loadRepositories();
  }, [loadRepositories]);

  useEffect(() => {
    setPage(1);
  }, [keyword, selectedLanguage]);

  const handleLanguageChange = (language: string | null) => {
    setSelectedLanguage(language);
  };

  const handlePrevPage = () => {
    if (page > 1) {
      setPage(page - 1);
    }
  };

  const handleNextPage = () => {
    if (page < totalPages) {
      setPage(page + 1);
    }
  };

  if (isLoading && repositories.length === 0) {
    return (
      <div className={cn("w-full", className)}>
        <h2 className="text-xl font-semibold mb-4">
          {t("home.publicRepository.title")}
        </h2>
        <div className="mb-6">
          <div className="flex flex-wrap gap-2">
            {[1, 2, 3, 4, 5, 6].map((i) => (
              <Skeleton key={i} className="h-7 w-20 rounded-full" />
            ))}
          </div>
        </div>
        <RepositoryGridSkeleton />
      </div>
    );
  }

  if (error) {
    return (
      <div className={cn("w-full", className)}>
        <h2 className="text-xl font-semibold mb-4">
          {t("home.publicRepository.title")}
        </h2>
        <div className="flex flex-col items-center justify-center py-12 text-center">
          <XCircle className="h-12 w-12 text-destructive mb-4" />
          <p className="text-muted-foreground mb-4">{error}</p>
          <Button variant="outline" onClick={loadRepositories}>
            <RefreshCw className="mr-2 h-4 w-4" />
            {t("home.repository.retry")}
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className={cn("w-full", className)}>
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-xl font-semibold">
          {t("home.publicRepository.title")}
        </h2>
        <Button
          variant="ghost"
          size="icon"
          onClick={loadRepositories}
          disabled={isLoading}
        >
          <RefreshCw className={cn("h-4 w-4", isLoading && "animate-spin")} />
        </Button>
      </div>

      <LanguageTags
        selectedLanguage={selectedLanguage}
        onLanguageChange={handleLanguageChange}
        className="mb-6"
      />

      {repositories.length === 0 && !keyword && !selectedLanguage ? (
        <div className="flex flex-col items-center justify-center py-12 text-center">
          <GitBranch className="h-12 w-12 text-muted-foreground mb-4" />
          <p className="text-muted-foreground">
            {t("home.publicRepository.empty")}
          </p>
        </div>
      ) : repositories.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-12 text-center">
          <Search className="h-12 w-12 text-muted-foreground mb-4" />
          <p className="text-muted-foreground">
            {t("home.publicRepository.noResults")}
          </p>
        </div>
      ) : (
        <>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {repositories.map((repo) => (
              <PublicRepositoryCard key={repo.id} repository={repo} />
            ))}
          </div>

          {totalPages > 1 && (
            <div className="flex items-center justify-center gap-4 mt-8">
              <Button
                variant="outline"
                size="sm"
                onClick={handlePrevPage}
                disabled={page === 1 || isLoading}
              >
                <ChevronLeft className="h-4 w-4 mr-1" />
                {t("home.bookmarks.previous")}
              </Button>
              <span className="text-sm text-muted-foreground">
                {t("home.bookmarks.pageInfo")
                  .replace("{current}", page.toString())
                  .replace("{total}", totalPages.toString())}
              </span>
              <Button
                variant="outline"
                size="sm"
                onClick={handleNextPage}
                disabled={page === totalPages || isLoading}
              >
                {t("home.bookmarks.next")}
                <ChevronRight className="h-4 w-4 ml-1" />
              </Button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
