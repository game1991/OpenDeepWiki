import type { NextConfig } from "next";
import createNextIntlPlugin from 'next-intl/plugin';

const withNextIntl = createNextIntlPlugin('./i18n/request.ts');

const nextConfig: NextConfig = {
  output: 'standalone',
  // API 代理已改为运行时动态转发，见 app/api/[...path]/route.ts
  // 环境变量 API_PROXY_URL 在运行时读取，无需构建时烘焙
};

export default withNextIntl(nextConfig);
