/**
 * 推荐系统 API 客户端
 */

import { api } from "./api-client";

/** 推荐仓库项 */
export interface RecommendedRepository {
  id: string;
  repoName: string;
  orgName: string;
  primaryLanguage?: string;
  starCount: number;
  forkCount: number;
  bookmarkCount: number;
  subscriptionCount: number;
  viewCount: number;
  createdAt: string;
  updatedAt?: string;
  score: number;
  scoreBreakdown?: ScoreBreakdown;
  recommendReason?: string;
}

/** 得分明细 */
export interface ScoreBreakdown {
  popularity: number;
  subscription: number;
  timeDecay: number;
  userPreference: number;
  privateRepoLanguage: number;
  collaborative: number;
}

/** 推荐响应 */
export interface RecommendationResponse {
  items: RecommendedRepository[];
  strategy: string;
  totalCandidates: number;
}

/** 推荐请求参数 */
export interface RecommendationParams {
  userId?: string;
  limit?: number;
  strategy?: "default" | "popular" | "personalized" | "explore";
  language?: string;
}

/** 记录活动请求 */
export interface RecordActivityRequest {
  userId: string;
  repositoryId?: string;
  activityType: "View" | "Search" | "Bookmark" | "Subscribe" | "Analyze";
  duration?: number;
  searchQuery?: string;
  language?: string;
}

/** 记录活动响应 */
export interface RecordActivityResponse {
  success: boolean;
  errorMessage?: string;
}

/** 不感兴趣请求 */
export interface DislikeRequest {
  userId: string;
  repositoryId: string;
  reason?: string;
}

/** 不感兴趣响应 */
export interface DislikeResponse {
  success: boolean;
  errorMessage?: string;
}

/** 语言信息 */
export interface LanguageInfo {
  name: string;
  count: number;
}

/** 可用语言列表响应 */
export interface AvailableLanguagesResponse {
  languages: LanguageInfo[];
}

/**
 * 获取推荐仓库列表
 */
export async function getRecommendations(
  params: RecommendationParams = {}
): Promise<RecommendationResponse> {
  const searchParams = new URLSearchParams();
  
  if (params.userId) searchParams.set("userId", params.userId);
  if (params.limit) searchParams.set("limit", params.limit.toString());
  if (params.strategy) searchParams.set("strategy", params.strategy);
  if (params.language) searchParams.set("language", params.language);

  const queryString = searchParams.toString();
  const path = `/api/v1/recommendations${queryString ? `?${queryString}` : ""}`;
  
  return api.get<RecommendationResponse>(path);
}

/**
 * 获取热门仓库
 */
export async function getPopularRepos(
  limit: number = 20,
  language?: string
): Promise<RecommendationResponse> {
  const searchParams = new URLSearchParams();
  searchParams.set("limit", limit.toString());
  if (language) searchParams.set("language", language);

  return api.get<RecommendationResponse>(
    `/api/v1/recommendations/popular?${searchParams.toString()}`
  );
}

/**
 * 获取可用的编程语言列表
 */
export async function getAvailableLanguages(): Promise<AvailableLanguagesResponse> {
  return api.get<AvailableLanguagesResponse>("/api/v1/recommendations/languages");
}

/**
 * 记录用户活动
 */
export async function recordActivity(
  request: RecordActivityRequest
): Promise<RecordActivityResponse> {
  return api.post<RecordActivityResponse>("/api/v1/recommendations/activity", request);
}

/**
 * 标记仓库为不感兴趣
 */
export async function markAsDisliked(
  request: DislikeRequest
): Promise<DislikeResponse> {
  return api.post<DislikeResponse>("/api/v1/recommendations/dislike", request);
}

/**
 * 取消不感兴趣标记
 */
export async function removeDislike(
  userId: string,
  repositoryId: string
): Promise<{ success: boolean }> {
  return api.delete<{ success: boolean }>(
    `/api/v1/recommendations/dislike/${repositoryId}?userId=${userId}`
  );
}

/**
 * 刷新用户偏好缓存
 */
export async function refreshUserPreference(
  userId: string
): Promise<{ success: boolean; message: string }> {
  return api.post<{ success: boolean; message: string }>(
    `/api/v1/recommendations/refresh-preference/${userId}`
  );
}
