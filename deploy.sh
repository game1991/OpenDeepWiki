#!/bin/bash
# deploy.sh - OpenDeepWiki 统一部署脚本
#
# 用法:
#   ./deploy.sh install    # 全新安装
#   ./deploy.sh upgrade    # 升级现有部署
#   ./deploy.sh uninstall  # 完全卸载
#   ./deploy.sh status     # 查看状态
#   ./deploy.sh logs       # 查看日志
#   ./deploy.sh restart    # 重启服务

set -e

# ==========================================
# 配置变量
# ==========================================
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CHART_NAME="opendeepwiki"
CHART_DIR="$SCRIPT_DIR/charts/opendeepwiki"
VALUES_FILE="${VALUES_FILE:-$SCRIPT_DIR/config/values-k3s.yaml}"
NAMESPACE="${NAMESPACE:-opendeepwiki}"
SECRET_NAME="opendeepwiki-secrets"

# 颜色输出
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# ==========================================
# 日志函数
# ==========================================
log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }
log_step() { echo -e "${BLUE}[STEP]${NC} $1"; }

# ==========================================
# 检查函数
# ==========================================
check_dependencies() {
    log_step "检查依赖..."

    local deps=("kubectl" "helm")
    for dep in "${deps[@]}"; do
        if ! command -v "$dep" &> /dev/null; then
            log_error "$dep 未安装"
            exit 1
        fi
    done

    log_info "依赖检查通过 ✓"
}

check_kubernetes() {
    log_step "检查 Kubernetes 连接..."

    if ! kubectl cluster-info &> /dev/null; then
        log_error "无法连接到 Kubernetes 集群"
        exit 1
    fi

    log_info "Kubernetes 连接正常 ✓"
}

check_secret() {
    log_step "检查 Secret..."

    if ! kubectl get secret "$SECRET_NAME" -n "$NAMESPACE" &> /dev/null; then
        log_warn "Secret '$SECRET_NAME' 不存在"
        log_info "请先创建 Secret:"
        echo ""
        echo "  方式1 - 使用交互式脚本:"
        echo "    bash scripts/create-opendeepwiki-secret.sh $NAMESPACE"
        echo ""
        echo "  方式2 - 手动创建:"
        echo "    kubectl create secret generic $SECRET_NAME \\"
        echo "      --namespace $NAMESPACE \\"
        echo "      --from-literal=chat-api-key='your-api-key' \\"
        echo "      --from-literal=jwt-secret-key='your-jwt-secret'"
        echo ""
        return 1
    fi

    log_info "Secret 检查通过 ✓"
    return 0
}

# ==========================================
# 安装函数
# ==========================================
install() {
    log_step "开始安装 OpenDeepWiki..."

    check_dependencies
    check_kubernetes

    # 创建命名空间
    if ! kubectl get namespace "$NAMESPACE" &> /dev/null; then
        log_info "创建命名空间: $NAMESPACE"
        kubectl create namespace "$NAMESPACE"
    else
        log_warn "命名空间 $NAMESPACE 已存在"
    fi

    # 检查 Secret
    if ! check_secret; then
        read -p "是否现在创建 Secret? [y/N] " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            bash "$SCRIPT_DIR/scripts/create-opendeepwiki-secret.sh" "$NAMESPACE"
        else
            log_error "缺少必要的 Secret，安装中止"
            exit 1
        fi
    fi

    # 检查 values 文件
    if [ ! -f "$VALUES_FILE" ]; then
        log_error "Values 文件不存在: $VALUES_FILE"
        exit 1
    fi

    # 验证 Helm chart
    log_step "验证 Helm chart..."
    if ! helm lint "$CHART_DIR" --quiet; then
        log_error "Helm chart 验证失败"
        exit 1
    fi
    log_info "Helm chart 验证通过 ✓"

    # 安装
    log_step "安装 OpenDeepWiki..."
    helm upgrade --install "$CHART_NAME" "$CHART_DIR" \
        --namespace "$NAMESPACE" \
        --values "$VALUES_FILE" \
        --wait \
        --timeout 10m

    log_info "安装完成！"
    show_status
}

# ==========================================
# 升级函数
# ==========================================
upgrade() {
    log_step "开始升级 OpenDeepWiki..."

    check_dependencies
    check_kubernetes

    # 检查是否已安装
    if ! helm list -n "$NAMESPACE" | grep -q "$CHART_NAME"; then
        log_error "OpenDeepWiki 未安装，请先运行: $0 install"
        exit 1
    fi

    # 检查 Secret
    check_secret || exit 1

    # 备份当前 release values
    log_step "备份当前配置..."
    local backup_file="/tmp/${CHART_NAME}-backup-$(date +%Y%m%d-%H%M%S).yaml"
    helm get values "$CHART_NAME" -n "$NAMESPACE" > "$backup_file"
    log_info "配置已备份到: $backup_file"

    # 升级
    log_step "升级 OpenDeepWiki..."
    helm upgrade "$CHART_NAME" "$CHART_DIR" \
        --namespace "$NAMESPACE" \
        --values "$VALUES_FILE" \
        --wait \
        --timeout 10m

    log_info "升级完成！"
    show_status
}

