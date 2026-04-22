#!/bin/bash
# migrate-opendeepwiki.sh - OpenDeepWiki 一键迁移脚本
# 从源服务器备份，传输到目标服务器，并自动恢复

set -e

# 配置
SOURCE_HOST=""
SOURCE_USER=""
SOURCE_NAMESPACE="opendeepwiki"
TARGET_NAMESPACE="opendeepwiki"
SSH_KEY=""
BACKUP_LOCAL_DIR="./migration-backups"

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
    echo "OpenDeepWiki 一键迁移脚本"
    echo ""
    echo "功能: 从源服务器备份 -> 传输到本地 -> 部署到目标服务器 -> 恢复数据"
    echo ""
    echo "用法:"
    echo "  $0 --source user@host [--ssh-key path] [--source-ns ns] [--target-ns ns]"
    echo ""
    echo "参数:"
    echo "  --source        源服务器地址 (格式: user@host，必需)"
    echo "  --ssh-key       SSH 私钥路径 (可选)"
    echo "  --source-ns     源服务器命名空间 (默认: opendeepwiki)"
    echo "  --target-ns     目标服务器命名空间 (默认: opendeepwiki)"
    echo "  -h, --help      显示帮助"
    echo ""
    echo "示例:"
    echo "  $0 --source admin@old-server.com"
    echo "  $0 --source admin@old-server.com --ssh-key ~/.ssh/id_rsa"
    echo ""
    echo "注意:"
    echo "  1. 源服务器需要有 kubectl 和 helm"
    echo "  2. 目标服务器需要已启动 Minikube"
    echo "  3. 迁移过程中会提示确认"
    exit 1
}

# 解析参数
parse_args() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            --source)
                SOURCE_HOST="$2"
                shift 2
                ;;
            --ssh-key)
                SSH_KEY="$2"
                shift 2
                ;;
            --source-ns)
                SOURCE_NAMESPACE="$2"
                shift 2
                ;;
            --target-ns)
                TARGET_NAMESPACE="$2"
                shift 2
                ;;
            -h|--help)
                usage
                ;;
            *)
                log_error "未知参数: $1"
                usage
                ;;
        esac
    done

    if [ -z "$SOURCE_HOST" ]; then
        log_error "必须指定源服务器地址 (--source)"
        usage
    fi

    SOURCE_USER=$(echo "$SOURCE_HOST" | cut -d'@' -f1)
    SOURCE_ADDR=$(echo "$SOURCE_HOST" | cut -d'@' -f2)
}

# SSH 命令
ssh_cmd() {
    if [ -n "$SSH_KEY" ]; then
        ssh -i "$SSH_KEY" -o StrictHostKeyChecking=no "$@"
    else
        ssh -o StrictHostKeyChecking=no "$@"
    fi
}

# SCP 命令
scp_cmd() {
    if [ -n "$SSH_KEY" ]; then
        scp -i "$SSH_KEY" -o StrictHostKeyChecking=no "$@"
    else
        scp -o StrictHostKeyChecking=no "$@"
    fi
}

# 检查源服务器
check_source() {
    log_step "检查源服务器..."

    if ! ssh_cmd "$SOURCE_HOST" "kubectl version" &> /dev/null; then
        log_error "无法连接到源服务器或 kubectl 不可用"
        exit 1
    fi

    if ! ssh_cmd "$SOURCE_HOST" "kubectl get namespace $SOURCE_NAMESPACE" &> /dev/null; then
        log_error "源服务器上命名空间 $SOURCE_NAMESPACE 不存在"
        exit 1
    fi

    log_info "源服务器连接正常"
}

# 在源服务器执行备份
backup_on_source() {
    log_step "在源服务器执行备份..."

    REMOTE_SCRIPT=$(ssh_cmd "$SOURCE_HOST" "mktemp")

    # 上传备份脚本
    scp_cmd "$(dirname "$0")/backup-opendeepwiki.sh" "$SOURCE_HOST:$REMOTE_SCRIPT"

    # 执行备份
    log_info "正在备份..."
    ssh_cmd "$SOURCE_HOST" "bash $REMOTE_SCRIPT"

    # 获取备份文件路径
    REMOTE_BACKUP=$(ssh_cmd "$SOURCE_HOST" "ls -1t ~/opendeepwiki/backups/opendeepwiki-backup-*.tar.gz 2>/dev/null | head -1")

    if [ -z "$REMOTE_BACKUP" ]; then
        log_error "源服务器备份失败"
        exit 1
    fi

    log_info "源服务器备份完成: $REMOTE_BACKUP"
}

