export function getApiProxyUrl(): string {
  const publicApiProxyUrl = process.env.NEXT_PUBLIC_API_PROXY_URL?.trim();

  if (typeof window !== "undefined") {
    return publicApiProxyUrl || "";
  }

  return process.env.API_PROXY_URL?.trim() || publicApiProxyUrl || "";
}
