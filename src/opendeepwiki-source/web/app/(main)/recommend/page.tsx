"use client";

import { useState, useEffect, useCallback } from "react";
import { AppLayout } from "@/components/app-layout";
import { useTranslations } from "@/hooks/use-translations";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { 
  Star, 
  GitFork, 
  Bell, 
  Bookmark,
  RefreshCw,
  Sparkles,
  TrendingUp,
  Compass,
  Loader2,
  MoreVertical,
  ThumbsDown,
  Eye,
  Code2
} from "lucide-react";
import { 
  getRecommendations,
  getAvailableLanguages,
  markAsDisliked,
  recordActivity,
  type RecommendedRepository,
  type RecommendationParams,
  type LanguageInfo
} from "@/lib/recommendation-api";
import { useAuth } from "@/contexts/auth-context";
import Link from "next/link";
import { buildRepoBasePath } from "@/lib/repo-route";

type Strategy = "default" | "popular" | "personalized" | "explore";

function RepoCardSkeleton() {
  return (
    <Card className="h-full">
      <CardHeader className="pb-3">
        <div className="flex items-start justify-between">
          <div className="space-y-2">
            <Skeleton className="h-5 w-32" />
            <Skeleton className="h-4 w-20" />
          </div>
          <Skeleton className="h-5 w-16 rounded-full" />
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        <Skeleton className="h-4 w-full" />
        <div className="flex gap-3">
          <Skeleton className="h-4 w-12" />
          <Skeleton className="h-4 w-12" />
          <Skeleton className="h-4 w-12" />
        </div>
        <div className="pt-2 border-t">
          <Skeleton className="h-1.5 w-full rounded-full" />
        </div>
      </CardContent>
    </Card>
  );
}

