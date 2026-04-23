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

export interface AddBookmarkRequest {
  userId: string;
  repositoryId: string;
}

export interface BookmarkResponse {
  success: boolean;
  errorMessage?: string;
  bookmarkId?: string;
}

export interface BookmarkItemResponse {
  bookmarkId: string;
  repositoryId: string;
  repoName: string;
  orgName: string;
  description?: string;
  starCount: number;
  forkCount: number;
  bookmarkCount: number;
  bookmarkedAt: string;
}

export interface BookmarkListResponse {
  items: BookmarkItemResponse[];
  total: number;
  page: number;
  pageSize: number;
}

export interface BookmarkStatusResponse {
  isBookmarked: boolean;
  bookmarkedAt?: string;
}


/**
 * Add a bookmark for a repository
 */
export async function addBookmark(request: AddBookmarkRequest): Promise<BookmarkResponse> {
  const url = buildApiUrl("/api/v1/bookmarks");

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
 * Remove a bookmark from a repository
 */
export async function removeBookmark(repositoryId: string, userId: string): Promise<BookmarkResponse> {
  const url = buildApiUrl(`/api/v1/bookmarks/${encodeURIComponent(repositoryId)}?userId=${encodeURIComponent(userId)}`);

  const response = await fetch(url, {
    method: "DELETE",
  });

  return await response.json();
}

/**
 * Get user's bookmark list with pagination
 */
export async function getUserBookmarks(
  userId: string,
  page: number = 1,
  pageSize: number = 20
): Promise<BookmarkListResponse> {
  const params = new URLSearchParams({
    userId,
    page: page.toString(),
    pageSize: pageSize.toString(),
  });

  const url = buildApiUrl(`/api/v1/bookmarks?${params.toString()}`);

  const response = await fetch(url, { cache: "no-store" });

  if (!response.ok) {
    throw new Error("Failed to fetch bookmarks");
  }

  return await response.json();
}

/**
 * Get bookmark status for a repository
 */
export async function getBookmarkStatus(
  repositoryId: string,
  userId: string
): Promise<BookmarkStatusResponse> {
  const url = buildApiUrl(
    `/api/v1/bookmarks/${encodeURIComponent(repositoryId)}/status?userId=${encodeURIComponent(userId)}`
  );

  const response = await fetch(url, { cache: "no-store" });

  if (!response.ok) {
    throw new Error("Failed to fetch bookmark status");
  }

  return await response.json();
}
