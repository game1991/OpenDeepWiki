# OpenDeepWiki Harbor 构建部署指南

## 修复内容

已修复 Git 私有仓库克隆问题：
- **问题**: LibGit2Sharp 的 `CredentialsProvider` 在容器环境无法正确认证
- **解决**: 将用户名/密码嵌入 URL (`https://user:pass@host/path`) 后再克隆

## 文件修改

```
src/OpenDeepWiki/Services/Repositories/RepositoryAnalyzer.cs
```

修改 `CloneRepositoryAsync` 方法，新增凭据嵌入 URL 逻辑。

---

## 快速开始

### 1. 登录 Harbor

```bash
cd /tmp/OpenDeepWiki
docker login harbor.eagleye.com
```

### 2. 构建并推送镜像

**使用 Makefile（推荐）:**
```bash
# 登录 Harbor
make harbor-login

# 构建镜像
make harbor-build

# 推送镜像
make harbor-push
```

**使用脚本:**
```bash
chmod +x build-and-push.sh
./build-and-push.sh
```

**手动构建:**
```bash
# 后端
docker build -f src/OpenDeepWiki/Dockerfile \
  -t harbor.eagleye.com/open-deep-wiki/opendeepwiki-backend:latest .

# 前端
docker build -f web/Dockerfile \
  -t harbor.eagleye.com/open-deep-wiki/opendeepwiki-web:latest ./web

# 推送
docker push harbor.eagleye.com/open-deep-wiki/opendeepwiki-backend:latest
docker push harbor.eagleye.com/open-deep-wiki/opendeepwiki-web:latest
```

---

## 部署到 Kubernetes

### 1. 创建 Harbor 密钥

```bash
kubectl create secret docker-registry harbor-secret \
  --docker-server=harbor.eagleye.com \
  --docker-username=<你的Harbor用户名> \
  --docker-password=<你的Harbor密码> \
  -n opendeepwiki
```

### 2. 使用 Helm 部署

```bash
# 使用 Harbor 专用配置
helm upgrade --install opendeepwiki ./charts/opendeepwiki \
  -f config/values-kind-harbor.yaml \
  -n opendeepwiki

# 或使用 Makefile
make harbor-deploy
```

### 3. 验证部署

```bash
# 查看 Pod 状态
kubectl get pod -n opendeepwiki

# 查看日志
kubectl logs -n opendeepwiki deployment/opendeepwiki-backend -f
```

---

## GitHub Actions 自动构建

已配置 `.github/workflows/build-harbor.yml`

### 设置 Secrets

在 GitHub 仓库 → Settings → Secrets 中添加:
- `HARBOR_USERNAME`: Harbor 用户名
- `HARBOR_PASSWORD`: Harbor 密码

### 触发构建

- 推送到 `main`/`master` 分支: 自动构建推送 `latest` 标签
- 创建 Tag (如 `v1.0.0`): 自动构建推送对应版本标签
- 手动触发: Actions → Build and Push to Harbor → Run workflow

---

## 测试 Git 克隆修复

部署后，在 OpenDeepWiki Web 界面测试:

1. 新建仓库
2. Git URL: `https://ezone.ksyun.com/ezone/.../falcon_monitor.git`
3. 用户名: `ganlei`
4. Token: `323dd15f397f44c1b43bffb9479605891776846883402`
5. 点击「开始导入」

预期结果:
- ✅ 仓库状态变为「处理中」
- ✅ 30 秒内开始克隆代码
- ✅ 代码成功拉取并生成文档

---

## 镜像地址

| 组件 | 镜像 |
|------|------|
| Backend | `harbor.eagleye.com/open-deep-wiki/opendeepwiki-backend:latest` |
| Frontend | `harbor.eagleye.com/open-deep-wiki/opendeepwiki-web:latest` |

---

## 故障排查

### 镜像拉取失败 (ImagePullBackOff)

```bash
# 检查密钥
kubectl get secret harbor-secret -n opendeepwiki

# 重新创建密钥
kubectl delete secret harbor-secret -n opendeepwiki
kubectl create secret docker-registry harbor-secret \
  --docker-server=harbor.eagleye.com \
  --docker-username=<用户名> \
  --docker-password=<密码> \
  -n opendeepwiki
```

### Git 克隆仍然失败

检查日志:
```bash
kubectl logs -n opendeepwiki deployment/opendeepwiki-backend | grep -i "clone\|error"
```

确认修复已生效:
- 日志中应显示: `Using embedded credentials URL: https://***@***`

---

## 版本信息

- 修复版本: `1.0.0-fix$(date +%Y%m%d)`
- 修复日期: 2026-04-22
- 修复文件: `RepositoryAnalyzer.cs`
