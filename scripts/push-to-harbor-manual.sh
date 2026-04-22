#!/bin/bash
# 手动推送到 Harbor 脚本
# 使用方式: ./scripts/push-to-harbor-manual.sh

set -e

HARBOR_URL="harbor.eagleye.com"
PROJECT="open-deep-wiki"
VERSION="1.0.0-fix$(date +%Y%m%d)"

echo "=========================================="
echo "OpenDeepWiki Harbor 手动推送脚本"
echo "=========================================="
echo ""
echo "前置条件:"
echo "1. 已连接到公司 VPN（如需）"
echo "2. Docker 已登录 Harbor: docker login ${HARBOR_URL}"
echo ""

# 检查本地镜像
echo "检查本地镜像..."
docker images | grep opendeepwiki || {
    echo "错误: 本地镜像不存在，请先执行 'make harbor-build'"
    exit 1
}

echo ""
echo "镜像列表:"
docker images | grep opendeepwiki

echo ""
echo "=========================================="
echo "开始标记并推送镜像..."
echo "=========================================="

# 标记后端镜像
echo ""
echo "1. 标记后端镜像..."
docker tag opendeepwiki-backend:fixed ${HARBOR_URL}/${PROJECT}/opendeepwiki-backend:${VERSION}
docker tag opendeepwiki-backend:fixed ${HARBOR_URL}/${PROJECT}/opendeepwiki-backend:latest

# 标记前端镜像（如果存在）
if docker images | grep -q opendeepwiki-web; then
    echo "2. 标记前端镜像..."
    docker tag opendeepwiki-web:fixed ${HARBOR_URL}/${PROJECT}/opendeepwiki-web:${VERSION}
    docker tag opendeepwiki-web:fixed ${HARBOR_URL}/${PROJECT}/opendeepwiki-web:latest
fi

# 推送
echo ""
echo "3. 推送后端镜像..."
docker push ${HARBOR_URL}/${PROJECT}/opendeepwiki-backend:${VERSION}
docker push ${HARBOR_URL}/${PROJECT}/opendeepwiki-backend:latest

if docker images | grep -q opendeepwiki-web; then
    echo ""
    echo "4. 推送前端镜像..."
    docker push ${HARBOR_URL}/${PROJECT}/opendeepwiki-web:${VERSION}
    docker push ${HARBOR_URL}/${PROJECT}/opendeepwiki-web:latest
fi

echo ""
echo "=========================================="
echo "推送完成!"
echo "=========================================="
echo ""
echo "镜像地址:"
echo "  ${HARBOR_URL}/${PROJECT}/opendeepwiki-backend:${VERSION}"
if docker images | grep -q opendeepwiki-web; then
    echo "  ${HARBOR_URL}/${PROJECT}/opendeepwiki-web:${VERSION}"
fi
echo ""
echo "部署命令:"
echo "  make harbor-deploy"
echo ""
