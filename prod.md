# OpenDeepWiki 知识库系统搭建指南 - 专业提示词

## 任务目标
基于 OpenDeepWiki 开源项目（https://github.com/AIDotNet/OpenDeepWiki）构建一套完整的 AI 驱动知识库系统。该系统是一个基于 .NET 9 和 Semantic Kernel 开发的 AI 驱动代码知识库平台，旨在提供强大的知识管理和协作能力。

---

## 项目技术栈

| 层级 | 技术 |
|------|------|
| 后端 | C# (.NET 9) + Semantic Kernel |
| 前端 | TypeScript + Next.js |
| 数据库 | SQLite（默认）、MySQL、PostgreSQL、SQL Server |
| AI 框架 | Semantic Kernel |
| 部署 | Docker + Docker Compose / Kubernetes + Helm |
| K8s 依赖 | Bitnami MySQL/PostgreSQL Chart（可选） |

---

## 核心功能特性

1. **代码分析**：自动分析代码结构，生成 Mermaid 图表
2. **文档生成**：自动生成 README.md、项目概述、更新日志
3. **AI 对话**：与 AI 对话获取详细代码信息
4. **知识库构建**：递归扫描目录，智能过滤，生成文档目录结构
5. **SEO 优化**：基于 Next.js 生成 SEO 友好的文档
6. **多语言支持**：中文、英文、法文等
7. **智能上下文压缩**：90-95% 压缩率，使用 Prompt Encoding Compression 技术
8. **增量更新**：支持仓库自动增量更新
9. **MCP 支持**：可作为单个仓库的 MCP Server 运行

---

## 执行计划

### 阶段一：环境准备

**操作系统要求**：
- Linux/macOS/Windows (WSL2)
- Docker 20.10+ 和 Docker Compose 2.0+
- Git

**必要环境变量配置**（需在 compose.yaml 中配置）：
```yaml
# AI 对话配置
CHAT_API_KEY: your_api_key
ENDPOINT: https://api.openai.com/v1/chat/completions
CHAT_REQUEST_TYPE: OpenAI

# 目录生成 AI 配置
WIKI_CATALOG_MODEL: gpt-4o-mini
WIKI_CATALOG_API_KEY: your_api_key

# 内容生成 AI 配置
WIKI_CONTENT_MODEL: gpt-4o
WIKI_CONTENT_API_KEY: your_api_key
```

### 阶段二：系统部署（方案二选一）

#### 方案 A：Docker Compose 部署（适合单机/开发环境）

1. 克隆仓库
   ```bash
   git clone https://github.com/AIDotNet/OpenDeepWiki.git
   cd OpenDeepWiki
   ```

2. 编辑 compose.yaml 配置环境变量和 AI 提供商

3. 构建并启动
   ```bash
   docker compose build
   docker compose up -d
   # 或使用 Makefile: make build && make up
   ```

4. 验证部署
   - 前端访问：http://localhost:3000
   - 后端 API：http://localhost:8080
   - 默认管理员账号：`admin@routin.ai` / `Admin@123`

#### 方案 B：Kubernetes + Helm 部署（适合生产环境）

**前置要求**：
- Kubernetes 1.24+ 集群
- Helm 3.12+ 已安装
- Ingress Controller（如 Nginx Ingress）
- StorageClass（用于 PVC）

**部署步骤**：

1. **添加 Helm Chart 仓库**（或使用本地 Chart）
   ```bash
   # 方式1：使用预构建的 Chart（假设已发布到 Helm 仓库）
   helm repo add opendeepwiki https://AIDotNet.github.io/OpenDeepWiki/charts
   helm repo update

   # 方式2：使用本地 Chart（推荐，便于自定义）
   git clone https://github.com/AIDotNet/OpenDeepWiki.git
   cd OpenDeepWiki/charts/opendeepwiki
   ```

