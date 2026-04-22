#!/bin/bash
# backup-opendeepwiki.sh - OpenDeepWiki 完整备份脚本
# 备份内容包括：数据文件、配置、Secrets

set -e

NAMESPACE="opendeepwiki"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKUP_BASE_DIR="${SCRIPT_DIR}/../backups"
BACKUP_NAME="opendeepwiki-backup-$(date +%Y%m%d_%H%M%S)"
BACKUP_DIR="${BACKUP_BASE_DIR}/${BACKUP_NAME}"

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

# 检查环境
check_environment() {
    log_step "检查环境..."

    if ! command -v kubectl &> /dev/null; then
        log_error "kubectl 未安装"
        exit 1
    fi

    if ! kubectl get namespace $NAMESPACE &> /dev/null; then
        log_error "命名空间 $NAMESPACE 不存在"
        exit 1
    fi

    # 获取后端 Pod 名称
    POD_NAME=$(kubectl get pod -n $NAMESPACE -l app.kubernetes.io/component=backend -o jsonpath='{.items[0].metadata.name}' 2>/dev/null || true)

    if [ -z "$POD_NAME" ]; then
        log_error "未找到后端 Pod，请确认 OpenDeepWiki 已部署"
        exit 1
    fi

    log_info "后端 Pod: $POD_NAME"
}

# 创建备份目录
setup_backup_dir() {
    log_step "创建备份目录..."
    mkdir -p "$BACKUP_DIR"
    log_info "备份目录: $BACKUP_DIR"
}

# 备份数据文件
backup_data() {
    log_step "备份数据文件（SQLite + 知识库）..."

    log_info "复制 /data 目录..."
    kubectl cp "$NAMESPACE/$POD_NAME:/data" "$BACKUP_DIR/data"

    # 检查备份内容
    if [ -f "$BACKUP_DIR/data/opendeepwiki.db" ]; then
        DB_SIZE=$(du -h "$BACKUP_DIR/data/opendeepwiki.db" | cut -f1)
        log_info "数据库备份成功: $DB_SIZE"
    else
        log_warn "未找到数据库文件 opendeepwiki.db"
    fi

    REPO_COUNT=$(find "$BACKUP_DIR/data" -type d | wc -l)
    log_info "知识库目录数量: $((REPO_COUNT - 1))"
}

# 备份配置
backup_configs() {
    log_step "备份 Kubernetes 配置..."

    # 导出 ConfigMap
    log_info "导出 ConfigMap..."
    kubectl get configmap -n $NAMESPACE -o yaml > "$BACKUP_DIR/configmaps.yaml"

    # 导出 Secret（注意：包含敏感信息，请妥善保管）
    log_info "导出 Secrets..."
    kubectl get secret opendeepwiki-secrets -n $NAMESPACE -o yaml > "$BACKUP_DIR/secrets.yaml"

    # 导出所有资源
    log_info "导出所有资源..."
    kubectl get all -n $NAMESPACE -o yaml > "$BACKUP_DIR/all-resources.yaml"

    # 导出 PVC
    log_info "导出 PVC..."
    kubectl get pvc -n $NAMESPACE -o yaml > "$BACKUP_DIR/pvc.yaml"
}

# 备份 Helm 配置
backup_helm() {
    log_step "备份 Helm 配置..."

    if helm list -n $NAMESPACE | grep -q opendeepwiki; then
        helm get values opendeepwiki -n $NAMESPACE > "$BACKUP_DIR/helm-values.yaml"
        helm get manifest opendeepwiki -n $NAMESPACE > "$BACKUP_DIR/helm-manifest.yaml"
        log_info "Helm 配置已导出"
    else
        log_warn "未找到 Helm release"
    fi
}