# ==========================================
# 卸载函数
# 用法:
#   uninstall        - 仅卸载应用（保留数据和密钥）
#   uninstall --all  - 完全卸载（包括 PVC 和 Secret）
# ==========================================
uninstall() {
    local purge_mode=false

    # 检查是否有 --all 或 --purge 参数
    for arg in "$@"; do
        if [[ "$arg" == "--all" || "$arg" == "--purge" ]]; then
            purge_mode=true
            break
        fi
    done

    if [[ "$purge_mode" == true ]]; then
        log_step "开始完全卸载 OpenDeepWiki（将删除所有数据）..."
        echo ""
        log_warn "⚠️  警告：此操作将永久删除以下资源："
        echo "    - OpenDeepWiki 应用 (Deployment, Service, Ingress)"
        echo "    - 数据库持久化卷 (PVC) - 包含所有 Wiki 数据"
        echo "    - 密钥 (Secret) - API Key 等敏感信息"
        echo ""
        read -p "确定要完全卸载并删除所有数据吗? 此操作不可恢复! [yes/N] " -r
        echo
        if [[ ! $REPLY =~ ^[Yy][Ee][Ss]$ ]]; then
            log_info "取消卸载"
            exit 0
        fi
    else
        log_step "开始卸载 OpenDeepWiki..."
        echo ""
        log_info "此操作将："
        echo "    ✓ 删除 OpenDeepWiki 应用 (Deployment, Pod, Service, Ingress)"
        echo "    ✗ 保留数据库持久化卷 (PVC) - 你的 Wiki 数据不会丢失"
        echo "    ✗ 保留密钥 (Secret) - API Key 等配置不会丢失"
        echo ""
        log_info "如需同时删除数据和密钥，请使用: $0 uninstall --all"
        echo ""
        read -p "确定要卸载 OpenDeepWiki 吗? [y/N] " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            log_info "取消卸载"
            exit 0
        fi
    fi

    check_dependencies

    # 卸载 Helm release
    if helm list -n "$NAMESPACE" | grep -q "$CHART_NAME"; then
        log_info "卸载 Helm release..."
        helm uninstall "$CHART_NAME" -n "$NAMESPACE"
    else
        log_warn "Helm release 不存在"
    fi

    # 完全清理模式：删除 PVC
    if [[ "$purge_mode" == true ]]; then
        log_warn "删除持久化数据 (PVC)..."
        kubectl delete pvc -n "$NAMESPACE" -l "app.kubernetes.io/name=$CHART_NAME" --ignore-not-found=true
        log_info "PVC 已删除"

        log_warn "删除密钥 (Secret)..."
        kubectl delete secret "$SECRET_NAME" -n "$NAMESPACE" --ignore-not-found=true
        log_info "Secret 已删除"

        # 询问是否删除命名空间
        read -p "是否删除命名空间 ($NAMESPACE)? [y/N] " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            log_warn "删除命名空间..."
            kubectl delete namespace "$NAMESPACE" --ignore-not-found=true
            log_info "命名空间已删除"
        fi

        log_info "完全卸载完成！所有数据已清理"
    else
        log_info "普通卸载完成！"
        log_info "数据已保留，可通过 './deploy.sh install' 重新部署并恢复数据"
    fi
}

