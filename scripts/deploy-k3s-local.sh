#!/bin/bash
# K3s 本地环境一键部署脚本
# 使用方式: bash scripts/deploy-k3s-local.sh
# 前提: WSL2 CentOS 7.9 已安装 Docker

set -e

echo "=============================================="
echo "K3s 本地开发环境一键部署"
echo "=============================================="

# ==========================================
# 1. 安装 K3s
# ==========================================
echo ""
echo "[1/4] 安装 K3s..."
if command -v k3s &>/dev/null; then
    echo "  K3s 已安装，跳过"
else
    curl -sfL https://rancher.io/install-k3s | sh -s - --cluster-init --disable=servicelb
    echo "  K3s 安装完成"
fi

# 等待 K3s 就绪
echo "  等待 K3s 就绪..."
for i in $(seq 1 30); do
    if kubectl get nodes 2>/dev/null | grep -q " Ready"; then
        break
    fi
    sleep 2
done
kubectl get nodes

# ==========================================
# 2. 安装 Kubernetes Dashboard
# ==========================================
echo ""
echo "[2/4] 安装 Kubernetes Dashboard..."
kubectl apply -f https://raw.githubusercontent.com/kubernetes/dashboard/v2.7.0/aio/deploy/recommended.yaml
kubectl wait --for=condition=ready pod -n kubernetes-dashboard -l k8s-app=kubernetes-dashboard --timeout=60s
echo "  Dashboard 安装完成"

# ==========================================
# 3. 部署 K8s Gateway（Helm）
# ==========================================
echo ""
echo "[3/4] 部署 K8s Gateway..."
helm upgrade --install k8s-gateway charts/k8s-gateway

# 修补 Dashboard 使用管理员账号
kubectl patch deployment kubernetes-dashboard -n kubernetes-dashboard \
  -p '{"spec":{"template":{"spec":{"serviceAccountName":"admin-user"}}}}'

echo "  K8s Gateway 部署完成"

# ==========================================
# 4. 部署 OpenDeepWiki
# ==========================================
echo ""
echo "[4/4] 部署 OpenDeepWiki..."

# 创建命名空间
kubectl create namespace opendeepwiki --dry-run=client -o yaml | kubectl apply -f -

# 创建 Secret（交互式）
if ! kubectl get secret opendeepwiki-secrets -n opendeepwiki &>/dev/null; then
    bash scripts/create-opendeepwiki-secret.sh
fi

# Helm 部署
helm upgrade --install opendeepwiki charts/opendeepwiki \
  -n opendeepwiki \
  -f config/values-k3s.yaml

echo "  OpenDeepWiki 部署完成"

# ==========================================
# 验证
# ==========================================
echo ""
echo "验证部署..."
echo ""

# 等待 Pod 就绪
kubectl wait --for=condition=ready pod -n opendeepwiki -l app.kubernetes.io/name=opendeepwiki --timeout=120s 2>/dev/null || true

echo "=== Pod 状态 ==="
kubectl get pods -A | grep -E "gateway|dashboard|opendeepwiki|traefik"

echo ""
echo "=== 路由资源 ==="
kubectl get ingress,ingressroute,middleware,serverstransport --all-namespaces

echo ""
echo "=== 连通性测试 ==="
echo -n "  Gateway  (8880): " && curl -s -H "Host: local.gateway.com" http://localhost:8880/ -o /dev/null -w "%{http_code}" 2>/dev/null || echo "失败"
echo ""
echo -n "  Wiki     (8880): " && curl -s -H "Host: local.wiki.com" http://localhost:8880/api/system/version -o /dev/null -w "%{http_code}" 2>/dev/null || echo "失败"
echo ""
echo -n "  Dashboard(8443): " && curl -sk -H "Host: local.dashboard.com" https://localhost:8443/ -o /dev/null -w "%{http_code}" 2>/dev/null || echo "失败"
echo ""

echo ""
echo "=============================================="
echo "部署完成！"
echo ""
echo "访问地址："
echo "  http://local.gateway.com:8880       - 网关首页"
echo "  https://local.dashboard.com:8443     - K8s Dashboard"
echo "  http://local.wiki.com:8880          - OpenDeepWiki"
echo ""
echo "Dashboard Token:"
echo "  kubectl get secret admin-user-token -n kubernetes-dashboard -o jsonpath='{.data.token}' | base64 -d"
echo "=============================================="