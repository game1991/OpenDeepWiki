import { getApiProxyUrl } from "./env";

const API_BASE_URL = getApiProxyUrl();

function buildApiUrl(path: string) {
  if (!API_BASE_URL) {
    return path;
  }
  const trimmedBase = API_BASE_URL.endsWith("/")
    ? API_BASE_URL.slice(0, -1)
    : API_BASE_URL;
  return `${trimmedBase}${path}`;
}

export interface AddSubscriptionRequest {
  userId: string;
  repositoryId: string;
}

export interface SubscriptionResponse {
  success: boolean;
  errorMessage?: string;
  subscriptionId?: string;
}

export interface SubscriptionStatusResponse {
  isSubscribed: boolean;
  subscribedAt?: string;
}

export interface SubscriptionItemResponse {
  subscriptionId: string;
  repositoryId: string;
  repoName: string;
  orgName: string;
  description?: string;
  starCount: number;
  forkCount: number;
  subscriptionCount: number;
  subscribedAt: string;
}

export interface SubscriptionListResponse {
  items: SubscriptionItemResponse[];
  total: number;
  page: number;
  pageSize: number;
}

/**
 * Add a subscription for a repository
 */
export async function addSubscription(request: AddSubscriptionRequest): Promise<SubscriptionResponse> {
  const url = buildApiUrl("/api/v1/subscriptions");

  const response = await fetch(url, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
  });

  return await response.json();
}

/**
 * Remove a subscription from a repository
 */
export async function removeSubscription(repositoryId: string, userId: string): Promise<SubscriptionResponse> {
  const url = buildApiUrl(`/api/v1/subscriptions/${encodeURIComponent(repositoryId)}?userId=${encodeURIComponent(userId)}`);

  const response = await fetch(url, {
    method: "DELETE",
  });

  return await response.json();
}

/**
 * Get subscription status for a repository
 */
export async function getSubscriptionStatus(
  repositoryId: string,
  userId: string
): Promise<SubscriptionStatusResponse> {
  const url = buildApiUrl(
    `/api/v1/subscriptions/${encodeURIComponent(repositoryId)}/status?userId=${encodeURIComponent(userId)}`
  );

  const response = await fetch(url, { cache: "no-store" });

  if (!response.ok) {
    throw new Error("Failed to fetch subscription status");
  }

  return await response.json();
}

/**
 * Get user's subscription list with pagination
 */
export async function getUserSubscriptions(
  userId: string,
  page: number = 1,
  pageSize: number = 20
): Promise<SubscriptionListResponse> {
  const params = new URLSearchParams({
    userId,
    page: page.toString(),
    pageSize: pageSize.toString(),
  });

  const url = buildApiUrl(`/api/v1/subscriptions?${params.toString()}`);

  const response = await fetch(url, { cache: "no-store" });

  if (!response.ok) {
    throw new Error("Failed to fetch subscriptions");
  }

  return await response.json();
}
