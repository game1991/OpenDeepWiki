# OpenDeepWiki 部署手册

本文档详细介绍如何部署 OpenDeepWiki 知识库系统，包含 Docker Compose 和 Kubernetes + Helm 两种部署方式。

---

## 目录

1. [环境准备](#环境准备)
2. [Docker Compose 部署](#docker-compose-部署)
3. [Kubernetes + Helm 部署](#kubernetes--helm-部署)
4. [环境变量配置](#环境变量配置)
5. [常见问题排查](#常见问题排查)

---

## 环境准备

### 系统要求

| 组件 | 最低配置 | 推荐配置 |
|------|----------|----------|
| CPU | 2 核 | 4 核 |
| 内存 | 4 GB | 8 GB |
| 磁盘 | 20 GB | 100 GB |
| 网络 | 可访问互联网 | 稳定的网络连接 |

### 软件要求

**Docker Compose 部署**：
- Docker 20.10+
- Docker Compose 2.0+
- Git

**Kubernetes 部署**：
- Kubernetes 1.24+
- Helm 3.12+
- Ingress Controller（如 Nginx Ingress）
- StorageClass（local-path 或其他）

---

## Docker Compose 部署

### 1. 克隆仓库

```bash
git clone https://github.com/AIDotNet/OpenDeepWiki.git
cd OpenDeepWiki
```

### 2. 配置环境变量

创建 `.env` 文件：

```bash
cat > .env << 'EOF'
# AI 对话配置
CHAT_API_KEY=your-openai-api-key
ENDPOINT=https://api.openai.com/v1
CHAT_REQUEST_TYPE=OpenAI

# 目录生成 AI 配置
WIKI_CATALOG_MODEL=gpt-4o-mini
WIKI_CATALOG_API_KEY=your-openai-api-key

# 内容生成 AI 配置
WIKI_CONTENT_MODEL=gpt-4o
WIKI_CONTENT_API_KEY=your-openai-api-key

# JWT 密钥（生产环境请修改）
JWT_SECRET_KEY=your-secret-key-min-32-characters-long
EOF
```

### 3. 启动服务

```bash
# 方式1：使用 Docker Compose
docker compose up -d

# 方式2：使用 Makefile
make build && make up
```

### 4. 验证部署

```bash
# 查看容器状态
docker compose ps

# 查看日志
docker compose logs -f

# 测试后端 API
curl http://localhost:8080/health

# 访问前端
# 打开浏览器访问 http://localhost:3000
```

### 5. 默认账号

- **邮箱**：`admin@routin.ai`
- **密码**：`Admin@123`

**⚠️ 重要**：首次登录后请立即修改默认密码！

---

## 内网部署网络要求

### AI API 访问说明

OpenDeepWiki 需要访问 AI 服务提供商的 API 才能正常工作：

| 功能 | 需要的 API | 默认端点 |
|------|-----------|----------|
| AI 对话 | OpenAI / Azure / Anthropic | `https://api.openai.com/v1` |
| 目录生成 | OpenAI / Azure / Anthropic | `https://api.openai.com/v1` |
| 内容生成 | OpenAI / Azure / Anthropic | `https://api.openai.com/v1` |

**⚠️ 内网部署注意**：如果您的 K8s 集群无法直接访问外网，需要配置以下任一方案：

### 方案 1：配置代理服务器（推荐）

在 `values.yaml` 中配置代理：

```yaml
backend:
  environment:
    # 通过代理访问 AI API
    ENDPOINT: http://your-proxy-server:8080/v1
    WIKI_CATALOG_ENDPOINT: http://your-proxy-server:8080/v1
    WIKI_CONTENT_ENDPOINT: http://your-proxy-server:8080/v1
```

或使用环境变量配置代理：

```yaml
backend:
  environment:
    HTTP_PROXY: http://proxy.company.com:8080
    HTTPS_PROXY: http://proxy.company.com:8080
    NO_PROXY: localhost,127.0.0.1,cluster.local
```

### 方案 2：使用内网可访问的 AI 服务

如果有内网部署的 AI 服务（如内网 OpenAI 兼容服务、自托管模型等）：

```yaml
backend:
  environment:
    # 内网 AI 服务地址
    ENDPOINT: http://internal-ai-service.company.com:8000/v1
    CHAT_REQUEST_TYPE: OpenAI  # 确保兼容 OpenAI API 格式
```

### 方案 3：使用 Kubernetes 出口代理

配置集群级别的出口代理：

```yaml
# 在 Pod 级别配置代理
backend:
  environment:
    HTTP_PROXY: http://egress-proxy.company.com:3128
    HTTPS_PROXY: http://egress-proxy.company.com:3128
    NO_PROXY: .cluster.local,.svc,10.0.0.0/8
```

### 方案 4：本地 AI 模型（无需外网）

如果有本地部署的 AI 模型（如 Ollama、LocalAI、vLLM）：

```yaml
backend:
  environment:
    # 本地模型服务
    ENDPOINT: http://ollama-service.local:11434/v1
    CHAT_REQUEST_TYPE: OpenAI
    WIKI_CATALOG_MODEL: llama3.1:8b
    WIKI_CONTENT_MODEL: llama3.1:8b
```

**注意**：本地模型需要确保性能足够（推荐至少 8B 参数模型）。

### 网络连通性测试

部署前，建议先测试网络连通性：

```bash
# 在 K8s 集群内创建测试 Pod
kubectl run -it --rm test-network --image=curlimages/curl --restart=Never -- \
  curl -v https://api.openai.com/v1/models \
  -H "Authorization: Bearer sk-your-api-key"

# 如果无法访问，测试代理连通性
kubectl run -it --rm test-proxy --image=curlimages/curl --restart=Never -- \
  curl -v -x http://proxy.company.com:8080 \
  https://api.openai.com/v1/models
```

---

## Kubernetes + Helm 部署

### 1. 前置检查

```bash
# 检查 K8s 版本
kubectl version --short

# 检查 Helm 版本
helm version

# 检查 StorageClass
kubectl get storageclass

# 确保有 local-path 或其他可用的 StorageClass
```

### 2. 准备 Helm Chart

```bash
# 克隆项目
git clone https://github.com/AIDotNet/OpenDeepWiki.git
cd OpenDeepWiki/charts/opendeepwiki

# 更新 Helm 依赖（如果使用 MySQL/PostgreSQL）
helm dependency update
```

### 3. 创建命名空间和 Secret

```bash
# 创建命名空间
kubectl create namespace opendeepwiki

# 创建 Secrets（请替换为实际的 API 密钥）
kubectl create secret generic opendeepwiki-secrets \
  --namespace opendeepwiki \
  --from-literal=chat-api-key="sk-xxxxxxxx" \
  --from-literal=catalog-api-key="sk-xxxxxxxx" \
  --from-literal=content-api-key="sk-xxxxxxxx" \
  --from-literal=jwt-secret="your-secret-key-min-32-characters-long"
```

### 4. 配置 values.yaml

创建 `custom-values.yaml`：

```yaml
# 后端配置
backend:
  secrets:
    - name: CHAT_API_KEY
      secretName: opendeepwiki-secrets
      key: chat-api-key
    - name: WIKI_CATALOG_API_KEY
      secretName: opendeepwiki-secrets
      key: catalog-api-key
    - name: WIKI_CONTENT_API_KEY
      secretName: opendeepwiki-secrets
      key: content-api-key
    - name: JWT_SECRET_KEY
      secretName: opendeepwiki-secrets
      key: jwt-secret

# Ingress 配置（请修改为你的域名）
ingress:
  enabled: true
  hosts:
    - host: wiki.example.com
      paths:
        - path: /
          pathType: Prefix
          service: frontend
        - path: /api
          pathType: Prefix
          service: backend
```

### 5. 部署到 K8s

```bash
helm install opendeepwiki . \
  --namespace opendeepwiki \
  --values custom-values.yaml
```

### 6. 使用 MySQL 数据库（可选）

如果需要使用 MySQL 替代 SQLite：

```yaml
# custom-values.yaml
mysql:
  enabled: true
  architecture: standalone
  auth:
    rootPassword: "root-pass"
    username: "opendeepwiki"
    password: "user-pass"
    database: "opendeepwiki"
  primary:
    persistence:
      enabled: true
      size: 10Gi
      storageClass: "local-path"

backend:
  environment:
    Database__Type: mysql
    ConnectionStrings__Default: >
      Server=opendeepwiki-mysql-primary;
      Port=3306;
      Database=opendeepwiki;
      User Id=opendeepwiki;
      Password=user-pass;
      Charset=utf8mb4;
  initContainers:
    - name: wait-for-mysql
      image: busybox:1.36
      command:
        - sh
        - -c
        - |
          until nc -z opendeepwiki-mysql-primary 3306; do
            echo "Waiting for MySQL..."
            sleep 2
          done
```

### 7. 验证部署

```bash
# 查看 Pod 状态
kubectl get pods -n opendeepwiki -w

# 查看服务
kubectl get svc -n opendeepwiki

# 查看 Ingress
kubectl get ingress -n opendeepwiki

# 查看 PVC
kubectl get pvc -n opendeepwiki

# 查看后端日志
kubectl logs -n opendeepwiki -l app.kubernetes.io/component=backend -f
```

### 8. 配置 DNS

将域名解析到 Ingress Controller 的 IP：

```bash
# 获取 Ingress IP
kubectl get svc -n ingress-nginx

# 配置 DNS A 记录
# wiki.example.com -> <Ingress-IP>
```

---

## 环境变量配置

### 后端环境变量

| 变量名 | 说明 | 默认值 |
|--------|------|--------|
| `ASPNETCORE_ENVIRONMENT` | 运行环境 | `Production` |
| `URLS` | 监听地址 | `http://+:8080` |
| `Database__Type` | 数据库类型 | `sqlite` |
| `ConnectionStrings__Default` | 数据库连接字符串 | SQLite 路径 |
| `CHAT_API_KEY` | AI 对话 API 密钥 | - |
| `ENDPOINT` | AI API 端点 | OpenAI 官方 |
| `CHAT_REQUEST_TYPE` | AI 请求类型 | `OpenAI` |
| `WIKI_CATALOG_MODEL` | 目录生成模型 | `gpt-4o` |
| `WIKI_CATALOG_API_KEY` | 目录生成 API 密钥 | - |
| `WIKI_CONTENT_MODEL` | 内容生成模型 | `gpt-4o` |
| `WIKI_CONTENT_API_KEY` | 内容生成 API 密钥 | - |
| `WIKI_PARALLEL_COUNT` | 并行处理数量 | `5` |
| `WIKI_LANGUAGES` | 支持的语言 | `en,zh` |
| `JWT_SECRET_KEY` | JWT 签名密钥 | - |
| `REPOSITORIES_DIRECTORY` | 知识库存储目录 | `/data` |

### 前端环境变量

| 变量名 | 说明 | 默认值 |
|--------|------|--------|
| `NODE_ENV` | Node 环境 | `production` |
| `API_PROXY_URL` | 后端 API 地址 | `http://backend:8080` |

---

## 常见问题排查

### Q1: 容器启动失败，日志显示权限错误

**原因**：数据目录权限不足

**解决**：
```bash
# Docker Compose
docker compose exec -u root opendeepwiki chown -R app:app /data

# Kubernetes
kubectl exec -n opendeepwiki deployment/opendeepwiki-backend -- chown -R app:app /data
```

### Q2: AI 功能无法使用，提示 API 错误

**排查步骤**：
1. 检查 API Key 是否正确配置
2. 确认网络可以访问 AI 服务提供商
3. 查看后端日志确认具体错误

```bash
# Docker Compose
docker compose logs opendeepwiki | grep -i error

# Kubernetes
kubectl logs -n opendeepwiki -l app.kubernetes.io/component=backend | grep -i error
```

### Q3: 数据库连接失败（MySQL 模式）

**排查步骤**：
1. 检查 MySQL Pod 是否正常运行
2. 验证连接字符串配置
3. 确认 Secret 中的密码正确

```bash
# 检查 MySQL 状态
kubectl get pods -n opendeepwiki | grep mysql

# 测试数据库连接
kubectl run -it --rm mysql-client --image=mysql:8 --restart=Never -- mysql -h opendeepwiki-mysql-primary -u opendeepwiki -p
```

### Q4: Ingress 无法访问

**排查步骤**：
1. 确认 Ingress Controller 已安装
2. 检查 Ingress 配置和域名解析
3. 查看 Ingress Controller 日志

```bash
# 检查 Ingress 状态
kubectl describe ingress -n opendeepwiki

# 测试后端直接访问
kubectl port-forward -n opendeepwiki svc/opendeepwiki-backend 8080:8080
```

### Q5: local-path PVC 处于 Pending 状态

**原因**：local-path 需要在节点上有可用的本地目录

**解决**：
1. 确认已安装 local-path-provisioner
2. 检查节点磁盘空间
3. 或者改用其他 StorageClass

```bash
# 检查 local-path-provisioner
kubectl get pods -n local-path-storage

# 查看 PVC 事件
kubectl describe pvc -n opendeepwiki
```

---

## 升级与维护

### 升级 Helm Chart

```bash
# 拉取更新
git pull origin main

# 升级部署
helm upgrade opendeepwiki . \
  --namespace opendeepwiki \
  --values custom-values.yaml
```

### 备份数据

```bash
# 备份 SQLite 数据库
kubectl cp opendeepwiki/opendeepwiki-backend-xxx:/data/opendeepwiki.db ./backup.db

# 备份 MySQL 数据
kubectl exec -n opendeepwiki opendeepwiki-mysql-primary-xxx -- mysqldump -u root -p opendeepwiki > backup.sql
```

### 卸载

```bash
# Helm 卸载
helm uninstall opendeepwiki -n opendeepwiki

# 删除 PVC（会删除数据！）
kubectl delete pvc -n opendeepwiki --all

# Docker Compose 卸载
docker compose down -v
```

---

## 相关文档

- [用户操作指南](./user-guide.md)
- [数据导入教程](./data-import.md)
- [最佳实践手册](./best-practices.md)