# ==========================================
# 状态函数
# ==========================================
show_status() {
    log_step "OpenDeepWiki 状态"
    echo ""

    # Helm release 状态
    echo "========================================"
    echo "Helm Release 状态:"
    echo "========================================"
    helm list -n "$NAMESPACE" --filter "$CHART_NAME" -o table 2>/dev/null || echo "未安装"
    echo ""

    # Pod 状态
    echo "========================================"
    echo "Pod 状态:"
    echo "========================================"
    kubectl get pods -n "$NAMESPACE" -o wide 2>/dev/null || echo "无 Pod"
    echo ""

    # Service 状态
    echo "========================================"
    echo "Service 状态:"
    echo "========================================"
    kubectl get svc -n "$NAMESPACE" 2>/dev/null || echo "无 Service"
    echo ""

    # Ingress 状态
    echo "========================================"
    echo "Ingress 状态:"
    echo "========================================"
    kubectl get ingress -n "$NAMESPACE" 2>/dev/null || echo "无 Ingress"
    echo ""

    # PVC 状态
    echo "========================================"
    echo "PVC 状态:"
    echo "========================================"
    kubectl get pvc -n "$NAMESPACE" 2>/dev/null || echo "无 PVC"
    echo ""

    # API 健康检查
    echo "========================================"
    echo "API 健康检查:"
    echo "========================================"
    if kubectl get deployment "$CHART_NAME-backend" -n "$NAMESPACE" &> /dev/null; then
        local health
        health=$(kubectl exec -n "$NAMESPACE" deployment/"$CHART_NAME-backend" \
            -- curl -s http://localhost:8080/api/system/version 2>/dev/null || echo "{}" )
        echo "后端 API: $health"
    else
        echo "后端未部署"
    fi
    echo ""

    # 访问信息
    echo "========================================"
    echo "访问方式:"
    echo "========================================"
    echo "1. Ingress (需配置 hosts):"
    local ingress_ip
    ingress_ip=$(kubectl get ingress -n "$NAMESPACE" "$CHART_NAME" -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || echo "")
    if [ -n "$ingress_ip" ]; then
        echo "   echo '$ingress_ip local.wiki.com' | sudo tee -a /etc/hosts"
        echo "   curl http://local.wiki.com/api/system/version"
    else
        echo "   Ingress IP 未分配，请检查 ingress controller"
    fi
    echo ""
    echo "2. Port-forward:"
    echo "   kubectl port-forward -n $NAMESPACE svc/$CHART_NAME-frontend 3000:3000"
    echo "   访问: http://localhost:3000"
    echo ""
    echo "3. 后端 API:"
    echo "   kubectl port-forward -n $NAMESPACE svc/$CHART_NAME-backend 8080:8080"
    echo "   curl http://localhost:8080/api/system/version"
    echo ""

    # 默认账号
    echo "========================================"
    echo "默认管理员账号:"
    echo "========================================"
    echo "   首次启动后请查看 Pod 日志获取默认凭据"
    echo "   ⚠️  首次登录后请立即修改密码！"
    echo ""
}

# ==========================================
# 日志函数
# ==========================================
show_logs() {
    local component="${1:-backend}"
    local lines="${2:-100}"

    log_step "查看 $component 日志 (最近 $lines 行)..."

    kubectl logs -n "$NAMESPACE" -l "app.kubernetes.io/component=$component" --tail="$lines" -f
}

# ==========================================
# 重启函数
# ==========================================
restart() {
    log_step "重启 OpenDeepWiki..."

    kubectl rollout restart deployment/"$CHART_NAME-backend" -n "$NAMESPACE"
    kubectl rollout restart deployment/"$CHART_NAME-frontend" -n "$NAMESPACE"

    log_info "重启命令已发送，等待滚动更新完成..."

    kubectl rollout status deployment/"$CHART_NAME-backend" -n "$NAMESPACE" --timeout=5m
    kubectl rollout status deployment/"$CHART_NAME-frontend" -n "$NAMESPACE" --timeout=5m

    log_info "重启完成！"
}

# ==========================================
# 帮助函数
# ==========================================
show_help() {
    cat << EOF
OpenDeepWiki 部署脚本

用法: $0 <command> [options]

命令:
  install      全新安装 OpenDeepWiki
               环境变量:
                 VALUES_FILE    指定 values 文件 (默认: config/values-k3s.yaml)
                 NAMESPACE      指定命名空间 (默认: opendeepwiki)

  upgrade      升级现有部署
               会自动备份当前配置

  uninstall    卸载 OpenDeepWiki
               默认保留数据 (PVC) 和密钥 (Secret)
               选项:
                 --all, --purge  完全卸载（删除数据和密钥）

  status       查看部署状态
               包括 Pod、Service、Ingress、API 健康等

  logs         查看日志
               用法: $0 logs [backend|frontend] [行数]
               默认查看 backend 最近 100 行

  restart      重启服务
               执行滚动更新

  help         显示此帮助信息

示例:
  # 使用默认配置安装
  $0 install

  # 使用生产环境配置安装
  VALUES_FILE=config/values-production.yaml $0 install

  # 安装到指定命名空间
  NAMESPACE=mywiki $0 install

  # 查看前端日志
  $0 logs frontend 200

  # 升级
  $0 upgrade

  # 普通卸载（保留数据）
  $0 uninstall

  # 完全卸载（删除所有数据）
  $0 uninstall --all

EOF
}

# ==========================================
# 主函数
# ==========================================
main() {
    local command="${1:-help}"

    case "$command" in
        install)
            install
            ;;
        upgrade)
            upgrade
            ;;
        uninstall)
            shift
            uninstall "$@"
            ;;
        status)
            show_status
            ;;
        logs)
            show_logs "${2:-backend}" "${3:-100}"
            ;;
        restart)
            restart
            ;;
        help|--help|-h)
            show_help
            ;;
        *)
            log_error "未知命令: $command"
            show_help
            exit 1
            ;;
    esac
}

main "$@"
