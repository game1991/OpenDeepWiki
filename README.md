# OpenDeepWiki

AI 驱动的代码知识库平台。

## 快速部署

```bash
# 1. 创建 Secret
bash scripts/create-opendeepwiki-secret.sh

# 2. 部署
./deploy.sh install

# 3. 查看状态
./deploy.sh status
```

详细部署指南：[DEPLOY.md](DEPLOY.md)

## 脚本说明

| 脚本 | 用途 |
|------|------|
| `deploy.sh` | 统一部署脚本（install/upgrade/uninstall/status/logs/restart） |
| `scripts/create-opendeepwiki-secret.sh` | 交互式创建 K8s Secret |

## Helm 配置

| 文件 | 用途 |
|------|------|
| `config/values-k3s.yaml` | K3s/本地开发配置 |
| `config/values-production.yaml` | 生产环境配置 |
| `charts/opendeepwiki/secret-example.yaml` | Secret 配置示例 |

## 默认访问

- **URL**: http://local.wiki.com (需配置 hosts)
- **账号**: 首次启动后请查看 Pod 日志获取默认凭据，并立即修改密码

## 命令参考

```bash
# 安装/升级/卸载
./deploy.sh install
./deploy.sh upgrade
./deploy.sh uninstall          # 保留数据
./deploy.sh uninstall --all    # 完全清理

# 查看状态
./deploy.sh status

# 查看日志
./deploy.sh logs backend
./deploy.sh logs frontend

# 重启
./deploy.sh restart

# 帮助
./deploy.sh help
```

## 环境变量

```bash
# 使用生产环境配置
VALUES_FILE=config/values-production.yaml ./deploy.sh install

# 指定命名空间
NAMESPACE=mywiki ./deploy.sh install
```
