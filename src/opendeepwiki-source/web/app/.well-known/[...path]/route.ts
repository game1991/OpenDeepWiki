import { NextRequest, NextResponse } from 'next/server';

let cachedApiUrl: string | null = null;
let lastLoadTime = 0;
const CACHE_TTL = 5000;

function getApiProxyUrl(): string {
  if (process.env.API_PROXY_URL) {
    return process.env.API_PROXY_URL;
  }

  const now = Date.now();
  if (cachedApiUrl !== null && now - lastLoadTime < CACHE_TTL) {
    return cachedApiUrl;
  }

  try {
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    const fs = require('fs');
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    const path = require('path');
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    const dotenv = require('dotenv');

    const rootDir = process.cwd();
    const envLocalPath = path.resolve(rootDir, '.env.local');
    const envPath = path.resolve(rootDir, '.env');

    if (fs.existsSync(envLocalPath)) {
      const result = dotenv.config({ path: envLocalPath });
      if (result.parsed?.API_PROXY_URL) {
        cachedApiUrl = result.parsed.API_PROXY_URL;
        lastLoadTime = now;
        return cachedApiUrl!;
      }
    }

    if (fs.existsSync(envPath)) {
      const result = dotenv.config({ path: envPath });
      if (result.parsed?.API_PROXY_URL) {
        cachedApiUrl = result.parsed.API_PROXY_URL;
        lastLoadTime = now;
        return cachedApiUrl!;
      }
    }
  } catch {
    // Ignore env loading failures and fall back to empty string
  }

  cachedApiUrl = '';
  lastLoadTime = now;
  return '';
}

async function proxyRequest(request: NextRequest) {
  const apiUrl = getApiProxyUrl();
  if (!apiUrl) {
    return NextResponse.json(
      { error: 'API_PROXY_URL_NOT_CONFIGURED' },
      { status: 503 }
    );
  }

  const pathname = request.nextUrl.pathname;
  const searchParams = request.nextUrl.search;
  const targetUrl = `${apiUrl}${pathname}${searchParams}`;

  try {
    const headers = new Headers();
    request.headers.forEach((value, key) => {
      if (!['host', 'connection'].includes(key.toLowerCase())) {
        headers.set(key, value);
      }
    });

    const response = await fetch(targetUrl, {
      method: request.method,
      headers,
      redirect: 'manual',
    });

    if (response.status === 302 || response.status === 301) {
      const location = response.headers.get('location');
      if (location) {
        return NextResponse.redirect(location, response.status as 301 | 302);
      }
    }

    const responseHeaders = new Headers();
    response.headers.forEach((value, key) => {
      if (!['content-encoding', 'transfer-encoding'].includes(key.toLowerCase())) {
        responseHeaders.set(key, value);
      }
    });

    return new NextResponse(response.body, {
      status: response.status,
      statusText: response.statusText,
      headers: responseHeaders,
    });
  } catch (error) {
    const errorMessage = error instanceof Error ? error.message : 'Unknown error';
    return NextResponse.json(
      { error: 'PROXY_ERROR', message: errorMessage },
      { status: 502 }
    );
  }
}

export async function GET(request: NextRequest) {
  return proxyRequest(request);
}
