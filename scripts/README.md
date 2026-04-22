# OpenDeepWiki 迁移友好部署方案

适用于 Minikube 单节点环境的 OpenDeepWiki 部署和迁移方案。

## 文件说明

```
scripts/
├── deploy-opendeepwiki.sh          # 部署脚本
├── opendeepwiki-migrate-friendly.yaml  # 迁移友好的 Helm 配置
├── backup-opendeepwiki.sh          # 备份脚本
├── restore-opendeepwiki.sh         # 恢复脚本
└── migrate-opendeepwiki.sh         # 一键迁移脚本
```

## 快速开始

### 1. 首次部署

```bash
cd scripts
./deploy-opendeepwiki.sh
```

脚本会：
- 检查 Minikube 状态
- 创建命名空间和 Secrets
- 部署 OpenDeepWiki
- 等待 Pod 就绪
- 显示访问方式

### 2. 访问应用

```bash
# 方式1: kubectl port-forward
kubectl port-forward -n opendeepwiki svc/opendeepwiki-frontend 3000:3000
# 访问 http://localhost:3000

# 方式2: Minikube service
minikube service opendeepwiki-frontend -n opendeepwiki
```

默认账号：
- 邮箱: `admin@routin.ai`
- 密码: `Admin@123`

### 3. 备份数据

```bash
./backup-opendeepwiki.sh
```

备份内容包括：
- SQLite 数据库和知识库文件
- ConfigMap 和 Secrets 配置
- Helm 部署配置
- 自动生成的恢复脚本

备份保存位置：`../backups/opendeepwiki-backup-YYYYMMDD_HHMMSS.tar.gz`

### 4. 恢复数据

```bash
# 使用最新备份
./restore-opendeepwiki.sh

# 指定备份目录
./restore-opendeepwiki.sh ../backups/opendeepwiki-backup-20240101_120000

# 指定备份压缩包
./restore-opendeepwiki.sh ../backups/opendeepwiki-backup-20240101_120000.tar.gz

# 指定命名空间
./restore-opendeepwiki.sh ../backups/opendeepwiki-backup-20240101_120000 my-namespace
```

### 5. 迁移到新服务器

在目标服务器上：

```bash
# 启动 Minikube
minikube start --driver=docker --image-mirror-country=cn

# 部署并恢复
./deploy-opendeepwiki.sh
./restore-opendeepwiki.sh /path/to/backup.tar.gz
```

## 一键迁移（从旧服务器到新服务器）

在新服务器上执行：

```bash
./migrate-opendeepwiki.sh --source user@old-server.com
```

此命令会：
1. 在旧服务器执行备份
2. 传输备份到新服务器
3. 部署 OpenDeepWiki
4. 恢复数据

可选参数：
```bash
./migrate-opendeepwiki.sh \
  --source admin@old-server.com \
  --ssh-key ~/.ssh/id_rsa \
  --source-ns opendeepwiki \
  --target-ns opendeepwiki
```

## 迁移检查清单

迁移到新服务器前确认：

- [ ] 新服务器已安装 kubectl、helm、minikube
- [ ] Minikube 已启动
- [ ] 新服务器可以访问 AI API（如 OpenAI）
- [ ] 备份文件已准备好
- [ ] 准备新的 API Key（如果需要）

迁移后确认：

- [ ] Pod 全部运行正常
- [ ] 可以登录系统
- [ ] 知识库数据完整
- [ ] AI 功能正常
- [ ] 更新 API Key（如果需要）

## 更新 API Key

```bash
kubectl set secret opendeepwiki-secrets -n opendeepwiki \
  --from-literal=chat-api-key='sk-new-key' \
  --from-literal=catalog-api-key='sk-new-key' \
  --from-literal=content-api-key='sk-new-key'

# 重启 Pod 生效
kubectl rollout restart deployment/opendeepwiki-backend -n opendeepwiki
```

## 注意事项

1. **SQLite vs MySQL**: 本方案使用 SQLite，单文件易备份。如需 MySQL，请参考官方文档修改配置。

2. **数据位置**: 数据存储在 PVC 中，路径 `/data`，包含：
   - `opendeepwiki.db` - SQLite 数据库
   - `repositories/` - 知识库文件

3. **自动清理**: 备份脚本会自动保留最近 7 个备份，旧的会自动删除。

4. **Secrets 安全**: 备份包含 Secrets（API Key），请妥善保管备份文件。

## 故障排查

### Pod 无法启动

```bash
# 查看 Pod 状态
kubectl get pods -n opendeepwiki

# 查看日志
kubectl logs -n opendeepwiki -l app.kubernetes.io/component=backend

# 查看事件
kubectl get events -n opendeepwiki --sort-by='.lastTimestamp'
```

### 数据丢失

```bash
# 检查 PVC
kubectl get pvc -n opendeepwiki

# 检查数据目录
kubectl exec -n opendeepwiki deployment/opendeepwiki-backend -- ls -la /data
```

### 权限问题

```bash
# 修复数据目录权限
kubectl exec -n opendeepwiki deployment/opendeepwiki-backend -- chown -R 1000:1000 /data
```
