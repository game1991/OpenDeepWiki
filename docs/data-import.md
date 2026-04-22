# OpenDeepWiki 数据导入教程

本文档详细介绍如何将各种数据源导入到 OpenDeepWiki 知识库系统。

---

## 目录

1. [支持的导入源](#支持的导入源)
2. [从 Git 仓库导入](#从-git-仓库导入)
3. [ZIP 文件上传](#zip-文件上传)
4. [本地文件导入](#本地文件导入)
5. [批量导入技巧](#批量导入技巧)
6. [数据更新策略](#数据更新策略)

---

## 支持的导入源

| 数据源 | 支持情况 | 认证方式 |
|--------|----------|----------|
| **GitHub** | ✅ 支持 | Token / SSH Key |
| **GitLab** | ✅ 支持 | Token / SSH Key |
| **Gitee** | ✅ 支持 | Token |
| **Gitea** | ✅ 支持 | Token |
| **AtomGit** | ✅ 支持 | Token |
| **ZIP 文件** | ✅ 支持 | 无需认证 |
| **本地目录** | ✅ 支持 | 无需认证 |

---

## 从 Git 仓库导入

### GitHub 导入

#### 公开仓库

1. 复制仓库 HTTPS URL：
   ```
   https://github.com/username/repository.git
   ```

2. 在 OpenDeepWiki 中选择「Git 仓库」导入方式

3. 粘贴 URL，选择分支（默认 `main`）

4. 点击「开始导入」

#### 私有仓库

需要创建 Personal Access Token：

1. 登录 GitHub → Settings → Developer settings → Personal access tokens

2. 点击 "Generate new token (classic)"

3. 勾选权限：
   - ✅ `repo` - 完全控制私有仓库

4. 生成并复制 Token

5. 在导入页面填写：
   - 仓库 URL
   - Access Token
   - 分支名称

### GitLab 导入

#### 公开仓库

使用 HTTPS URL：
```
https://gitlab.com/username/repository.git
```

#### 私有仓库

创建 Access Token：

1. GitLab → User Settings → Access Tokens

2. 创建 Token，勾选：
   - ✅ `read_repository`
   - ✅ `read_api`

3. 使用 Token 导入

#### 自托管 GitLab

使用完整 URL：
```
https://gitlab.company.com/group/project.git
```

### Gitee 导入

创建私人令牌：

1. Gitee → 设置 → 私人令牌

2. 生成令牌，勾选 `projects` 权限

3. 使用令牌导入私有仓库

### 导入配置选项

| 选项 | 说明 | 建议 |
|------|------|------|
| **代码分析** | 分析代码结构和依赖 | ✅ 启用 |
| **生成架构图** | 生成 Mermaid 图表 | ✅ 启用 |
| **语言** | 文档生成语言 | 根据项目选择 |
| **排除目录** | 跳过的目录 | `node_modules`, `.git`, `dist` |
| **文件类型过滤** | 只分析指定类型 | 根据需求设置 |

### 排除目录配置

常用排除模式：

```
# 依赖目录
node_modules/
vendor/
__pycache__/

# 构建产物
dist/
build/
target/
*.min.js

# 版本控制
.git/
.svn/

# 测试数据
test/fixtures/
*.test.js
*.spec.js

# 文档（已存在）
docs/
README.md
```

---

## ZIP 文件上传

### 准备 ZIP 文件

1. 确保项目结构完整：
   ```
   project-folder/
   ├── src/           # 源代码
   ├── package.json   # 依赖配置
   ├── README.md      # 说明文档
   └── ...
   ```

2. 压缩为 ZIP：
   ```bash
   # Linux/macOS
   zip -r project.zip project-folder/ -x "*.git*" -x "*node_modules*"

   # Windows (PowerShell)
   Compress-Archive -Path project-folder -DestinationPath project.zip
   ```

3. 确保 ZIP 文件 < 500MB

### 上传步骤

1. 选择「ZIP 文件」导入方式
2. 拖放 ZIP 文件或点击选择
3. 等待上传完成
4. 配置分析选项
5. 开始处理

---

## 本地文件导入

适用于直接部署环境下的本地导入。

### Docker 方式

1. 复制文件到容器：
   ```bash
   docker cp /local/path/to/project opendeepwiki:/data/imports/
   ```

2. 在界面选择「本地目录」

3. 指定目录路径：
   ```
   /data/imports/project
   ```

### Kubernetes 方式

1. 复制到 Pod：
   ```bash
   kubectl cp /local/project opendeepwiki/opendeepwiki-backend-xxx:/data/imports/
   ```

2. 通过界面导入

---

## 批量导入技巧

### 使用脚本批量导入

创建批量导入脚本：

```bash
#!/bin/bash
# bulk-import.sh

REPOS=(
  "https://github.com/user/repo1.git"
  "https://github.com/user/repo2.git"
  "https://github.com/user/repo3.git"
)

API_URL="http://localhost:8080/api"
API_KEY="your-api-key"

for repo in "${REPOS[@]}"; do
  echo "Importing: $repo"

  curl -X POST "$API_URL/repositories" \
    -H "Authorization: Bearer $API_KEY" \
    -H "Content-Type: application/json" \
    -d "{
      \"name\": \"$(basename $repo .git)\",
      \"url\": \"$repo\",
      \"type\": \"git\",
      \"settings\": {
        \"analyzeCode\": true,
        \"generateDiagrams\": true
      }
    }"

  sleep 5  # 避免请求过快
done
```

### 批量导入配置模板

创建 `import-config.json`：

```json
{
  "repositories": [
    {
      "name": "Project A",
      "url": "https://github.com/org/project-a.git",
      "branch": "main",
      "settings": {
        "analyzeCode": true,
        "generateDiagrams": true,
        "language": "zh",
        "excludePatterns": ["node_modules", "dist", ".git"]
      }
    },
    {
      "name": "Project B",
      "url": "https://github.com/org/project-b.git",
      "branch": "develop",
      "settings": {
        "analyzeCode": true,
        "generateDiagrams": false,
        "language": "en"
      }
    }
  ]
}
```

执行批量导入：

```bash
curl -X POST http://localhost:8080/api/bulk-import \
  -H "Content-Type: application/json" \
  -d @import-config.json
```

---

## 数据更新策略

### 自动同步

#### 配置定时同步

1. 进入仓库设置
2. 开启「自动同步」
3. 设置同步间隔：
   - 每 15 分钟
   - 每小时
   - 每天

#### Webhook 自动触发（推荐）

配置 Git 仓库 Webhook，在代码推送时自动更新：

**GitHub Webhook 配置**：

1. 仓库 → Settings → Webhooks → Add webhook

2. 配置参数：
   - **Payload URL**: `https://wiki.yourdomain.com/api/webhook/github`
   - **Content type**: `application/json`
   - **Secret**: 设置密钥
   - **Events**: 选择 "Push"

3. 在 OpenDeepWiki 中配置相同的 Secret

### 手动更新

#### 单仓库更新

1. 进入知识库
2. 点击「同步」按钮
3. 选择同步方式：
   - **快速同步**：仅拉取更新
   - **完全重建**：重新分析全部代码

#### 批量更新

```bash
# 更新所有仓库
curl -X POST http://localhost:8080/api/sync-all \
  -H "Authorization: Bearer YOUR_API_KEY"

# 更新指定仓库
curl -X POST http://localhost:8080/api/sync \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"repositoryId": "repo-id-123"}'
```

### 增量更新 vs 全量更新

| 方式 | 适用场景 | 耗时 | 资源占用 |
|------|----------|------|----------|
| **增量更新** | 日常代码更新 | 短 | 低 |
| **全量更新** | 结构大改后 | 长 | 高 |

**建议**：
- 日常开发：增量更新
- 版本发布：全量更新
- 每周一次：全量更新（清理冗余数据）

---

## 导入问题排查

### 导入失败常见问题

#### 1. 认证失败

**现象**：提示 "Authentication failed"

**解决**：
- 检查 Token 是否过期
- 确认 Token 权限是否正确
- 检查仓库 URL 是否正确

#### 2. 仓库过大

**现象**：导入超时或内存不足

**解决**：
- 使用 ZIP 方式分段导入
- 增加系统内存
- 排除大文件和目录

#### 3. 网络超时

**现象**："Connection timeout"

**解决**：
- 检查网络连接
- 使用镜像仓库
- 调整超时设置

#### 4. 分析失败

**现象**：代码导入成功但无文档生成

**解决**：
- 检查日志了解具体错误
- 确认语言支持
- 检查文件编码

### 查看导入日志

**Docker**：
```bash
docker compose logs -f opendeepwiki | grep -i import
```

**Kubernetes**：
```bash
kubectl logs -n opendeepwiki deployment/opendeepwiki-backend -f | grep -i import
```

---

## 性能优化建议

### 大仓库处理

| 仓库大小 | 建议 |
|----------|------|
| < 100MB | 直接导入 |
| 100MB - 500MB | 排除依赖目录后导入 |
| > 500MB | 分批导入或使用 ZIP |

### 并发导入

如需导入多个仓库，建议：
- 同时导入数 ≤ 3
- 每个导入间隔 5 秒
- 避免高峰期导入

---

## 相关文档

- [部署手册](./deployment.md)
- [用户操作指南](./user-guide.md)
- [最佳实践手册](./best-practices.md)
