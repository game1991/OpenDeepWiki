# OpenDeepWiki 部署指南

## 快速开始

使用统一的部署脚本管理 OpenDeepWiki 的整个生命周期。

```bash
# 1. 创建 Secret（只需执行一次）
bash scripts/create-opendeepwiki-secret.sh

# 2. 安装
./deploy.sh install

# 3. 查看状态
./deploy.sh status
```

## deploy.sh 脚本

### 命令列表

| 命令 | 说明 | 示例 |
|------|------|------|
| `install` | 全新安装 | `./deploy.sh install` |
| `upgrade` | 升级现有部署 | `./deploy.sh upgrade` |
| `uninstall` | 卸载（保留数据） | `./deploy.sh uninstall` |
| `uninstall --all` | 完全卸载（删除数据） | `./deploy.sh uninstall --all` |
| `status` | 查看状态 | `./deploy.sh status` |
| `logs` | 查看日志 | `./deploy.sh logs backend 100` |
| `restart` | 重启服务 | `./deploy.sh restart` |
| `help` | 显示帮助 | `./deploy.sh help` |

### 环境变量

| 变量 | 默认值 | 说明 |
|------|--------|------|
| `VALUES_FILE` | `config/values-k3s.yaml` | 指定 values 文件 |
| `NAMESPACE` | `opendeepwiki` | 指定命名空间 |

### 使用示例

#### 安装到指定命名空间

```bash
NAMESPACE=mywiki ./deploy.sh install
```

#### 使用生产环境配置

```bash
VALUES_FILE=config/values-production.yaml ./deploy.sh install
```

#### 查看前端日志

```bash
./deploy.sh logs frontend 200
```

#### 普通卸载（保留数据）

```bash
./deploy.sh uninstall
```

此命令会：
- ✓ 删除 OpenDeepWiki 应用（Deployment、Pod、Service、Ingress）
- ✗ 保留数据库持久化卷（PVC）- Wiki 数据不会丢失
- ✗ 保留密钥（Secret）- API Key 等配置不会丢失

重新部署后可以恢复数据：
```bash
./deploy.sh install  # 数据仍然存在
```

#### 完全卸载（删除所有数据）

```bash
./deploy.sh uninstall --all
# 或
./deploy.sh uninstall --purge
```

⚠️ 警告：此操作将永久删除所有数据，包括：
- OpenDeepWiki 应用
- 数据库持久化卷（PVC）- 所有 Wiki 数据
- 密钥（Secret）- API Key 等配置

## Secret 管理

### 创建 Secret

交互式创建：

```bash
bash scripts/create-opendeepwiki-secret.sh [namespace]
```

手动创建：

```bash
kubectl create secret generic opendeepwiki-secrets \
  --namespace opendeepwiki \
  --from-literal=chat-api-key='your-api-key' \
  --from-literal=jwt-secret-key='your-jwt-secret'
```

### Secret Key 说明

| Key | 必需 | 说明 |
|-----|------|------|
| `chat-api-key` | ✅ | AI API 密钥 |
| `jwt-secret-key` | ✅ | JWT 签名密钥（至少32字符） |
| `wiki-catalog-api-key` | ❌ | 目录生成专用（默认使用 chat-api-key） |
| `wiki-content-api-key` | ❌ | 内容生成专用（默认使用 chat-api-key） |
| `wiki-translation-api-key` | ❌ | 翻译功能专用 |

生成 JWT Secret：

```bash
openssl rand -base64 32
```

## 环境配置

### 配置目录结构

```
config/
├── values-k3s.yaml          # K3s/本地开发配置（默认）
└── values-production.yaml   # 生产环境配置
```

### 切换环境

开发环境：

```bash
./deploy.sh install  # 默认使用 values-k3s.yaml
```

生产环境：

```bash
VALUES_FILE=config/values-production.yaml ./deploy.sh install
```

## 访问方式

### 方式1：Ingress（推荐）

配置 hosts：

```bash
echo '<YOUR_WSL_IP> local.wiki.com' | sudo tee -a /etc/hosts
curl http://local.wiki.com/api/system/version
```

### 方式2：Port-forward

前端：

```bash
kubectl port-forward -n opendeepwiki svc/opendeepwiki-frontend 3000:3000
# 访问 http://localhost:3000
```

后端 API：

```bash
kubectl port-forward -n opendeepwiki svc/opendeepwiki-backend 8080:8080
curl http://localhost:8080/api/system/version
```

## 默认账号

- **邮箱**: 首次启动后请查看 Pod 日志获取默认凭据

> ⚠️ 首次登录后请立即修改密码！

## 故障排查

### 查看 Pod 状态

```bash
./deploy.sh status
```

### 查看日志

```bash
# 后端日志
./deploy.sh logs backend

# 前端日志
./deploy.sh logs frontend

# 实时跟踪
kubectl logs -n opendeepwiki -l app.kubernetes.io/component=backend -f
```

### Pod 无法启动

检查 Secret：

```bash
kubectl get secret opendeepwiki-secrets -n opendeepwiki
kubectl describe pod -n opendeepwiki -l app.kubernetes.io/component=backend
```

### 重新部署

```bash
# 完全清理后重新安装
./deploy.sh uninstall
./deploy.sh install
```
