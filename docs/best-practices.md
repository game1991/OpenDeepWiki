# OpenDeepWiki 最佳实践手册

本文档提供 OpenDeepWiki 知识库系统的最佳实践建议，帮助您更好地使用和管理系统。

---

## 目录

1. [知识库组织建议](#知识库组织建议)
2. [性能优化技巧](#性能优化技巧)
3. [安全注意事项](#安全注意事项)
4. [K8s 生产环境最佳实践](#k8s-生产环境最佳实践)
5. [local-path 存储使用建议](#local-path-存储使用建议)
6. [AI 配置优化](#ai-配置优化)
7. [监控与告警](#监控与告警)

---

## 知识库组织建议

### 仓库命名规范

使用清晰、一致的命名：

```
✅ 推荐：
  - frontend-web-app
  - backend-api-service
  - ml-training-pipeline

❌ 避免：
  - project1
  - test
  - myrepo
```

### 分类策略

按业务域组织：

```
📁 前端项目
   ├── 📦 web-dashboard
   ├── 📦 mobile-app-h5
   └── 📦 admin-console

📁 后端服务
   ├── 📦 user-service
   ├── 📦 order-service
   └── 📦 payment-service

📁 基础设施
   ├── 📦 deployment-scripts
   └── 📦 monitoring-configs
```

### 导入前准备

1. **清理无用文件**：
   ```bash
   # 删除依赖目录
   rm -rf node_modules/ vendor/ __pycache__/

   # 删除构建产物
   rm -rf dist/ build/ target/

   # 删除大文件
   find . -size +10M -type f -delete
   ```

2. **添加 .opendeepwikiignore**：
   ```
   # 系统文件
   .DS_Store
   Thumbs.db

   # IDE 配置
   .idea/
   .vscode/
   *.swp

   # 日志文件
   *.log
   logs/

   # 敏感文件
   .env
   *.pem
   *.key
   ```

---

## 性能优化技巧

### 数据库优化

**SQLite（默认）**：
- 适用：小型项目（< 10 个仓库）
- 限制：单节点，并发写入有限

**MySQL（推荐生产环境）**：
```yaml
# 推荐配置
mysql:
  primary:
    configuration: |-
      [mysqld]
      max_connections=500
      innodb_buffer_pool_size=2G
      innodb_log_file_size=512M
      query_cache_size=256M
      character-set-server=utf8mb4
```

### AI 处理优化

1. **合理设置并发数**：
   ```yaml
   # 小型部署
   WIKI_PARALLEL_COUNT: "3"

   # 中型部署
   WIKI_PARALLEL_COUNT: "5"

   # 大型部署（需高配额 API Key）
   WIKI_PARALLEL_COUNT: "10"
   ```

2. **选择合适的模型**：

   | 任务类型 | 推荐模型 | 成本 | 质量 |
   |----------|----------|------|------|
   | 目录生成 | gpt-4o-mini | 低 | 良好 |
   | 内容生成 | gpt-4o | 中 | 优秀 |
   | 代码分析 | gpt-4o | 中 | 优秀 |

3. **批量处理策略**：
   - 避免高峰期（UTC 14:00-16:00）批量导入
   - 分批导入大型代码库
   - 使用本地缓存避免重复分析

### 存储优化

**文件存储结构**：
```
/data
├── repositories/          # 代码仓库
│   ├── repo-a/
│   └── repo-b/
├── documents/             # 生成文档
│   ├── repo-a/
│   └── repo-b/
└── cache/                 # 缓存数据
    └── embeddings/
```

**定期清理**：
```bash
# 清理未使用的缓存
find /data/cache -mtime +30 -delete

# 清理已删除仓库的数据
./cleanup-orphaned-data.sh
```

---

## 安全注意事项

### API Key 管理

1. **使用专用密钥**：
   - 为不同环境（dev/staging/prod）创建不同密钥
   - 定期轮换（建议每 90 天）

2. **密钥存储**：
   ```yaml
   # ✅ 正确：使用 Secret
   kubectl create secret generic opendeepwiki-secrets \
     --from-literal=chat-api-key="sk-xxx"

   # ❌ 错误：硬编码在配置中
   environment:
     CHAT_API_KEY: "sk-xxx"
   ```

3. **密钥权限最小化**：
   - 仅授予必要权限
   - 设置 IP 白名单（如支持）
   - 启用使用监控

### JWT 密钥安全

1. **生产环境必须修改默认密钥**：
   ```bash
   # 生成强密钥
   openssl rand -base64 64
   ```

2. **密钥长度至少 32 字符**

3. **定期更换密钥**（注意：更换后用户需重新登录）

### 网络安全

1. **启用 HTTPS**：
   ```yaml
   ingress:
     tls:
       - secretName: opendeepwiki-tls
         hosts:
           - wiki.yourdomain.com
   ```

2. **配置安全响应头**：
   ```yaml
   annotations:
     nginx.ingress.kubernetes.io/configuration-snippet: |
       add_header X-Frame-Options "SAMEORIGIN";
       add_header X-Content-Type-Options "nosniff";
       add_header X-XSS-Protection "1; mode=block";
   ```

3. **限制访问来源**（可选）：
   ```yaml
   annotations:
     nginx.ingress.kubernetes.io/whitelist-source-range: "10.0.0.0/8,172.16.0.0/12"
   ```

### 数据保护

1. **敏感代码处理**：
   - 使用 `.opendeepwikiignore` 排除敏感文件
   - 导入前审查代码内容
   - 定期审计访问日志

2. **备份策略**：
   - 数据库：每日备份
   - 文档：每周完整备份
   - 异地备份：重要数据异地存储

---

## K8s 生产环境最佳实践

### 资源规划

| 规模 | 后端副本 | 前端副本 | MySQL 配置 | 存储 |
|------|----------|----------|------------|------|
| 小型 (< 10 仓库) | 2 | 2 | 1C 1G | 20GB |
| 中型 (10-50 仓库) | 3 | 3 | 2C 4G | 100GB |
| 大型 (> 50 仓库) | 5+ | 5+ | 4C 8G+ | 500GB+ |

### 高可用配置

**Pod 分布策略**：
```yaml
affinity:
  podAntiAffinity:
    requiredDuringSchedulingIgnoredDuringExecution:
      - labelSelector:
          matchLabels:
            app.kubernetes.io/component: backend
        topologyKey: kubernetes.io/hostname
```

**Pod 中断预算**：
```yaml
pdb:
  enabled: true
  backend:
    minAvailable: 2  # 至少保持 2 个副本可用
  frontend:
    minAvailable: 2
```

### 健康检查优化

```yaml
livenessProbe:
  httpGet:
    path: /health
    port: http
  initialDelaySeconds: 60  # 给足启动时间
  periodSeconds: 10
  failureThreshold: 3

readinessProbe:
  httpGet:
    path: /health/ready  # 准备就绪检查
    port: http
  initialDelaySeconds: 10
  periodSeconds: 5
  successThreshold: 1
  failureThreshold: 3
```

### 滚动更新策略

```yaml
strategy:
  type: RollingUpdate
  rollingUpdate:
    maxSurge: 1           # 最多多启动 1 个 Pod
    maxUnavailable: 0     # 不允许中断服务
```

### 资源限制

**严格模式**（推荐）：
```yaml
resources:
  requests:
    memory: "1Gi"
    cpu: "1000m"
  limits:
    memory: "2Gi"  # 严格限制，防止 OOM 影响节点
    cpu: "2000m"
```

### 监控配置

**Prometheus 监控**：
```yaml
annotations:
  prometheus.io/scrape: "true"
  prometheus.io/port: "8080"
  prometheus.io/path: "/metrics"
```

---

## local-path 存储使用建议

### 适用场景

✅ **适合使用**：
- 单节点 K8s（k3s、minikube）
- 边缘计算部署
- 开发测试环境
- 数据可重建的缓存类应用

❌ **不适合使用**：
- 多节点生产集群
- 需要 Pod 跨节点迁移
- 数据不可丢失的业务

### 节点固定策略

使用节点亲和性固定 Pod：
```yaml
nodeSelector:
  node-type: storage  # 标记专用存储节点

affinity:
  nodeAffinity:
    requiredDuringSchedulingIgnoredDuringExecution:
      nodeSelectorTerms:
        - matchExpressions:
            - key: kubernetes.io/hostname
              operator: In
              values:
                - node-1  # 固定到 node-1
```

### 数据备份策略

由于 local-path 数据绑定节点，必须定期备份：

```bash
#!/bin/bash
# backup-local-path.sh

NAMESPACE="opendeepwiki"
POD_NAME=$(kubectl get pods -n $NAMESPACE -l app.kubernetes.io/component=backend -o jsonpath='{.items[0].metadata.name}')
BACKUP_DIR="/backup/$(date +%Y%m%d)"

# 备份数据
kubectl exec -n $NAMESPACE $POD_NAME -- tar czf - /data > $BACKUP_DIR/opendeepwiki-backup.tar.gz

# 保留最近 7 天备份
find /backup -name "*.tar.gz" -mtime +7 -delete
```

### 迁移到共享存储

如需从 local-path 迁移到共享存储：

1. **创建新的 PVC（共享存储）**：
   ```yaml
   apiVersion: v1
   kind: PersistentVolumeClaim
   metadata:
     name: opendeepwiki-data-shared
   spec:
     storageClassName: nfs-client  # 或其他共享存储
     accessModes:
       - ReadWriteMany
     resources:
       requests:
         storage: 100Gi
   ```

2. **数据迁移**：
   ```bash
   # 创建临时 Pod 挂载两个 PVC
   kubectl apply -f migration-pod.yaml

   # 复制数据
   kubectl exec migration-pod -- cp -r /data-old/* /data-new/
   ```

3. **切换 PVC**：
   修改 Deployment 使用新的 PVC

---

## AI 配置优化

### 成本控制

**分层使用策略**：

| 任务 | 模型 | 成本/1K Token |
|------|------|---------------|
| 简单分类/标签 | gpt-4o-mini | $0.0006 |
| 文档生成 | gpt-4o | $0.005 |
| 复杂代码分析 | gpt-4o | $0.005 |

### 缓存策略

1. **启用结果缓存**：
   ```yaml
   environment:
     WIKI_ENABLE_CACHE: "true"
     WIKI_CACHE_TTL: "3600"  # 1小时
   ```

2. **Embedding 缓存**：
   - 文本向量结果缓存到本地
   - 相同查询直接返回缓存结果

### 错误处理

**重试策略**：
```yaml
environment:
  WIKI_MAX_RETRIES: "3"
  WIKI_RETRY_DELAY: "1000"  # 1秒
```

**降级方案**：
- API 限流时自动切换到低级模型
- 记录失败任务稍后重试

---

## 监控与告警

### 关键指标

| 指标 | 告警阈值 | 说明 |
|------|----------|------|
| CPU 使用率 | > 80% | 资源不足 |
| 内存使用率 | > 85% | 可能 OOM |
| 磁盘使用率 | > 80% | 存储不足 |
| 请求延迟 | > 2s | 性能下降 |
| 错误率 | > 1% | 服务异常 |
| AI API 失败率 | > 5% | 检查 API Key |

### 日志收集

**Fluentd 配置示例**：
```xml
<source>
  @type kubernetes
  @id input_kubernetes
  path /var/log/containers/opendeepwiki*.log
  pos_file /var/log/fluentd-opendeepwiki.log.pos
  tag opendeepwiki.*
</source>
```

### 告警规则

**Prometheus Alertmanager**：
```yaml
groups:
  - name: opendeepwiki
    rules:
      - alert: OpenDeepWikiHighErrorRate
        expr: rate(http_requests_total{status=~"5.."}[5m]) > 0.01
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "OpenDeepWiki 错误率过高"
```

---

## 故障恢复

### 常见故障处理

**Pod 频繁重启**：
```bash
# 检查 OOM
kubectl describe pod -n opendeepwiki opendeepwiki-backend-xxx

# 查看退出码
kubectl get pod -n opendeepwiki opendeepwiki-backend-xxx -o jsonpath='{.status.containerStatuses[0].lastState}'
```

**数据库连接失败**：
```bash
# 检查 MySQL 状态
kubectl get pods -n opendeepwiki | grep mysql

# 查看连接数
kubectl exec -n opendeepwiki opendeepwiki-mysql-primary-xxx -- mysql -e "SHOW STATUS LIKE 'Threads_connected';"
```

**AI 功能不可用**：
```bash
# 测试 API 连通性
kubectl exec -n opendeepwiki deployment/opendeepwiki-backend -- curl -s $ENDPOINT/health

# 检查 API Key 余额
```

---

## 相关文档

- [部署手册](./deployment.md)
- [用户操作指南](./user-guide.md)
- [数据导入教程](./data-import.md)
