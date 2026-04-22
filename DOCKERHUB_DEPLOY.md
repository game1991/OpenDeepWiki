# OpenDeepWiki Docker Hub 部署指南

## 你的镜像地址

```
docker.io/luckystar520/opendeepwiki-backend:latest
docker.io/luckystar520/opendeepwiki-web:latest
```

---

## 方案 1：GitHub Actions 自动推送（推荐）

### 步骤 1：获取 Docker Hub Token

1. 登录 https://hub.docker.com
2. 点击右上角头像 → Account Settings
3. 选择 Security → New Access Token
4. 输入 Token 名称（如 "github-actions"）
5. 权限选择：Read, Write, Delete
6. 复制生成的 Token（**只显示一次**）

### 步骤 2：推送代码到 GitHub

```bash
cd /home/ganlei/workspcae/openDeepWiki

# 添加 GitHub 远程仓库（替换 yourname 为你的 GitHub 用户名）
git remote add origin https://github.com/yourname/OpenDeepWiki.git

# 推送代码
git push -u origin master
```

### 步骤 3：配置 GitHub Secrets

1. 打开 GitHub 仓库页面
2. 点击 Settings → Secrets and variables → Actions
3. 点击 New repository secret
4. 添加 Secret：
   - **Name**: `DOCKER_HUB_TOKEN`
   - **Value**: 刚才复制的 Docker Hub Token

### 步骤 4：触发构建

有三种方式触发：

**方式 A - 推送代码自动触发**:
```bash
git add .
git commit -m "更新代码"
git push
# 自动触发构建并推送到 Docker Hub
```

**方式 B - 手动触发**:
1. 打开 GitHub 仓库 → Actions 标签
2. 选择 "Build and Push to Docker Hub"
3. 点击 "Run workflow"

### 步骤 5：查看构建结果

1. 打开 GitHub → Actions 标签
2. 查看构建进度
3. 构建成功后，镜像自动推送到 Docker Hub

---

## 部署到 Kind

### 1. 创建 Docker Hub 密钥

```bash
kubectl create secret docker-registry dockerhub-secret \
  --docker-server=docker.io \
  --docker-username=luckystar520 \
  --docker-password=<你的 Docker Hub 密码或 Token> \
  -n opendeepwiki
```

### 2. 使用 Helm 部署

```bash
helm upgrade --install opendeepwiki ./charts/opendeepwiki \
  -f config/values-kind-dockerhub.yaml \
  -n opendeepwiki
```

---

## 镜像地址

构建完成后：
- Backend: `docker.io/luckystar520/opendeepwiki-backend:latest`
- Frontend: `docker.io/luckystar520/opendeepwiki-web:latest`