2. **创建命名空间和配置文件**
   ```bash
   kubectl create namespace opendeepwiki

   # 创建环境变量 Secret
   kubectl create secret generic opendeepwiki-secrets \
     --namespace opendeepwiki \
     --from-literal=chat-api-key="your-chat-api-key" \
     --from-literal=catalog-api-key="your-catalog-api-key" \
     --from-literal=content-api-key="your-content-api-key" \
     --from-literal=jwt-secret="your-jwt-secret-key-min-32-chars-long"
   ```

3. **配置 values.yaml**
   创建自定义 values 文件 `custom-values.yaml`：
   ```yaml
   # 后端服务配置
   backend:
     replicaCount: 2
     image:
       repository: crpi-j9ha7sxwhatgtvj4.cn-shenzhen.personal.cr.aliyuncs.com/open-deepwiki/opendeepwiki
       tag: latest
       pullPolicy: IfNotPresent
     resources:
       requests:
         memory: "512Mi"
         cpu: "500m"
       limits:
         memory: "2Gi"
         cpu: "2000m"
     persistence:
       enabled: true
       storageClass: "local-path"  # 使用 local-path 本地存储，适合单节点 K8s 或边缘部署
       size: 10Gi
       accessMode: ReadWriteOnce
     environment:
       ASPNETCORE_ENVIRONMENT: Production
       URLS: http://+:8080
       Database__Type: sqlite
       ConnectionStrings__Default: Data Source=/data/opendeepwiki.db
       REPOSITORIES_DIRECTORY: /data
       ENDPOINT: https://api.openai.com/v1
       CHAT_REQUEST_TYPE: OpenAI
       WIKI_CATALOG_MODEL: gpt-4o
       WIKI_CATALOG_ENDPOINT: https://api.openai.com/v1
       WIKI_CATALOG_REQUEST_TYPE: OpenAI
       WIKI_CONTENT_MODEL: gpt-4o
       WIKI_CONTENT_ENDPOINT: https://api.openai.com/v1
       WIKI_CONTENT_REQUEST_TYPE: OpenAI
       WIKI_PARALLEL_COUNT: "5"
       WIKI_LANGUAGES: "en,zh"
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

   # 前端服务配置
   frontend:
     replicaCount: 2
     image:
       repository: crpi-j9ha7sxwhatgtvj4.cn-shenzhen.personal.cr.aliyuncs.com/open-deepwiki/opendeepwiki-web
       tag: latest
       pullPolicy: IfNotPresent
     resources:
       requests:
         memory: "256Mi"
         cpu: "250m"
       limits:
         memory: "512Mi"
         cpu: "500m"
     environment:
       NODE_ENV: production
       API_PROXY_URL: http://opendeepwiki-backend:8080

   # Ingress 配置
   ingress:
     enabled: true
     className: nginx
     annotations:
       nginx.ingress.kubernetes.io/proxy-body-size: "100m"
       nginx.ingress.kubernetes.io/proxy-read-timeout: "300"
       nginx.ingress.kubernetes.io/proxy-send-timeout: "300"
     hosts:
       - host: opendeepwiki.yourdomain.com
         paths:
           - path: /
             pathType: Prefix
             service: frontend
           - path: /api
             pathType: Prefix
             service: backend
     tls:
       - secretName: opendeepwiki-tls
         hosts:
           - opendeepwiki.yourdomain.com
   ```

4. **执行 Helm 部署**
   ```bash
   helm install opendeepwiki ./opendeepwiki \
     --namespace opendeepwiki \
     --values custom-values.yaml
   ```

5. **验证部署**
   ```bash
   # 查看 Pod 状态
   kubectl get pods -n opendeepwiki

   # 查看服务
   kubectl get svc -n opendeepwiki

   # 查看 Ingress
   kubectl get ingress -n opendeepwiki
   ```

6. **访问系统**
   - 配置 DNS 将域名指向 Ingress Controller IP
   - 访问：https://opendeepwiki.yourdomain.com
   - 默认管理员账号：`admin@routin.ai` / `Admin@123`

