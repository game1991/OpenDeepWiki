#!/bin/bash
# restore-opendeepwiki.sh - OpenDeepWiki 独立恢复脚本
# 用于从备份目录恢复数据到新部署的环境

set -e

NAMESPACE="${1:-opendeepwiki}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# 颜色输出
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

log_step() {
    echo -e "${BLUE}[STEP]${NC} $1"
}

# 使用说明
usage() {
    echo "OpenDeepWiki 恢复脚本"
    echo ""
    echo "用法:"
    echo "  $0 [备份目录] [命名空间]"
    echo ""
    echo "参数:"
    echo "  备份目录    备份目录或压缩包路径 (默认: 自动查找最新备份)"
    echo "  命名空间    Kubernetes 命名空间 (默认: opendeepwiki)"
    echo ""
    echo "示例:"
    echo "  $0                                          # 使用最新备份"
    echo "  $0 ./backups/opendeepwiki-backup-20240101  # 指定备份目录"
    echo "  $0 ./backups/opendeepwiki-backup-20240101.tar.gz  # 指定压缩包"
    echo "  $0 ./backups/opendeepwiki-backup-20240101 my-namespace  # 指定命名空间"
    exit 1
}

# 查找最新备份
find_latest_backup() {
    local BACKUP_DIR="${SCRIPT_DIR}/../backups"

    if [ ! -d "$BACKUP_DIR" ]; then
        log_error "备份目录不存在: $BACKUP_DIR"
        exit 1
    fi

    local LATEST=$(ls -1td "$BACKUP_DIR"/opendeepwiki-backup-* 2>/dev/null | head -1)

    if [ -z "$LATEST" ]; then
        log_error "未找到备份文件"
        exit 1
    fi

    echo "$LATEST"
}

# 解压备份包
extract_backup() {
    local BACKUP_PATH="$1"

    if [[ "$BACKUP_PATH" == *.tar.gz ]]; then
        log_info "解压备份包: $BACKUP_PATH"
        local EXTRACT_DIR="${BACKUP_PATH%.tar.gz}"

        if [ -d "$EXTRACT_DIR" ]; then
            log_warn "解压目录已存在: $EXTRACT_DIR"
            read -p "是否覆盖? (y/N): " CONFIRM
            if [[ ! $CONFIRM =~ ^[Yy]$ ]]; then
                log_error "用户取消"
                exit 1
            fi
            rm -rf "$EXTRACT_DIR"
        fi

        tar xzf "$BACKUP_PATH" -C "$(dirname "$BACKUP_PATH")"
        BACKUP_PATH="$EXTRACT_DIR"
    fi

    echo "$BACKUP_PATH"
}

# 检查环境
check_environment() {
    log_step "检查环境..."

    if ! command -v kubectl &> /dev/null; then
        log_error "kubectl 未安装"
        exit 1
    fi

    if ! kubectl get namespace $NAMESPACE &> /dev/null; then
        log_error "命名空间 $NAMESPACE 不存在"
        log_info "请先部署 OpenDeepWiki: ./deploy-opendeepwiki.sh"
        exit 1
    fi

    # 等待 Pod 就绪
    log_info "等待后端 Pod 就绪..."
    if ! kubectl wait --for=condition=ready pod \
        -l app.kubernetes.io/component=backend \
        -n $NAMESPACE \
        --timeout=300s 2>/dev/null; then
        log_error "后端 Pod 未就绪"
        exit 1
    fi

    log_info "环境检查通过"
}

# 确认恢复
confirm_restore() {
    local BACKUP_PATH="$1"

    echo ""
    echo "=========================================="
    echo "恢复确认"
    echo "=========================================="
    log_info "备份来源: $BACKUP_PATH"
    log_info "目标命名空间: $NAMESPACE"
    echo ""
    log_warn "此操作将覆盖现有数据！"
    echo ""
    read -p "确认继续? (yes/no): " CONFIRM

    if [ "$CONFIRM" != "yes" ]; then
        log_error "用户取消恢复"
        exit 1
    fi
}

# 恢复配置
restore_configs() {
    log_step "恢复配置..."

    # 应用 ConfigMap
    if [ -f "$BACKUP_DIR/configmaps.yaml" ]; then
        kubectl apply -f "$BACKUP_DIR/configmaps.yaml"
        log_info "ConfigMap 已恢复"
    else
        log_warn "未找到 configmaps.yaml"
    fi

    # 恢复 Secret（询问用户）
    if [ -f "$BACKUP_DIR/secrets.yaml" ]; then
        echo ""
        log_warn "发现 Secrets 备份"
        echo "选项:"
        echo "  1. 恢复备份的 Secrets（使用原 API Key）"
        echo "  2. 跳过（使用当前环境的 Secrets）"
        echo "  3. 创建新的 Secrets"
        read -p "请选择 (1/2/3): " SECRET_CHOICE

        case $SECRET_CHOICE in
            1)
                kubectl apply -f "$BACKUP_DIR/secrets.yaml"
                log_info "Secrets 已恢复"
                ;;
            2)
                log_info "跳过 Secrets 恢复"
                ;;
            3)
                log_info "请运行以下命令创建新的 Secrets:"
                echo "  kubectl create secret generic opendeepwiki-secrets -n $NAMESPACE \\"
                echo "    --from-literal=chat-api-key='新key' \\"
                echo "    --from-literal=catalog-api-key='新key' \\"
                echo "    --from-literal=content-api-key='新key' \\"
                echo "    --from-literal=jwt-secret='$(openssl rand -base64 32)'"
                read -p "创建完成后按回车继续..."
                ;;
            *)
                log_warn "无效选择，跳过 Secrets 恢复"
                ;;
        esac
    fi
}

