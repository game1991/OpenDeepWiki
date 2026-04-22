# OpenDeepWiki 构建问题总结

## 问题 1：Harbor 无法连接

```
Error: Get "https://harbor.eagleye.com/v2/": dial tcp 10.147.32.42:443: connect: connection refused
```

**原因**: Harbor 服务器在内网，需要 VPN 才能访问

**解决**:
- 连接到公司 VPN 后再执行 `docker login harbor.eagleye.com`
- 或使用 GitHub Actions 构建（服务器可以访问 Harbor）

---

## 问题 2：Docker 构建网络失败

```
Connection failed [IP: 185.125.190.83 80]
E: Unable to locate package libkrb5-3
```

**原因**: Dockerfile 中 `apt-get update` 需要访问 Ubuntu 软件源

**解决**: 使用离线版 Dockerfile（已提供 `src/fix/Dockerfile.offline`）

---

## 推荐方案：GitHub Actions 自动构建

### 步骤 1：推送代码到 GitHub

```bash
cd /home/ganlei/workspcae/openDeepWiki

# 添加 GitHub 远程仓库
git remote add github https://github.com/yourusername/OpenDeepWiki.git

# 推送代码
git push github master
```

### 步骤 2：配置 GitHub Secrets

在 GitHub 仓库 → Settings → Secrets and variables → Actions 中添加：

| Secret Name | Value |
|-------------|-------|
| `HARBOR_USERNAME` | 你的 Harbor 用户名 |
| `HARBOR_PASSWORD` | 你的 Harbor 密码 |

### 步骤 3：触发构建

三种方式：
1. 推送代码到 `main` 分支自动触发
2. 创建 Tag (如 `v1.0.1`) 触发版本构建
3. 手动触发：Actions → Build and Push to Harbor → Run workflow

---

## 备选方案：本地构建后导出

### 在有网络的环境中构建

```bash
# 在可以访问 Docker Hub 的机器上
git clone https://github.com/AIDotNet/OpenDeepWiki.git
cd OpenDeepWiki

# 应用修复
cp /path/to/RepositoryAnalyzer.cs \
   src/OpenDeepWiki/Services/Repositories/RepositoryAnalyzer.cs

# 构建
docker build -f src/OpenDeepWiki/Dockerfile -t opendeepwiki-backend:fixed .

# 导出镜像
docker save opendeepwiki-backend:fixed > opendeepwiki-backend-fixed.tar
```

### 导入到 Kind 集群

```bash
# 在目标机器上加载镜像
docker load < opendeepwiki-backend-fixed.tar

# 或者使用 Kind 加载
kind load docker-image opendeepwiki-backend:fixed --name k8s-nat

# 更新部署
kubectl set image deployment/opendeepwiki-backend \
  opendeepwiki-backend=opendeepwiki-backend:fixed \
  -n opendeepwiki
```

---

## 最快验证方案：修改现有 Deployment

如果你只是想验证修复是否有效，可以直接修改现有的 Deployment：

```bash
# 1. 将修复文件复制到 Pod
kubectl cp src/fix/RepositoryAnalyzer.cs \
  opendeepwiki/$(kubectl get pod -n opendeepwiki -l app.kubernetes.io/component=backend -o jsonpath='{.items[0].metadata.name}'):/app/Services/Repositories/

# 2. 重启 Pod
kubectl rollout restart deployment/opendeepwiki-backend -n opendeepwiki
```

**注意**: .NET 是编译型语言，此方法**不会生效**，必须重新构建镜像。

---

## 结论

| 方案 | 难度 | 时间 | 推荐度 |
|------|------|------|--------|
| GitHub Actions | 低 | 10 分钟 | ⭐⭐⭐⭐⭐ |
| 本地构建 + 导出 | 中 | 30 分钟 | ⭐⭐⭐⭐ |
| 连接 VPN 后推送 | 低 | 15 分钟 | ⭐⭐⭐⭐ |
| 直接修改 Pod | 不可行 | - | ❌ |

**最佳选择**: 配置 GitHub Actions，一劳永逸。