**Helm Chart 目录结构**：
```
charts/opendeepwiki/
├── Chart.yaml              # Chart 元数据
├── values.yaml             # 默认配置值
├── values-production.yaml  # 生产环境配置示例
├── templates/
│   ├── _helpers.tpl        # 模板辅助函数
│   ├── deployment.yaml     # Deployment 模板
│   ├── service.yaml        # Service 模板
│   ├── ingress.yaml        # Ingress 模板
│   ├── pvc.yaml            # 持久卷声明模板
│   ├── secret.yaml         # Secret 模板（可选）
│   ├── configmap.yaml      # ConfigMap 模板
│   ├── hpa.yaml            # 水平自动扩缩容模板
│   └── NOTES.txt           # 部署后提示信息
└── README.md               # Chart 使用说明
```

**高级配置选项**：

1. **使用 MySQL 替代 SQLite**（推荐用于生产环境）：
   ```yaml
   # values.yaml 中启用 MySQL，使用 local-path 本地存储
   # local-path 是轻量级本地存储方案，适合单节点或边缘 K8s 集群
   mysql:
     enabled: true
     architecture: standalone
     auth:
       rootPassword: "opendeepwiki-root-pass"
       username: "opendeepwiki"
       password: "opendeepwiki-pass"
       database: "opendeepwiki"
     primary:
       persistence:
         enabled: true
         size: 10Gi
         storageClass: "local-path"
       resources:
         requests:
           memory: "512Mi"
           cpu: "250m"
         limits:
           memory: "1Gi"
           cpu: "500m"

   backend:
     # 数据库配置自动指向 MySQL service
     environment:
       Database__Type: mysql
       ConnectionStrings__Default: >
         Server=opendeepwiki-mysql-primary;
         Port=3306;
         Database=opendeepwiki;
         User Id=opendeepwiki;
         Password=opendeepwiki-pass;
         Charset=utf8mb4;
     # 依赖检查 init container
     initContainers:
       - name: wait-for-mysql
         image: busybox:1.36
         command:
           - sh
           - -c
           - |
             until nc -z opendeepwiki-mysql-primary 3306; do
               echo "Waiting for MySQL to be ready..."
               sleep 2
             done
             echo "MySQL is ready!"
   ```

2. **使用 PostgreSQL 替代 SQLite**：
   ```yaml
   # values.yaml 中启用 PostgreSQL
   postgresql:
     enabled: true
     auth:
       username: opendeepwiki
       password: your-db-password
       database: opendeepwiki
     primary:
       persistence:
         enabled: true
         size: 10Gi

   backend:
     environment:
       Database__Type: postgresql
       ConnectionStrings__Default: >
         Host=opendeepwiki-postgresql;
         Port=5432;
         Database=opendeepwiki;
         Username=opendeepwiki;
         Password=your-db-password;
     initContainers:
       - name: wait-for-postgres
         image: busybox:1.36
         command:
           - sh
           - -c
           - |
             until nc -z opendeepwiki-postgresql 5432; do
               echo "Waiting for PostgreSQL to be ready..."
               sleep 2
             done
             echo "PostgreSQL is ready!"
   ```

3. **启用 HPA 自动扩缩容**：
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

4. **配置 Pod Disruption Budget**：
   ```yaml
   pdb:
     enabled: true
     backend:
       minAvailable: 1
     frontend:
       minAvailable: 1
   ```

### 阶段三：数据导入

**支持的导入源**：
- GitHub / GitLab / AtomGit / Gitee / Gitea 代码仓库
- ZIP 文件上传
- 本地文件导入

**导入步骤**：
1. 登录管理后台
2. 创建仓库目录
3. 选择导入方式（Git 仓库链接或文件上传）
4. 配置仓库参数（是否启用代码依赖分析等）
5. 启动自动分析生成文档

### 阶段四：系统配置与优化

1. **数据库配置**：
   - 默认 SQLite 无需额外配置
   - 如需 PostgreSQL：设置 `DB_TYPE=postgresql` 和 `CONNECTION_STRING`