# 创建恢复脚本
create_restore_script() {
    log_step "创建恢复脚本..."

    cat > "$BACKUP_DIR/restore.sh" << 'RESTORE_SCRIPT'
#!/bin/bash
# restore.sh - OpenDeepWiki 恢复脚本
# 用法: ./restore.sh [命名空间]

set -e

NAMESPACE="${1:-opendeepwiki}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
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

# 检查环境
check_environment() {
    log_info "检查环境..."

    if ! command -v kubectl &> /dev/null; then
        log_error "kubectl 未安装"
        exit 1
    fi

    if ! kubectl get namespace $NAMESPACE &> /dev/null; then
        log_error "命名空间 $NAMESPACE 不存在，请先部署 OpenDeepWiki"
        exit 1
    fi

    # 等待 Pod 就绪
    log_info "等待后端 Pod 就绪..."
    kubectl wait --for=condition=ready pod \
        -l app.kubernetes.io/component=backend \
        -n $NAMESPACE \
        --timeout=300s
}

# 恢复配置
restore_configs() {
    log_info "恢复配置..."

    # 应用 ConfigMap
    if [ -f "$SCRIPT_DIR/configmaps.yaml" ]; then
        kubectl apply -f "$SCRIPT_DIR/configmaps.yaml"
        log_info "ConfigMap 已恢复"
    fi

    # 恢复 Secret（可选，如果新环境需要新 key 可以跳过）
    if [ -f "$SCRIPT_DIR/secrets.yaml" ]; then
        log_warn "正在恢复 Secrets，如果需要使用新的 API Key 请手动更新"
        kubectl apply -f "$SCRIPT_DIR/secrets.yaml"
        log_info "Secrets 已恢复"
    fi
}

# 恢复数据
restore_data() {
    log_info "恢复数据文件..."

    POD_NAME=$(kubectl get pod -n $NAMESPACE -l app.kubernetes.io/component=backend -o jsonpath='{.items[0].metadata.name}')

    # 备份当前数据（防止意外）
    log_info "备份当前 Pod 数据..."
    kubectl exec -n $NAMESPACE $POD_NAME -- sh -c 'cp -r /data /data.backup.$(date +%Y%m%d_%H%M%S) 2>/dev/null || true'

    # 复制数据到 Pod
    log_info "复制备份数据到 Pod..."
    kubectl cp "$SCRIPT_DIR/data" "$NAMESPACE/$POD_NAME:/data"

    # 设置正确权限
    kubectl exec -n $NAMESPACE $POD_NAME -- chown -R 1000:1000 /data

    log_info "数据恢复完成"
}

# 重启 Pod
restart_pods() {
    log_info "重启 Pod 以应用更改..."
    kubectl rollout restart deployment/opendeepwiki-backend -n $NAMESPACE
    kubectl rollout restart deployment/opendeepwiki-frontend -n $NAMESPACE

    # 等待就绪
    kubectl wait --for=condition=ready pod \
        -l app.kubernetes.io/component=backend \
        -n $NAMESPACE \
        --timeout=300s

    log_info "Pod 已重启并就绪"
}

# 显示信息
show_info() {
    log_info "恢复完成！"
    echo ""
    echo "=========================================="
    kubectl get pods -n $NAMESPACE
    echo "=========================================="
    echo ""
    log_info "如需更新 API Key，请运行:"
    echo "  kubectl set secret opendeepwiki-secrets -n $NAMESPACE \\"
    echo "    --from-literal=chat-api-key='新key' \\"
    echo "    --from-literal=catalog-api-key='新key' \\"
    echo "    --from-literal=content-api-key='新key'"
}

# 主函数
main() {
    log_info "开始恢复 OpenDeepWiki..."
    log_info "备份目录: $SCRIPT_DIR"
    log_info "目标命名空间: $NAMESPACE"

    check_environment
    restore_configs
    restore_data
    restart_pods
    show_info

    log_info "恢复完成！"
}

main "$@"
RESTORE_SCRIPT

    chmod +x "$BACKUP_DIR/restore.sh"
    log_info "恢复脚本已创建: restore.sh"
}