# 传输备份到本地
transfer_backup() {
    log_step "传输备份到本地..."

    mkdir -p "$BACKUP_LOCAL_DIR"

    LOCAL_BACKUP="$BACKUP_LOCAL_DIR/$(basename "$REMOTE_BACKUP")"

    log_info "正在传输..."
    scp_cmd "$SOURCE_HOST:$REMOTE_BACKUP" "$LOCAL_BACKUP"

    # 计算传输大小
    BACKUP_SIZE=$(du -h "$LOCAL_BACKUP" | cut -f1)
    log_info "传输完成: $BACKUP_SIZE"
}

# 清理源服务器临时文件
cleanup_source() {
    log_step "清理源服务器..."
    ssh_cmd "$SOURCE_HOST" "rm -f $REMOTE_SCRIPT"
    log_info "清理完成"
}

# 检查目标服务器（本地）
check_target() {
    log_step "检查目标服务器（本地）..."

    if ! command -v kubectl &> /dev/null; then
        log_error "kubectl 未安装"
        exit 1
    fi

    if ! command -v helm &> /dev/null; then
        log_error "helm 未安装"
        exit 1
    fi

    if ! minikube status &> /dev/null; then
        log_error "Minikube 未运行"
        log_info "请先启动 Minikube: minikube start --driver=docker --image-mirror-country=cn"
        exit 1
    fi

    log_info "目标服务器检查通过"
}

# 部署到目标服务器
deploy_on_target() {
    log_step "部署 OpenDeepWiki 到目标服务器..."

    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

    # 运行部署脚本
    log_info "执行部署..."
    bash "$SCRIPT_DIR/deploy-opendeepwiki.sh"
}

# 恢复数据
restore_on_target() {
    log_step "恢复数据到目标服务器..."

    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

    # 运行恢复脚本
    bash "$SCRIPT_DIR/restore-opendeepwiki.sh" "$LOCAL_BACKUP" "$TARGET_NAMESPACE"
}

# 确认迁移
confirm_migration() {
    echo ""
    echo "=========================================="
    echo "           迁移确认"
    echo "=========================================="
    log_info "源服务器: $SOURCE_HOST"
    log_info "源命名空间: $SOURCE_NAMESPACE"
    log_info "目标命名空间: $TARGET_NAMESPACE"
    log_info "备份存储: $BACKUP_LOCAL_DIR"
    echo ""
    log_warn "迁移过程包括:"
    echo "  1. 在源服务器备份数据"
    echo "  2. 传输备份到本地"
    echo "  3. 在目标服务器部署 OpenDeepWiki"
    echo "  4. 恢复数据到目标服务器"
    echo ""
    read -p "确认开始迁移? (yes/no): " CONFIRM

    if [ "$CONFIRM" != "yes" ]; then
        log_error "用户取消迁移"
        exit 1
    fi
}

# 显示迁移结果
show_migration_result() {
    echo ""
    echo "=========================================="
    echo "           迁移完成"
    echo "=========================================="
    echo ""
    kubectl get pods -n $TARGET_NAMESPACE
    echo ""
    log_info "迁移完成！"
    log_info "备份文件保留在: $LOCAL_BACKUP"
    echo ""
    log_info "访问方式:"
    echo "  kubectl port-forward -n $TARGET_NAMESPACE svc/opendeepwiki-frontend 3000:3000"
    echo ""
    log_info "验证完成后，可以删除备份:"
    echo "  rm -rf $BACKUP_LOCAL_DIR"
    echo "=========================================="
}

# 主函数
main() {
    echo "=========================================="
    echo "    OpenDeepWiki 一键迁移工具"
    echo "=========================================="
    echo ""

    parse_args "$@"
    confirm_migration

    # 阶段 1: 源服务器
    check_source
    backup_on_source
    transfer_backup
    cleanup_source

    # 阶段 2: 目标服务器
    check_target
    deploy_on_target
    restore_on_target

    show_migration_result

    log_info "迁移完成！"
}

# 运行
main "$@"