export default function RecommendPage() {
  const t = useTranslations();
  const { user } = useAuth();
  const [activeItem, setActiveItem] = useState(t("sidebar.recommend"));
  const [repos, setRepos] = useState<RecommendedRepository[]>([]);
  const [loading, setLoading] = useState(true);
  const [strategy, setStrategy] = useState<Strategy>("default");
  const [totalCandidates, setTotalCandidates] = useState(0);
  const [languages, setLanguages] = useState<LanguageInfo[]>([]);
  const [selectedLanguage, setSelectedLanguage] = useState<string>("all");
  const [dislikingId, setDislikingId] = useState<string | null>(null);

  // 获取可用语言列表
  useEffect(() => {
    getAvailableLanguages()
      .then((res) => setLanguages(res.languages))
      .catch(console.error);
  }, []);

  const fetchRecommendations = useCallback(async () => {
    setLoading(true);
    try {
      const params: RecommendationParams = {
        userId: user?.id,
        limit: 20,
        strategy,
        language: selectedLanguage === "all" ? undefined : selectedLanguage,
      };
      const response = await getRecommendations(params);
      setRepos(response.items);
      setTotalCandidates(response.totalCandidates);
    } catch (error) {
      console.error("Failed to fetch recommendations:", error);
    } finally {
      setLoading(false);
    }
  }, [user?.id, strategy, selectedLanguage]);

  useEffect(() => {
    fetchRecommendations();
  }, [fetchRecommendations]);

  const formatNumber = (num: number): string => {
    if (num >= 1000) {
      return (num / 1000).toFixed(1) + "k";
    }
    return num.toString();
  };

  const handleDislike = async (repo: RecommendedRepository, e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    
    if (!user) return;
    
    setDislikingId(repo.id);
    try {
      const result = await markAsDisliked({
        userId: user.id,
        repositoryId: repo.id,
      });
      
      if (result.success) {
        // 从列表中移除
        setRepos((prev) => prev.filter((r) => r.id !== repo.id));
      }
    } catch (error) {
      console.error("Failed to mark as disliked:", error);
    } finally {
      setDislikingId(null);
    }
  };

  const handleRepoClick = async (repo: RecommendedRepository) => {
    if (!user) return;
    
    // 记录浏览行为
    try {
      await recordActivity({
        userId: user.id,
        repositoryId: repo.id,
        activityType: "View",
        language: repo.primaryLanguage || undefined,
      });
    } catch (error) {
      console.error("Failed to record activity:", error);
    }
  };

  const getStrategyIcon = (s: Strategy) => {
    switch (s) {
      case "popular": return <TrendingUp className="h-4 w-4" />;
      case "personalized": return <Sparkles className="h-4 w-4" />;
      case "explore": return <Compass className="h-4 w-4" />;
      default: return <Star className="h-4 w-4" />;
    }
  };

  return (
    <AppLayout activeItem={activeItem} onItemClick={setActiveItem}>
      <div className="flex flex-1 flex-col gap-4 p-4 md:p-6">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div className="space-y-1">
            <h1 className="text-3xl font-bold tracking-tight">{t("sidebar.recommend")}</h1>
            <p className="text-muted-foreground">
              {totalCandidates > 0 
                ? t("recommend.subtitleWithCount", { count: totalCandidates })
                : t("recommend.subtitle")}
            </p>
          </div>
          <div className="flex items-center gap-2">
            <Select value={selectedLanguage} onValueChange={setSelectedLanguage}>
              <SelectTrigger className="w-[140px]">
                <Code2 className="h-4 w-4 mr-2" />
                <SelectValue placeholder={t("recommend.allLanguages")} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">{t("recommend.allLanguages")}</SelectItem>
                {languages.map((lang) => (
                  <SelectItem key={lang.name} value={lang.name}>
                    {lang.name} ({lang.count})
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button 
              variant="outline" 
              size="sm" 
              onClick={fetchRecommendations}
              disabled={loading}
            >
              <RefreshCw className={`h-4 w-4 mr-2 ${loading ? "animate-spin" : ""}`} />
              {t("recommend.refresh")}
            </Button>
          </div>
        </div>

        <Tabs value={strategy} onValueChange={(v) => setStrategy(v as Strategy)}>
          <TabsList>
            <TabsTrigger value="default" className="gap-2">
              <Star className="h-4 w-4" />
              {t("recommend.strategies.default")}
            </TabsTrigger>
            <TabsTrigger value="popular" className="gap-2">
              <TrendingUp className="h-4 w-4" />
              {t("recommend.strategies.popular")}
            </TabsTrigger>
            {user && (
              <TabsTrigger value="personalized" className="gap-2">
                <Sparkles className="h-4 w-4" />
                {t("recommend.strategies.personalized")}
              </TabsTrigger>
            )}
            <TabsTrigger value="explore" className="gap-2">
              <Compass className="h-4 w-4" />
              {t("recommend.strategies.explore")}
            </TabsTrigger>
          </TabsList>
        </Tabs>

        {loading ? (
          <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            {Array.from({ length: 6 }).map((_, i) => (
              <RepoCardSkeleton key={i} />
            ))}
          </div>
        ) : repos.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-20 text-muted-foreground">
            <Compass className="h-12 w-12 mb-4" />
            <p>{t("recommend.empty.title")}</p>
            <p className="text-sm">{t("recommend.empty.description")}</p>
          </div>
        ) : (
          <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            {repos.map((repo) => (
              <Link 
                key={repo.id} 
                href={buildRepoBasePath(repo.orgName, repo.repoName)}
                className="block"
                onClick={() => handleRepoClick(repo)}
              >
                <Card className="h-full hover:shadow-lg transition-shadow cursor-pointer group">
                  <CardHeader className="pb-3">
                    <div className="flex items-start justify-between">
                      <div className="space-y-1 flex-1 min-w-0">
                        <CardTitle className="text-lg truncate">{repo.repoName}</CardTitle>
                        <CardDescription className="truncate">{repo.orgName}</CardDescription>
                      </div>
                      <div className="flex items-center gap-2">
                        {repo.primaryLanguage && (
                          <Badge variant="secondary" className="text-xs shrink-0">
                            {repo.primaryLanguage}
                          </Badge>
                        )}
                        {user && (
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button 
                                variant="ghost" 
                                size="icon" 
                                className="h-8 w-8 opacity-0 group-hover:opacity-100 transition-opacity"
                                onClick={(e) => e.preventDefault()}
                              >
                                <MoreVertical className="h-4 w-4" />
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuItem 
                                onClick={(e) => handleDislike(repo, e)}
                                disabled={dislikingId === repo.id}
                              >
                                {dislikingId === repo.id ? (
                                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                                ) : (
                                  <ThumbsDown className="h-4 w-4 mr-2" />
                                )}
                                {t("recommend.actions.notInterested")}
                              </DropdownMenuItem>
                            </DropdownMenuContent>
                          </DropdownMenu>
                        )}
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent className="space-y-3">
                    {repo.recommendReason && (
                      <div className="flex items-center gap-1 text-xs text-primary">
                        {getStrategyIcon(strategy)}
                        <span className="truncate">{repo.recommendReason}</span>
                      </div>
                    )}
                    
                    <div className="flex flex-wrap items-center gap-3 text-sm text-muted-foreground">
                      <div className="flex items-center gap-1">
                        <Star className="h-4 w-4" />
                        <span>{formatNumber(repo.starCount)}</span>
                      </div>
                      <div className="flex items-center gap-1">
                        <GitFork className="h-4 w-4" />
                        <span>{formatNumber(repo.forkCount)}</span>
                      </div>
                      <div className="flex items-center gap-1">
                        <Bell className="h-4 w-4" />
                        <span>{formatNumber(repo.subscriptionCount)}</span>
                      </div>
                      <div className="flex items-center gap-1">
                        <Bookmark className="h-4 w-4" />
                        <span>{formatNumber(repo.bookmarkCount)}</span>
                      </div>
                      <div className="flex items-center gap-1">
                        <Eye className="h-4 w-4" />
                        <span>{formatNumber(repo.viewCount)}</span>
                      </div>
                    </div>

                    {repo.scoreBreakdown && (
                      <div className="pt-2 border-t">
                        <div className="flex items-center justify-between text-xs text-muted-foreground">
                          <span>{t("recommend.score.label")}</span>
                          <span className="font-medium text-foreground">
                            {(repo.score * 100).toFixed(0)}%
                          </span>
                        </div>
                        <div className="mt-1 h-1.5 bg-muted rounded-full overflow-hidden">
                          <div 
                            className="h-full bg-primary rounded-full transition-all"
                            style={{ width: `${repo.score * 100}%` }}
                          />
                        </div>
                      </div>
                    )}
                  </CardContent>
                </Card>
              </Link>
            ))}
          </div>
        )}
      </div>
    </AppLayout>
  );
}