# 恢复数据
restore_data() {
    log_step "恢复数据文件..."

    if [ ! -d "$BACKUP_DIR/data" ]; then
        log_error "备份数据目录不存在: $BACKUP_DIR/data"
        exit 1
    fi

    # 获取 Pod 名称
    POD_NAME=$(kubectl get pod -n $NAMESPACE -l app.kubernetes.io/component=backend -o jsonpath='{.items[0].metadata.name}')
    log_info "目标 Pod: $POD_NAME"

    # 备份当前数据（防止意外）
    log_info "备份当前 Pod 数据..."
    kubectl exec -n $NAMESPACE $POD_NAME -- sh -c 'cp -r /data /data.backup.$(date +%Y%m%d_%H%M%S) 2>/dev/null || echo "无法创建备份"'

    # 检查数据库文件
    if [ -f "$BACKUP_DIR/data/opendeepwiki.db" ]; then
        DB_SIZE=$(du -h "$BACKUP_DIR/data/opendeepwiki.db" | cut -f1)
        log_info "发现数据库文件: $DB_SIZE"
    fi

    # 复制数据到 Pod
    log_info "复制备份数据到 Pod..."
    kubectl cp "$BACKUP_DIR/data" "$NAMESPACE/$POD_NAME:/data"

    # 设置正确权限
    log_info "设置文件权限..."
    kubectl exec -n $NAMESPACE $POD_NAME -- chown -R 1000:1000 /data

    # 验证
    log_info "验证数据..."
    kubectl exec -n $NAMESPACE $POD_NAME -- ls -la /data/

    log_info "数据恢复完成"
}

# 重启 Pod
restart_pods() {
    log_step "重启 Pod 以应用更改..."

    kubectl rollout restart deployment/opendeepwiki-backend -n $NAMESPACE
    kubectl rollout restart deployment/opendeepwiki-frontend -n $NAMESPACE

    # 等待就绪
    log_info "等待后端 Pod 就绪..."
    kubectl wait --for=condition=ready pod \
        -l app.kubernetes.io/component=backend \
        -n $NAMESPACE \
        --timeout=300s

    log_info "Pod 已重启并就绪"
}

# 验证恢复
verify_restore() {
    log_step "验证恢复结果..."

    POD_NAME=$(kubectl get pod -n $NAMESPACE -l app.kubernetes.io/component=backend -o jsonpath='{.items[0].metadata.name}')

    # 检查数据库
    if kubectl exec -n $NAMESPACE $POD_NAME -- test -f /data/opendeepwiki.db; then
        DB_SIZE=$(kubectl exec -n $NAMESPACE $POD_NAME -- du -h /data/opendeepwiki.db | cut -f1)
        log_info "数据库文件存在: $DB_SIZE"
    else
        log_error "数据库文件不存在！"
        return 1
    fi

    # 检查知识库目录
    REPO_COUNT=$(kubectl exec -n $NAMESPACE $POD_NAME -- find /data -type d 2>/dev/null | wc -l)
    log_info "知识库目录数量: $((REPO_COUNT - 1))"

    # 测试健康检查
    log_info "测试后端健康..."
    kubectl exec -n $NAMESPACE $POD_NAME -- wget -qO- http://localhost:8080/health || true

    log_info "验证通过！"
}

# 显示恢复后信息
show_post_restore_info() {
    echo ""
    echo "=========================================="
    echo "           恢复完成"
    echo "=========================================="
    echo ""
    kubectl get pods -n $NAMESPACE
    echo ""
    log_info "访问方式:"
    echo "  kubectl port-forward -n $NAMESPACE svc/opendeepwiki-frontend 3000:3000"
    echo "  然后访问: http://localhost:3000"
    echo ""
    log_info "如需更新 API Key:"
    echo "  kubectl set secret opendeepwiki-secrets -n $NAMESPACE \\"
    echo "    --from-literal=chat-api-key='新key'"
    echo ""
    log_info "查看日志:"
    echo "  kubectl logs -n $NAMESPACE -l app.kubernetes.io/component=backend -f"
    echo "=========================================="
}

# 主函数
main() {
    # 解析参数
    if [ "$1" == "-h" ] || [ "$1" == "--help" ]; then
        usage
    fi

    local BACKUP_INPUT="${1:-}"
    local NAMESPACE_INPUT="${2:-opendeepwiki}"

    if [ -n "$NAMESPACE_INPUT" ]; then
        NAMESPACE="$NAMESPACE_INPUT"
    fi

    # 确定备份路径
    if [ -z "$BACKUP_INPUT" ]; then
        log_info "未指定备份，查找最新备份..."
        BACKUP_DIR=$(find_latest_backup)
    else
        BACKUP_INPUT="$(cd "$(dirname "$BACKUP_INPUT")" && pwd)/$(basename "$BACKUP_INPUT")"
        BACKUP_DIR=$(extract_backup "$BACKUP_INPUT")
    fi

    log_info "使用备份: $BACKUP_DIR"

    # 执行恢复流程
    check_environment
    confirm_restore "$BACKUP_DIR"
    restore_configs
    restore_data
    restart_pods
    verify_restore
    show_post_restore_info

    log_info "恢复完成！"
}

# 运行
main "$@"
