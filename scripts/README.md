# OpenDeepWiki 运维脚本

## 脚本列表

| 脚本 | 用途 |
|------|------|
| `create-opendeepwiki-secret.sh` | 交互式创建 K8s Secret（API Key + JWT） |
| `deploy-k3s-local.sh` | K3s 环境一键部署（K3s + Dashboard + Gateway + Wiki） |
| `backup-opendeepwiki.sh` | 备份 SQLite 数据库和知识库文件 |
| `restore-opendeepwiki.sh` | 从备份恢复数据 |
| `migrate-opendeepwiki.sh` | 跨服务器迁移（备份→传输→恢复） |
| `sync-wiki-db.sh` | 同步数据库到本地（供 Navicat 查看） |

## 快速开始

```bash
# 创建 Secret（首次部署时执行）
bash scripts/create-opendeepwiki-secret.sh

# 统一部署管理
./deploy.sh install     # 安装
./deploy.sh upgrade     # 升级
./deploy.sh status      # 查看状态
./deploy.sh logs backend  # 查看日志
```

详见 [DEPLOY.md](../DEPLOY.md)

## 各脚本说明

### create-opendeepwiki-secret.sh

交互式创建 `opendeepwiki-secrets` Secret，包含：

| Key | 必需 | 说明 |
|-----|------|------|
| `chat-api-key` | ✅ | AI API 密钥 |
| `jwt-secret-key` | ✅ | JWT 签名密钥 |
| `wiki-catalog-api-key` | ❌ | 目录生成专用（默认同 chat-api-key） |
| `wiki-content-api-key` | ❌ | 内容生成专用（默认同 chat-api-key） |

### deploy-k3s-local.sh

从零搭建完整 K3s 环境，包含：
1. 安装 K3s（`--disable=servicelb`）
2. 安装 Kubernetes Dashboard
3. 部署 K8s Gateway（Helm）
4. 部署 OpenDeepWiki（Helm）

> 适用于首次环境搭建，日常管理使用 `./deploy.sh`

### backup / restore / migrate

数据备份恢复工具链：
- **backup**: 备份数据库、ConfigMap、Secret、Helm 配置
- **restore**: 从备份恢复到新环境
- **migrate**: 一键从旧服务器迁移到新服务器

### sync-wiki-db.sh

将 K8s 中的 SQLite 数据库同步到 WSL2 本地和 Windows 目录，供 Navicat 等工具只读查看。
