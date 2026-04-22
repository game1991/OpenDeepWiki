#!/bin/bash
# deploy-opendeepwiki.sh - OpenDeepWiki 迁移友好部署脚本

set -e

NAMESPACE="opendeepwiki"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# 颜色输出
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# 检查依赖
check_dependencies() {
    log_info "检查依赖..."

    if ! command -v kubectl &> /dev/null; then
        log_error "kubectl 未安装"
        exit 1
    fi

    if ! command -v helm &> /dev/null; then
        log_error "helm 未安装"
        exit 1
    fi

    log_info "依赖检查通过"
}

# 检查 Minikube 状态
check_minikube() {
    log_info "检查 Minikube 状态..."

    if ! minikube status &> /dev/null; then
        log_error "Minikube 未运行，请先启动 Minikube"
        log_info "运行: minikube start --driver=docker --image-mirror-country=cn"
        exit 1
    fi

    log_info "Minikube 运行正常"
}

# 创建命名空间
create_namespace() {
    log_info "创建命名空间: $NAMESPACE"

    if kubectl get namespace $NAMESPACE &> /dev/null; then
        log_warn "命名空间 $NAMESPACE 已存在"
    else
        kubectl create namespace $NAMESPACE
        log_info "命名空间创建成功"
    fi
}

# 创建 Secrets
create_secrets() {
    log_info "创建 Secrets..."

    # 检查 Secret 是否已存在
    if kubectl get secret opendeepwiki-secrets -n $NAMESPACE &> /dev/null; then
        log_warn "Secret opendeepwiki-secrets 已存在，跳过创建"
        log_info "如需更新 Secret，请运行: kubectl delete secret opendeepwiki-secrets -n $NAMESPACE"
        return
    fi

    # 读取 API Key
    read -p "请输入 OpenAI API Key (用于对话): " CHAT_API_KEY
    read -p "请输入 OpenAI API Key (用于目录生成，可直接回车使用相同key): " CATALOG_API_KEY
    read -p "请输入 OpenAI API Key (用于内容生成，可直接回车使用相同key): " CONTENT_API_KEY

    # 如果目录和内容 key 为空，使用对话 key
    CATALOG_API_KEY=${CATALOG_API_KEY:-$CHAT_API_KEY}
    CONTENT_API_KEY=${CONTENT_API_KEY:-$CHAT_API_KEY}

    # 生成 JWT Secret
    JWT_SECRET=$(openssl rand -base64 32)

    kubectl create secret generic opendeepwiki-secrets \
        --namespace $NAMESPACE \
        --from-literal=chat-api-key="$CHAT_API_KEY" \
        --from-literal=catalog-api-key="$CATALOG_API_KEY" \
        --from-literal=content-api-key="$CONTENT_API_KEY" \
        --from-literal=jwt-secret="$JWT_SECRET"

    log_info "Secrets 创建成功"
}

# 部署应用
deploy_app() {
    log_info "部署 OpenDeepWiki..."

    cd "$SCRIPT_DIR/../.."

    # 检查是否已部署
    if helm list -n $NAMESPACE | grep -q opendeepwiki; then
        log_warn "OpenDeepWiki 已部署，执行升级..."
        helm upgrade opendeepwiki ./charts/opendeepwiki \
            --namespace $NAMESPACE \
            --values "$SCRIPT_DIR/opendeepwiki-migrate-friendly.yaml"
    else
        helm install opendeepwiki ./charts/opendeepwiki \
            --namespace $NAMESPACE \
            --values "$SCRIPT_DIR/opendeepwiki-migrate-friendly.yaml"
    fi

    log_info "部署完成，等待 Pod 启动..."
}

# 部署网关
deploy_gateway() {
    log_info "部署网关..."

    local GATEWAY_DIR="$SCRIPT_DIR/../docs/k8s-gateway"

    # 从本地 HTML 文件生成 ConfigMap
    kubectl create configmap gateway-html \
        --from-file=index.html="$GATEWAY_DIR/html/index.html" \
        --dry-run=client -o yaml | kubectl apply -f -

    # 部署 Pod 和 Service
    kubectl apply -f "$GATEWAY_DIR/yamls/01-gateway-pod.yaml"
    kubectl apply -f "$GATEWAY_DIR/yamls/02-gateway-service.yaml"

    log_info "网关部署完成"
}

# 等待就绪
wait_for_ready() {
    log_info "等待后端 Pod 就绪..."
    kubectl wait --for=condition=ready pod \
        -l app.kubernetes.io/component=backend \
        -n $NAMESPACE \
        --timeout=300s || {
        log_error "后端 Pod 启动超时"
        log_info "查看日志: kubectl logs -n $NAMESPACE -l app.kubernetes.io/component=backend"
        exit 1
    }

    log_info "等待前端 Pod 就绪..."
    kubectl wait --for=condition=ready pod \
        -l app.kubernetes.io/component=frontend \
        -n $NAMESPACE \
        --timeout=120s

    log_info "所有 Pod 已就绪！"
}

# 显示访问信息
show_access_info() {
    log_info "部署完成！访问信息："
    echo ""
    echo "=========================================="
    echo "Pod 状态:"
    kubectl get pods -n $NAMESPACE
    echo ""
    echo "服务状态:"
    kubectl get svc -n $NAMESPACE
    echo ""
    echo "访问方式:"
    echo "1. 使用 kubectl port-forward:"
    echo "   kubectl port-forward -n $NAMESPACE svc/opendeepwiki-frontend 3000:3000"
    echo "   然后访问: http://localhost:3000"
    echo ""
    echo "2. 使用 Minikube service:"
    echo "   minikube service opendeepwiki-frontend -n $NAMESPACE"
    echo ""
    echo "默认账号:"
    echo "   邮箱: admin@routin.ai"
    echo "   密码: Admin@123"
    echo "=========================================="
    echo ""
    log_info "备份脚本位置: $SCRIPT_DIR/backup-opendeepwiki.sh"
    log_info "恢复脚本位置: $SCRIPT_DIR/restore-opendeepwiki.sh"
}

# 主函数
main() {
    log_info "开始部署 OpenDeepWiki (迁移友好版)..."

    check_dependencies
    check_minikube
    create_namespace
    create_secrets
    deploy_app
    deploy_gateway
    wait_for_ready
    show_access_info

    log_info "部署完成！"
}

# 运行
main "$@"
