# OpenDeepWiki 源码修复说明

## 修复内容

修复了 Git 私有仓库克隆失败的问题。

### 问题
LibGit2Sharp 的 `CredentialsProvider` 回调机制在容器环境中无法与某些 Git 服务器（如 ezone）正确交互，导致克隆卡住。

### 解决方案
在克隆前将用户名/密码嵌入到 URL 中：
```
https://username:password@host/path/to/repo.git
```

## 文件说明

| 文件 | 说明 |
|------|------|
| `RepositoryAnalyzer.cs` | 修复后的完整源码文件 |
| `git-auth-fix.patch` | 补丁文件（与原版的 diff）|

## 如何使用

### 方法一：直接替换（推荐）

将 `RepositoryAnalyzer.cs` 替换 OpenDeepWiki 源码中的同名文件：

```bash
# 在 OpenDeepWiki 源码目录
cp src/fix/RepositoryAnalyzer.cs \
   src/OpenDeepWiki/Services/Repositories/RepositoryAnalyzer.cs
```

### 方法二：应用补丁

```bash
cd /path/to/OpenDeepWiki
patch -p0 < src/fix/git-auth-fix.patch
```

## 关键修改位置

文件：`src/OpenDeepWiki/Services/Repositories/RepositoryAnalyzer.cs`

方法：`CloneRepositoryAsync` (第 452-558 行)

新增代码：
```csharp
// 构建带凭据的 URL（解决 LibGit2Sharp 凭据回调问题）
string cloneUrl = workspace.GitUrl;
if (credentials is UsernamePasswordCredentials usernamePassword &&
    !string.IsNullOrEmpty(usernamePassword.Username))
{
    var uri = new Uri(workspace.GitUrl);
    var user = Uri.EscapeDataString(usernamePassword.Username);
    var pass = Uri.EscapeDataString(usernamePassword.Password ?? string.Empty);
    cloneUrl = $"{uri.Scheme}://{user}:{pass}@{uri.Host}{uri.PathAndQuery}";
}
// 使用 cloneUrl 进行克隆
GitRepository.Clone(cloneUrl, workspace.WorkingDirectory, cloneOptions);
```

## 构建推送

修复后构建并推送到 Harbor：

```bash
# 登录 Harbor
docker login harbor.eagleye.com

# 构建并推送
make harbor-build
make harbor-push

# 部署到 Kind
make harbor-deploy
```

详见项目根目录 `HARBOR_DEPLOY.md`