# 创建 README
create_readme() {
    log_step "创建备份说明..."

    cat > "$BACKUP_DIR/README.md" << README
# OpenDeepWiki 备份

备份时间: $(date '+%Y-%m-%d %H:%M:%S')
命名空间: $NAMESPACE
Pod 名称: $POD_NAME

## 备份内容

- \`data/\` - SQLite 数据库和知识库文件
- \`configmaps.yaml\` - ConfigMap 配置
- \`secrets.yaml\` - Secrets（包含 API Key，请妥善保管）
- \`all-resources.yaml\` - 所有 K8s 资源
- \`pvc.yaml\` - PVC 配置
- \`helm-values.yaml\` - Helm 部署配置
- \`helm-manifest.yaml\` - Helm manifest

## 恢复方法

### 1. 在新服务器部署 OpenDeepWiki

\`\`\`bash
# 启动 Minikube
minikube start --driver=docker --image-mirror-country=cn

# 运行部署脚本
cd scripts
./deploy-opendeepwiki.sh
\`\`\`

### 2. 恢复数据

\`\`\`bash
# 解压备份包（如果已打包）
tar xzf ${BACKUP_NAME}.tar.gz
cd ${BACKUP_NAME}

# 运行恢复脚本
./restore.sh [命名空间]
\`\`\`

### 3. 更新 API Key（如果需要）

\`\`\`bash
kubectl set secret opendeepwiki-secrets -n $NAMESPACE \\
  --from-literal=chat-api-key='新key' \\
  --from-literal=catalog-api-key='新key' \\
  --from-literal=content-api-key='新key'
\`\`\`

## 手动恢复

如果自动恢复失败，可以手动恢复：

\`\`\`bash
# 获取 Pod 名称
POD_NAME=\$(kubectl get pod -n $NAMESPACE -l app.kubernetes.io/component=backend -o jsonpath='{.items[0].metadata.name}')

# 复制数据
kubectl cp data \$NAMESPACE/\$POD_NAME:/data

# 设置权限
kubectl exec -n \$NAMESPACE \$POD_NAME -- chown -R 1000:1000 /data

# 重启 Pod
kubectl rollout restart deployment/opendeepwiki-backend -n \$NAMESPACE
\`\`\`

## 注意事项

1. Secrets 包含敏感信息，请妥善保管备份文件
2. 恢复前确保新环境可以访问 AI API
3. 建议在恢复前测试新环境的基础功能
README

    log_info "备份说明已创建: README.md"
}

# 打包备份
package_backup() {
    log_step "打包备份..."

    cd "$BACKUP_BASE_DIR"
    tar czf "${BACKUP_NAME}.tar.gz" "$BACKUP_NAME"

    # 计算大小
    BACKUP_SIZE=$(du -h "${BACKUP_NAME}.tar.gz" | cut -f1)

    log_info "备份打包完成: ${BACKUP_NAME}.tar.gz"
    log_info "备份大小: $BACKUP_SIZE"
}

# 显示备份摘要
show_summary() {
    echo ""
    echo "=========================================="
    echo "           备份完成摘要"
    echo "=========================================="
    log_info "备份名称: $BACKUP_NAME"
    log_info "备份位置: $BACKUP_DIR"
    log_info "压缩包: ${BACKUP_BASE_DIR}/${BACKUP_NAME}.tar.gz"
    echo ""
    log_info "备份内容:"
    ls -lh "$BACKUP_DIR" | tail -n +2
    echo ""
    log_info "重要文件:"
    echo "  - data/           : SQLite 数据库和知识库"
    echo "  - restore.sh      : 自动恢复脚本"
    echo "  - README.md       : 恢复说明"
    echo ""
    log_warn "注意事项:"
    echo "  1. secrets.yaml 包含 API Key，请妥善保管"
    echo "  2. 迁移到新服务器前，确保新环境可访问 AI API"
    echo "  3. 建议定期执行备份: ./backup-opendeepwiki.sh"
    echo "=========================================="
}

# 清理旧备份（保留最近7个）
cleanup_old_backups() {
    log_step "清理旧备份..."

    cd "$BACKUP_BASE_DIR"

    # 列出所有备份并按时间排序
    BACKUP_COUNT=$(ls -1d opendeepwiki-backup-* 2>/dev/null | wc -l)

    if [ "$BACKUP_COUNT" -gt 7 ]; then
        log_info "发现 $BACKUP_COUNT 个备份，保留最新的 7 个"
        ls -1td opendeepwiki-backup-* | tail -n +8 | xargs -r rm -rf
        log_info "旧备份已清理"
    else
        log_info "当前有 $BACKUP_COUNT 个备份，无需清理"
    fi
}

# 主函数
main() {
    echo "=========================================="
    echo "      OpenDeepWiki 备份工具"
    echo "=========================================="
    echo ""

    check_environment
    setup_backup_dir
    backup_data
    backup_configs
    backup_helm
    create_restore_script
    create_readme
    package_backup
    cleanup_old_backups
    show_summary

    echo ""
    log_info "备份完成！"
}

# 运行
main "$@"