2. **AI 提供商配置**：
   - 支持 OpenAI、AzureOpenAI、Anthropic 等
   - 在 compose.yaml 中配置对应的环境变量

3. **MCP Server 模式**：
   - 可作为单个仓库的 MCP Server 运行
   - 支持 Model Context Protocol 协议

---

## 输出交付物

请生成以下专业文档：

1. **部署手册** (`docs/deployment.md`)
   - Docker Compose 详细安装步骤
   - Kubernetes + Helm 完整部署指南
   - 环境变量配置说明
   - 常见问题排查

2. **Helm Chart 包** (`charts/opendeepwiki/`)
   - 完整的 Chart 模板文件
   - values.yaml 默认配置
   - values-production.yaml 生产环境配置
   - 多环境（dev/staging/prod）配置示例

3. **用户操作指南** (`docs/user-guide.md`)
   - 界面功能介绍
   - 知识库创建流程
   - AI 对话功能使用

4. **数据导入教程** (`docs/data-import.md`)
   - 各类数据源导入方法
   - 批量导入技巧
   - 数据更新策略

5. **最佳实践手册** (`docs/best-practices.md`)
   - 知识库组织建议
   - 性能优化技巧
   - 安全注意事项
   - K8s 生产环境最佳实践

6. **配置文件模板** (`config/`)
   - compose.yaml 生产环境配置
   - 环境变量示例文件
   - Helm values 配置文件模板
   - Kubernetes 原生 YAML 配置（无 Helm 场景使用）

---

## 约束条件

1. 所有文档需使用中文编写
2. 命令示例需适配 Linux/WSL2 环境
3. 配置文件需考虑安全性（敏感信息使用环境变量或 K8s Secret）
4. 提供 Docker Compose 和 Kubernetes + Helm 两种部署方案
5. 文档需包含故障排查章节
6. Helm Chart 需遵循 Helm 3 最佳实践
7. Kubernetes 资源需配置合理的资源限制和请求
8. 生产环境配置需支持高可用（多副本、PDB、HPA）
9. **local-path 存储限制**：
   - 仅适用于单节点 K8s 集群或边缘部署场景
   - Pod 重新调度到不同节点时数据会丢失
   - 生产多节点集群建议使用 NFS/Ceph/Rook 等共享存储

---

## 验收标准

- [ ] 完成 OpenDeepWiki 本地部署并正常运行（Docker Compose 方式）
- [ ] 成功导入至少一个示例代码仓库
- [ ] AI 对话功能正常工作
- [ ] 生成完整的文档套件（部署手册、用户指南、数据导入教程、最佳实践）
- [ ] 所有配置文件经过验证可正常使用
- [ ] **Helm Chart 包完整可用**
  - [ ] Chart 可以通过 `helm lint` 验证
  - [ ] Chart 可以通过 `helm template` 渲染
  - [ ] 支持自定义 values 配置
  - [ ] 包含合理的资源限制和请求
- [ ] **Kubernetes 部署验证**
  - [ ] 可以通过 Helm 成功部署到 K8s 集群
  - [ ] 后端和前端 Pod 正常运行
  - [ ] Ingress 配置正确，可通过域名访问
  - [ ] PVC 数据持久化正常工作
  - [ ] 滚动更新策略配置合理

---

## 参考资源

- 项目仓库：https://github.com/AIDotNet/OpenDeepWiki
- 技术栈：.NET 9, Semantic Kernel, Next.js, Docker
- 默认访问地址：http://localhost:3000
- Bitnami MySQL Chart：https://github.com/bitnami/charts/tree/main/bitnami/mysql

---

## 附录：Helm Chart 关键模板示例

