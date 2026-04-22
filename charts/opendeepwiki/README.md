# OpenDeepWiki Helm Chart

OpenDeepWiki 是一个基于 AI 的代码知识库系统，基于 .NET 9 和 Semantic Kernel 开发。

## 功能特性

- 🤖 AI 驱动的代码分析
- 📚 自动生成项目文档
- 💬 智能问答系统
- 🌐 多语言支持
- 📊 Mermaid 图表生成
- 🔍 全文搜索引擎

## 安装

### 添加 Helm 仓库

```bash
# 克隆项目
git clone https://github.com/AIDotNet/OpenDeepWiki.git
cd OpenDeepWiki/charts/opendeepwiki

# 更新依赖（可选，用于 MySQL/PostgreSQL）
helm dependency update
```

### 快速开始

```bash
# 创建命名空间
kubectl create namespace opendeepwiki

# 安装（基础配置）
helm install opendeepwiki . -n opendeepwiki
```

### 生产环境安装

1. 创建 `custom-values.yaml`：

```yaml
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

2. 创建 Secrets：

```bash
kubectl create secret generic opendeepwiki-secrets \
  -n opendeepwiki \
  --from-literal=chat-api-key="sk-xxx" \
  --from-literal=catalog-api-key="sk-xxx" \
  --from-literal=content-api-key="sk-xxx" \
  --from-literal=jwt-secret="your-secret-key"
```

3. 部署：

```bash
helm install opendeepwiki . \
  -n opendeepwiki \
  -f custom-values.yaml
```

## 配置

### 数据库选项

#### SQLite（默认）

无需额外配置，数据存储在 PVC 中。

#### MySQL

```yaml
mysql:
  enabled: true
  architecture: standalone
  auth:
    username: opendeepwiki
    password: your-password
    database: opendeepwiki

backend:
  environment:
    Database__Type: mysql
    ConnectionStrings__Default: >
      Server=opendeepwiki-mysql-primary;
      Port=3306;
      Database=opendeepwiki;
      User Id=opendeepwiki;
      Password=your-password;
      Charset=utf8mb4;
  initContainers:
    - name: wait-for-mysql
      image: busybox:1.36
      command:
        - sh
        - -c
        - |
          until nc -z opendeepwiki-mysql-primary 3306; do
            sleep 2
          done
```

### 存储配置

默认使用 `local-path` StorageClass，适合单节点 K8s：

```yaml
backend:
  persistence:
    enabled: true
    storageClass: "local-path"
    size: 50Gi
```

如需使用其他存储类型，修改 `storageClass` 即可。

### 自动扩缩容

```yaml
autoscaling:
  enabled: true
  backend:
    minReplicas: 2
    maxReplicas: 10
    targetCPUUtilizationPercentage: 70
  frontend:
    minReplicas: 2
    maxReplicas: 5
    targetCPUUtilizationPercentage: 70
```

## Values 参数

### 后端配置

| 参数 | 描述 | 默认值 |
|------|------|--------|
| `backend.replicaCount` | 副本数 | `2` |
| `backend.image.repository` | 镜像仓库 | `crpi-j9ha7sxwhatgtvj4.cn-shenzhen.personal.cr.aliyuncs.com/open-deepwiki/opendeepwiki` |
| `backend.image.tag` | 镜像标签 | `latest` |
| `backend.resources.requests.memory` | 内存请求 | `512Mi` |
| `backend.resources.limits.memory` | 内存限制 | `2Gi` |
| `backend.persistence.enabled` | 启用持久化 | `true` |
| `backend.persistence.size` | 存储大小 | `50Gi` |
| `backend.persistence.storageClass` | 存储类 | `local-path` |

### 前端配置

| 参数 | 描述 | 默认值 |
|------|------|--------|
| `frontend.replicaCount` | 副本数 | `2` |
| `frontend.image.repository` | 镜像仓库 | `crpi-j9ha7sxwhatgtvj4.cn-shenzhen.personal.cr.aliyuncs.com/open-deepwiki/opendeepwiki-web` |
| `frontend.resources.requests.memory` | 内存请求 | `256Mi` |
| `frontend.resources.limits.memory` | 内存限制 | `512Mi` |

### 数据库配置

| 参数 | 描述 | 默认值 |
|------|------|--------|
| `mysql.enabled` | 启用 MySQL | `false` |
| `postgresql.enabled` | 启用 PostgreSQL | `false` |

### Ingress 配置

| 参数 | 描述 | 默认值 |
|------|------|--------|
| `ingress.enabled` | 启用 Ingress | `true` |
| `ingress.className` | Ingress 类名 | `nginx` |
| `ingress.hosts` | 主机配置 | 见 values.yaml |

## 维护

### 升级

```bash
helm upgrade opendeepwiki . -n opendeepwiki -f custom-values.yaml
```

### 回滚

```bash
helm rollback opendeepwiki -n opendeepwiki
```

### 卸载

```bash
helm uninstall opendeepwiki -n opendeepwiki
kubectl delete pvc -n opendeepwiki --all
```

## 故障排查

查看日志：

```bash
# 后端日志
kubectl logs -n opendeepwiki -l app.kubernetes.io/component=backend -f

# 前端日志
kubectl logs -n opendeepwiki -l app.kubernetes.io/component=frontend -f
```

检查状态：

```bash
kubectl get pods -n opendeepwiki
kubectl get svc -n opendeepwiki
kubectl get ingress -n opendeepwiki
```

## 相关链接

- [项目主页](https://github.com/AIDotNet/OpenDeepWiki)
- [部署手册](../../docs/deployment.md)
- [用户指南](../../docs/user-guide.md)
