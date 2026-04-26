#!/bin/bash
# create-opendeepwiki-secret.sh
# 交互式创建 OpenDeepWiki 所需的 K8s Secret
# 用法：bash scripts/create-opendeepwiki-secret.sh

set -e

NAMESPACE="opendeepwiki"
SECRET_NAME="opendeepwiki-secrets"

echo "=============================================="
echo "OpenDeepWiki Secret 创建工具"
echo "=============================================="
echo ""

# 检查 namespace 是否存在
if ! kubectl get namespace "$NAMESPACE" > /dev/null 2>&1; then
    echo "Namespace '$NAMESPACE' 不存在，正在创建..."
    kubectl create namespace "$NAMESPACE"
    echo "Namespace 已创建"
fi

echo ""
echo "请输入以下配置信息（输入后不会回显密码）："
echo ""

# OpenAI API Key
echo "--- AI API 配置 ---"
read -sp "Chat API Key: " CHAT_API_KEY
echo ""
read -sp "Catalog API Key (回车则与 Chat 相同): " CATALOG_API_KEY
echo ""
if [ -z "$CATALOG_API_KEY" ]; then
    CATALOG_API_KEY="$CHAT_API_KEY"
    echo "  -> 使用 Chat API Key"
fi
read -sp "Content API Key (回车则与 Chat 相同): " CONTENT_API_KEY
echo ""
if [ -z "$CONTENT_API_KEY" ]; then
    CONTENT_API_KEY="$CHAT_API_KEY"
    echo "  -> 使用 Chat API Key"
fi

# JWT Secret
echo ""
echo "--- JWT 配置 ---"
JWT_SECRET=$(openssl rand -base64 32 2>/dev/null || head -c 32 /dev/urandom | base64)
echo "JWT Secret 已自动生成: ${JWT_SECRET:0:10}..."

# 创建 Secret
echo ""
echo "=============================================="
echo "创建 Secret: $SECRET_NAME"
echo "=============================================="

kubectl create secret generic "$SECRET_NAME" \
  --namespace "$NAMESPACE" \
  --from-literal=chat-api-key="$CHAT_API_KEY" \
  --from-literal=catalog-api-key="$CATALOG_API_KEY" \
  --from-literal=content-api-key="$CONTENT_API_KEY" \
  --from-literal=jwt-secret="$JWT_SECRET" \
  --dry-run=client -o yaml | kubectl apply -f -

echo ""
echo "Secret '$SECRET_NAME' 已创建/更新"
echo ""
echo "包含的 Key："
echo "   - chat-api-key      (Chat API Key)"
echo "   - catalog-api-key   (Catalog API Key)"
echo "   - content-api-key   (Content API Key)"
echo "   - jwt-secret        (JWT 签名密钥)"
echo ""
echo "部署/更新应用："
echo "   helm upgrade opendeepwiki charts/opendeepwiki -n $NAMESPACE -f config/values-k3s.yaml"
echo "=============================================="