### Chart.yaml（带依赖）
```yaml
apiVersion: v2
name: opendeepwiki
description: A Helm chart for OpenDeepWiki - AI-Driven Code Knowledge Base
type: application
version: 0.1.0
appVersion: "1.0.0"
keywords:
  - opendeepwiki
  - ai
  - knowledge-base
  - documentation
maintainers:
  - name: OpenDeepWiki Team
    email: support@opendeepwiki.com
dependencies:
  # MySQL 依赖（可选）
  - name: mysql
    version: 9.x.x
    repository: oci://registry-1.docker.io/bitnamicharts
    condition: mysql.enabled
    tags:
      - database
  # PostgreSQL 依赖（可选）
  - name: postgresql
    version: 13.x.x
    repository: oci://registry-1.docker.io/bitnamicharts
    condition: postgresql.enabled
    tags:
      - database
```

### 依赖管理说明

**添加依赖仓库**：
```bash
# 添加 Bitnami 仓库
helm repo add bitnami https://charts.bitnami.com/bitnami

# 更新依赖
helm dependency update charts/opendeepwiki

# 查看依赖列表
helm dependency list charts/opendeepwiki
```

**构建时自动拉取依赖**：
```bash
# 在 Chart 目录中
helm package . --sign --key 'OpenDeepWiki Signing Key'
```

### templates/deployment.yaml（后端示例，带依赖检查）
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{ include "opendeepwiki.fullname" . }}-backend
  labels:
    {{- include "opendeepwiki.labels" . | nindent 4 }}
    app.kubernetes.io/component: backend
spec:
  replicas: {{ .Values.backend.replicaCount }}
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0
  selector:
    matchLabels:
      {{- include "opendeepwiki.selectorLabels" . | nindent 6 }}
      app.kubernetes.io/component: backend
  template:
    metadata:
      labels:
        {{- include "opendeepwiki.selectorLabels" . | nindent 8 }}
        app.kubernetes.io/component: backend
    spec:
      # Init Containers：等待依赖服务就绪
      {{- if .Values.backend.initContainers }}
      initContainers:
        {{- toYaml .Values.backend.initContainers | nindent 8 }}
      {{- end }}

      containers:
        - name: backend
          image: "{{ .Values.backend.image.repository }}:{{ .Values.backend.image.tag }}"
          imagePullPolicy: {{ .Values.backend.image.pullPolicy }}
          ports:
            - name: http
              containerPort: 8080
              protocol: TCP
          env:
            {{- range $key, $value := .Values.backend.environment }}
            - name: {{ $key }}
              value: {{ $value | quote }}
            {{- end }}
            {{- range .Values.backend.secrets }}
            - name: {{ .name }}
              valueFrom:
                secretKeyRef:
                  name: {{ .secretName }}
                  key: {{ .key }}
            {{- end }}
          resources:
            {{- toYaml .Values.backend.resources | nindent 12 }}
          volumeMounts:
            - name: data
              mountPath: /data
          livenessProbe:
            httpGet:
              path: /health
              port: http
            initialDelaySeconds: 30
            periodSeconds: 10
          readinessProbe:
            httpGet:
              path: /health
              port: http
            initialDelaySeconds: 5
            periodSeconds: 5
      volumes:
        - name: data
          persistentVolumeClaim:
            claimName: {{ include "opendeepwiki.fullname" . }}-data
```

### values.yaml（完整示例）
```yaml
# OpenDeepWiki Helm Chart 默认配置

