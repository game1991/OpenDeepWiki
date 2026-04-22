#!/bin/bash
# 快速部署修复版本 - 使用 ConfigMap 注入修复代码
# 无需重新构建镜像，直接注入修复后的 RepositoryAnalyzer.cs

set -e

NAMESPACE="opendeepwiki"
FIX_FILE="/home/ganlei/workspcae/openDeepWiki/src/fix/RepositoryAnalyzer.cs"

echo "=========================================="
echo "OpenDeepWiki 热修复部署"
echo "=========================================="
echo ""
echo "原理: 使用 ConfigMap 挂载修复后的代码到容器"
echo ""

# 检查修复文件
if [ ! -f "$FIX_FILE" ]; then
    echo "错误: 修复文件不存在: $FIX_FILE"
    exit 1
fi

echo "1. 创建 ConfigMap 存储修复代码..."
kubectl create configmap opendeepwiki-fix \
    --from-file=RepositoryAnalyzer.cs="$FIX_FILE" \
    -n "$NAMESPACE" \
    --dry-run=client -o yaml | kubectl apply -f -

echo ""
echo "2. 备份当前 Deployment..."
kubectl get deployment opendeepwiki-backend -n "$NAMESPACE" -o yaml > /tmp/opendeepwiki-backup.yaml
echo "备份已保存: /tmp/opendeepwiki-backup.yaml"

echo ""
echo "3. 应用修复补丁..."
cat << 'EOF' | kubectl patch deployment opendeepwiki-backend -n "$NAMESPACE" --patch-file=/dev/stdin
{
  "spec": {
    "template": {
      "spec": {
        "volumes": [
          {
            "name": "fix-code",
            "configMap": {
              "name": "opendeepwiki-fix"
            }
          }
        ],
        "containers": [
          {
            "name": "opendeepwiki-backend",
            "volumeMounts": [
              {
                "name": "fix-code",
                "mountPath": "/app/Services/Repositories/RepositoryAnalyzer.cs",
                "subPath": "RepositoryAnalyzer.cs"
              }
            ]
          }
        ]
      }
    }
  }
}
EOF

echo ""
echo "4. 等待滚动更新完成..."
kubectl rollout status deployment/opendeepwiki-backend -n "$NAMESPACE" --timeout=120s

echo ""
echo "=========================================="
echo "修复部署完成!"
echo "=========================================="
echo ""
echo "验证方法:"
echo "  kubectl logs -n $NAMESPACE deployment/opendeepwiki-backend | grep -i 'embedded credentials'"
echo ""
echo "回滚命令（如需要）:"
echo "  kubectl rollout undo deployment/opendeepwiki-backend -n $NAMESPACE"
echo ""
