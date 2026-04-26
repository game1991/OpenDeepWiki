# OpenDeepWiki 部署手册

本文档介绍如何通过 K3s + Helm 部署 OpenDeepWiki 知识库系统。

---

## 目录

1. [环境准备](#环境准备)
2. [K3s + Helm 部署](#k3s--helm-部署)
3. [统一网关配置](#统一网关配置)
4. [环境变量配置](#环境变量配置)
5. [常见问题排查](#常见问题排查)

---

## 环境准备

### 系统要求

| 组件 | 最低配置 | 推荐配置 |
|------|----------|----------|
| K3s | v1.34+ | v1.34.6+k3s1 |
| 内存 | 4GB | 8GB+ |
| 存储 | 10GB | 20GB+ |
| CPU | 2核 | 4核+ |

### 前置条件

- WSL2 CentOS 7.9（或其他 Linux 发行版）
- K3s 已安装并运行
- Helm 3 已安装
- kubectl 可访问集群

---

## K3s + Helm 部署

### 1. 安装 K3s

```bash
curl -sfL https://rancher.io/install-k3s | sh -s - --cluster-init

# 验证
kubectl get nodes
```

### 2. 创建命名空间和 Secret

```bash
kubectl create namespace opendeepwiki

kubectl create secret generic opendeepwiki-secrets -n opendeepwiki \
  --from-literal=chat-api-key='<AI_API_KEY>' \
  --from-literal=catalog-api-key='<AI_API_KEY>' \
  --from-literal=content-api-key='<AI_API_KEY>' \
  --from-literal=wiki-translation-api-key='<AI_API_KEY>' \
  --from-literal=jwt-secret='$(openssl rand -base64 32)'
```

### 3. Helm 部署

```bash
helm install opendeepwiki charts/opendeepwiki \
  -n opendeepwiki \
  -f config/values-k3s.yaml
```

### 4. 验证部署

```bash
kubectl get pods -n opendeepwiki
kubectl get ingress -n opendeepwiki

# 测试 API
curl -s -H "Host: local.wiki.com" http://localhost:8880/api/system/version
```

### 5. 更新部署

```bash
helm upgrade opendeepwiki charts/opendeepwiki \
  -n opendeepwiki \
  -f config/values-k3s.yaml
```

---

## 统一网关配置

详见 [K8s 网关部署文档](k8s-gateway/README.md)

| 服务 | 域名 | 端口 | 协议 |
|------|------|------|------|
| 统一网关 | local.gateway.com | 8880 | HTTP |
| Dashboard | local.dashboard.com | 8443 | HTTPS |
| OpenDeepWiki | local.wiki.com | 8880 | HTTP |

---

## 环境变量配置

values-k3s.yaml 中的关键配置项：

```yaml
backend:
  env:
    ENDPOINT: "https://api.moonshot.cn/v1"        # AI API 端点
    CHAT_REQUEST_MODEL_ID: "kimi-k2.5"             # 聊天模型
    CATALOG_REQUEST_MODEL_ID: "kimi-k2.5"          # 目录生成模型
    CONTENT_REQUEST_MODEL_ID: "kimi-k2.5"          # 内容生成模型
    WIKI_TRANSLATION_MODEL: "kimi-k2.5"            # 翻译模型
```

---

## 常见问题排查

### Pod 一直 Pending

```bash
kubectl describe pod <pod-name> -n opendeepwiki | grep -A 10 Events
```

常见原因：
- PV 节点亲和性不匹配（节点名变化）
- 资源不足

### 数据库索引损坏

```bash
sudo sqlite3 /var/lib/rancher/k3s/storage/pvc-xxx/.../opendeepwiki.db \
  "PRAGMA integrity_check; REINDEX;"
```

### K3s 未自启动

```bash
# 检查 /etc/init.wsl 是否配置了 k3s
cat /etc/init.wsl

# 手动启动
sudo /usr/local/bin/k3s server &
```

详细踩坑记录见 [Kind→K3s 迁移踩坑记录](k8s-gateway/kind-to-k3s-migration.md)