# ==========================================
# 后端服务配置
# ==========================================
backend:
  replicaCount: 2

  image:
    repository: crpi-j9ha7sxwhatgtvj4.cn-shenzhen.personal.cr.aliyuncs.com/open-deepwiki/opendeepwiki
    tag: latest
    pullPolicy: IfNotPresent

  resources:
    requests:
      memory: "512Mi"
      cpu: "500m"
    limits:
      memory: "2Gi"
      cpu: "2000m"

  # 持久化存储（SQLite 模式需要）
  persistence:
    enabled: true
    storageClass: "local-path"
    size: 10Gi
    accessMode: ReadWriteOnce

  # 环境变量配置
  environment:
    ASPNETCORE_ENVIRONMENT: Production
    URLS: http://+:8080
    # 数据库配置（根据启用的数据库类型调整）
    Database__Type: sqlite
    ConnectionStrings__Default: Data Source=/data/opendeepwiki.db
    REPOSITORIES_DIRECTORY: /data
    # AI 配置
    ENDPOINT: https://api.openai.com/v1
    CHAT_REQUEST_TYPE: OpenAI
    WIKI_CATALOG_MODEL: gpt-4o
    WIKI_CATALOG_ENDPOINT: https://api.openai.com/v1
    WIKI_CATALOG_REQUEST_TYPE: OpenAI
    WIKI_CONTENT_MODEL: gpt-4o
    WIKI_CONTENT_ENDPOINT: https://api.openai.com/v1
    WIKI_CONTENT_REQUEST_TYPE: OpenAI
    WIKI_PARALLEL_COUNT: "5"
    WIKI_LANGUAGES: "en,zh"

  # 敏感信息从 Secret 引用
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

  # Init Containers：等待依赖服务就绪
  initContainers: []
  # 使用 MySQL 时取消注释以下内容：
  # initContainers:
  #   - name: wait-for-mysql
  #     image: busybox:1.36
  #     command:
  #       - sh
  #       - -c
  #       - |
  #         until nc -z opendeepwiki-mysql-primary 3306; do
  #           echo "Waiting for MySQL to be ready..."
  #           sleep 2
  #         done
  #         echo "MySQL is ready!"

# ==========================================
# 前端服务配置
# ==========================================
frontend:
  replicaCount: 2

  image:
    repository: crpi-j9ha7sxwhatgtvj4.cn-shenzhen.personal.cr.aliyuncs.com/open-deepwiki/opendeepwiki-web
    tag: latest
    pullPolicy: IfNotPresent

  resources:
    requests:
      memory: "256Mi"
      cpu: "250m"
    limits:
      memory: "512Mi"
      cpu: "500m"

  environment:
    NODE_ENV: production
    API_PROXY_URL: http://opendeepwiki-backend:8080

# ==========================================
# MySQL 数据库配置（可选）
# ==========================================
mysql:
  enabled: false
  architecture: standalone
  auth:
    rootPassword: ""
    username: opendeepwiki
    password: ""
    database: opendeepwiki
  primary:
    persistence:
      enabled: true
      size: 10Gi
      storageClass: "local-path"
    resources:
      requests:
        memory: "512Mi"
        cpu: "250m"
      limits:
        memory: "1Gi"
        cpu: "500m"

# ==========================================
# PostgreSQL 数据库配置（可选）
# ==========================================
postgresql:
  enabled: false
  auth:
    username: opendeepwiki
    password: ""
    database: opendeepwiki
  primary:
    persistence:
      enabled: true
      size: 10Gi
      storageClass: "local-path"

# ==========================================
# Ingress 配置
# ==========================================
ingress:
  enabled: true
  className: nginx
  annotations:
    nginx.ingress.kubernetes.io/proxy-body-size: "100m"
    nginx.ingress.kubernetes.io/proxy-read-timeout: "300"
    nginx.ingress.kubernetes.io/proxy-send-timeout: "300"
  hosts:
    - host: opendeepwiki.local
      paths:
        - path: /
          pathType: Prefix
          service: frontend
        - path: /api
          pathType: Prefix
          service: backend
  tls: []
  # - secretName: opendeepwiki-tls
  #   hosts:
  #     - opendeepwiki.local

# ==========================================
# 自动扩缩容配置
# ==========================================
autoscaling:
  enabled: false
  backend:
    minReplicas: 2
    maxReplicas: 10
    targetCPUUtilizationPercentage: 70
  frontend:
    minReplicas: 2
    maxReplicas: 5
    targetCPUUtilizationPercentage: 70

# ==========================================
# Pod Disruption Budget 配置
# ==========================================
pdb:
  enabled: false
  backend:
    minAvailable: 1
  frontend:
    minAvailable: 1
```
