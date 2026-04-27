# Kind → K3s 迁移踩坑记录

> 2026-04-25 总结，涵盖 Kind 部署、迁移 K3s、Traefik 网关配置全过程的问题与解决方案

---

## 一、Kind 阶段踩坑

### 1.1 Docker 网络隔离导致 Git Clone 失败

**现象**：OpenDeepWiki 添加私有仓库后，Pod 日志报错 `failed to connect to <YOUR_WSL_IP>: Connection refused`

**根因**：Kind 集群运行在 Docker 容器内，Pod 网络经过三层隔离（Windows → WSL2 → Docker → Pod），Pod 无法访问 WSL2 宿主机的 Git 服务。

**尝试过的方案**：
- ❌ `tinyproxy` 代理 — Pod 内需额外配置 `HTTP_PROXY`/`HTTPS_PROXY`
- ❌ `hostNetwork: true` — 端口冲突风险大
- ❌ 自定义 Docker 网络 — Kind 不支持
- ✅ **最终方案**：迁移到 K3s，消除 Docker 网络隔离层

### 1.2 Kind extraPortMappings 端口映射

**现象**：Windows 浏览器无法通过 `http://local.wiki.com:8880` 访问服务

**根因**：WSL2 NAT 模式下，Windows 无法直接访问 WSL 内部端口。Kind 的 `extraPortMappings` 将容器端口映射到 Docker 主机，但 WSL2 到 Windows 还需要一层端口转发。

**解决方案**：
1. Windows hosts 配置：`<YOUR_WSL_IP> local.wiki.com local.gateway.com local.dashboard.com`
2. `.wslconfig` 中配置 `hostAddressLoopback=true`，使 Windows 可直接访问 WSL 端口
3. 不需要 `netsh interface portproxy`，`hostAddressLoopback` 已足够

### 1.3 ingress-nginx 的 Dashboard HTTPS 后端配置

**现象**：Dashboard 使用自签名证书，Ingress 需要跳过证书验证

**解决方案**（nginx 方式）：
```yaml
annotations:
  nginx.ingress.kubernetes.io/backend-protocol: HTTPS
  nginx.ingress.kubernetes.io/proxy-ssl-verify: "off"
  nginx.ingress.kubernetes.io/ssl-redirect: "true"
```

---

## 二、K3s 迁移阶段踩坑

### 2.1 K3s 启动后出现双节点

**现象**：`kubectl get nodes` 显示两个节点，但实际只有一个 WSL 实例

**根因**：K3s 之前运行过，节点名 `k3s-wsl` 残留在 etcd 中。重启 K3s 后新节点以 hostname `wsl2-centos7-ganlei` 注册。

**解决方案**：
```bash
kubectl delete node k3s-wsl  # 删除旧节点
```

### 2.2 PV 节点亲和性失效

**现象**：后端 Pod 一直 Pending，报错 `node(s) didn't match PersistentVolume's node affinity`

**根因**：K3s 的 `local-path` 存储类创建的 PV 绑定了旧节点名 `k3s-wsl`，删除旧节点后 PV 无法调度到新节点。

**解决方案**：
1. 修改 PV 回收策略为 Retain（防止数据丢失）
2. 删除 PVC 和旧 PV
3. 创建新 PV，将 `nodeAffinity` 指向新节点名
4. 创建新 PVC 绑定新 PV

```bash
# 修改回收策略
kubectl patch pv pvc-xxx -p '{"spec":{"persistentVolumeReclaimPolicy":"Retain"}}'

# 删除旧资源
kubectl delete pvc opendeepwiki-backend-data -n opendeepwiki
kubectl delete pv pvc-xxx

# 创建新 PV（指向新节点 + 旧数据目录）
cat << 'EOF' | kubectl apply -f -
apiVersion: v1
kind: PersistentVolume
metadata:
  name: opendeepwiki-backend-data-pv
spec:
  capacity:
    storage: 10Gi
  accessModes:
    - ReadWriteOnce
  persistentVolumeReclaimPolicy: Retain
  storageClassName: local-path
  local:
    path: /var/lib/rancher/k3s/storage/pvc-xxx/opendeepwiki_opendeepwiki-backend-data
  nodeAffinity:
    required:
      nodeSelectorTerms:
      - matchExpressions:
        - key: kubernetes.io/hostname
          operator: In
          values:
          - wsl2-centos7-ganlei  # 新节点名
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: opendeepwiki-backend-data
  namespace: opendeepwiki
spec:
  accessModes:
    - ReadWriteOnce
  storageClassName: local-path
  volumeName: opendeepwiki-backend-data-pv
  resources:
    requests:
      storage: 10Gi
EOF
```

**⚠️ 教训**：`local-path` 类型的 PV 与节点强绑定，节点名变化时必须重建 PV。建议回收策略设为 `Retain`。

### 2.3 SQLite 数据库索引损坏

**现象**：私有仓库列表 API 只返回部分仓库，但数据库中数据完整

**排查过程**：
```sql
-- 数据存在
SELECT RepoName FROM Repositories WHERE IsDeleted=0;
-- falcon_monitor
-- marmot-sh

-- 但条件查询丢失数据
SELECT RepoName FROM Repositories WHERE OwnerUserId='xxx' AND IsDepartmentOwned=0 AND IsDeleted=0;
-- 只返回 falcon_monitor（丢失 marmot-sh）
```

**根因**：SQLite 索引损坏
```
PRAGMA integrity_check;
-- rowid 2 missing from index sqlite_autoindex_Repositories_1
```

**解决方案**：
```sql
REINDEX;
PRAGMA integrity_check;  -- 返回 ok
```

**⚠️ 教训**：WSL2 异常关机或 K3s 重启可能导致 SQLite WAL 模式数据不一致。遇到"数据存在但查不到"的诡异现象时，首先检查索引完整性。

### 2.4 Pod 内无法解析 localhost

**现象**：`kubectl port-forward` 报错 `failed to connect to localhost: no such host`

**根因**：部分 CNI 网络命名空间中 Pod 的 `/etc/hosts` 不包含 `localhost` 解析

**解决方案**：避免使用 `localhost`，改用 Pod IP 或 ClusterIP 直接访问。

### 2.5 K3s 服务未自启动

**现象**：WSL2 重启后，K3s 服务没有自动启动

**根因**：K3s 的 systemd 服务默认未启用（`disabled`），且 `ExecStart` 配置缺失

**解决方案**：
```bash
# 手动启动
sudo /usr/local/bin/k3s server &

# 或检查 systemd 配置
sudo systemctl enable k3s
sudo systemctl start k3s
```

---

## 三、Traefik 网关配置踩坑

### 3.1 Traefik hostPort 与 NodePort 共存

**现象**：Traefik Deployment 配置了 `hostPort: 8880`，同时 Service 改为 NodePort `30080`

**实际情况**：两个端口都能工作！
- `8880`：Traefik Pod 的 hostPort，直接映射到 WSL 主机
- `30080`：K8s Service NodePort，通过 kube-proxy iptables 规则转发

**⚠️ 注意**：hostPort 端口被 Pod 占用时，新 Pod 无法调度到同一节点（端口冲突）。建议优先使用 NodePort 或 LoadBalancer。

### 3.2 Dashboard 必须用 IngressRoute CRD，不能用标准 Ingress

**现象**：Dashboard Ingress 返回 `Internal Server Error`，日志显示 `TLS handshake error: remote error: tls: bad certificate`

**根因**：Dashboard 后端使用自签名证书。标准 K8s Ingress 无法关联 Traefik 的 `ServersTransport`（跳过证书验证的配置），必须使用 Traefik 的 CRD `IngressRoute`。

**错误方案**（标准 Ingress + annotation，**不可行**）：
```yaml
# ❌ 这样写 Traefik 不会读取 ServersTransport
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  annotations:
    traefik.ingress.kubernetes.io/service.serversscheme: https
    traefik.ingress.kubernetes.io/service.serversstransport: insecure-transport
spec:
  rules:
  - host: local.dashboard.com
    ...
```

**正确方案**（IngressRoute CRD + ServersTransport）：
```yaml
# ✅ ServersTransport 跳过证书验证
apiVersion: traefik.io/v1alpha1
kind: ServersTransport
metadata:
  name: dashboard-transport
  namespace: kubernetes-dashboard
spec:
  insecureSkipVerify: true
---
# ✅ IngressRoute 关联 ServersTransport
apiVersion: traefik.io/v1alpha1
kind: IngressRoute
metadata:
  name: dashboard-route
  namespace: kubernetes-dashboard
spec:
  entryPoints:
    - websecure
  routes:
    - match: Host(`local.dashboard.com`)
      kind: Rule
      services:
        - name: kubernetes-dashboard
          port: 443
          serversTransport: dashboard-transport  # 关键：关联 Transport
  tls: {}
```

**经验总结**：

| 场景 | 推荐方式 | 原因 |
|------|---------|------|
| 简单 HTTP 路由 | 标准 K8s Ingress | 简单、兼容性好 |
| HTTPS 后端（自签名证书） | IngressRoute CRD | 标准 Ingress 无法关联 ServersTransport |
| 需要自定义中间件 | IngressRoute CRD | 支持 Middleware、RateLimit 等高级功能 |
| 需要精确 EntryPoints 控制 | IngressRoute CRD | 可指定 web/websecure 等 |

### 3.3 Nginx Ingress → Traefik 配置映射

| Nginx Ingress Annotation | Traefik 等效方式 |
|--------------------------|-----------------|
| `nginx.ingress.kubernetes.io/backend-protocol: HTTPS` | IngressRoute + `serversTransport` |
| `nginx.ingress.kubernetes.io/ssl-redirect: true` | `router.entrypoints: websecure` |
| `nginx.ingress.kubernetes.io/proxy-ssl-verify: off` | ServersTransport `insecureSkipVerify: true` |
| `nginx.ingress.kubernetes.io/rewrite-target: /` | Middleware `ReplacePath` |

---

## 四、WSL2 网络踩坑

### 4.1 hostAddressLoopback 解决端口转发问题

**现象**：`netsh interface portproxy show all` 为空，但 Windows 浏览器能访问 WSL 端口

**根因**：`.wslconfig` 配置了 `hostAddressLoopback=true`，WSL2 自动将端口暴露给 Windows。

**关键配置**（`C:\Users\KC\.wslconfig`）：
```ini
[wsl2]
networkingMode=NAT
hostAddressLoopback=true    # 关键！Windows 可直接访问 WSL 所有端口
dnsTunneling=true
firewall=false

[experimental]
sparseVhd=true
autoMemoryReclaim=dropCache
```

**注意**：`#ignoredPorts=6060,8880,8443` 被注释掉了，所以所有端口都可通过 `hostAddressLoopback` 访问。

### 4.2 WSL 内 ss/netstat 看不到端口监听

**现象**：`sudo ss -tlnp` 在 WSL 中看不到 8880/30080 端口监听，但 Windows `netstat` 显示有 ESTABLISHED 连接

**根因**：WSL2 的 `hostAddressLoopback` 机制在内核层面工作，不通过用户空间监听。`ss`/`netstat` 只能看到用户空间进程的监听。

**验证方法**：从 Windows 侧用 `netstat -an | findstr ":8880"` 确认连通性。

---

## 五、OpenDeepWiki 应用踩坑

### 5.1 英文文档生成失败（API 403）

**现象**：falcon_monitor 仓库中文文档正常，但英文文档全部为空

**根因**：AI API（kimi-k2.5）返回 403 Forbidden：
```
Error response: {"error":{"type":"Forbidden","message":"unauthorized consumer"}}
AI agent failed. Operation: IncrementalUpdate, Model: kimi-k2.5, Language: en
```

**解决方案**：检查 AI API Key 权限，或重新生成文档。

### 5.2 数据库中目录节点没有 DocFileId

**现象**：部分中文文档点击后显示 "Not Found"

**根因**：目录节点（如 `1-overview`）本身没有 `DocFileId`，只有叶子节点（如 `1-overview.architecture`）才有文档内容。这是正常设计，不是 bug。

**验证 SQL**：
```sql
SELECT
  dc.Path,
  CASE WHEN dc.DocFileId IS NOT NULL THEN '有内容' ELSE '目录节点' END as Type
FROM DocCatalogs dc
JOIN BranchLanguages bl ON bl.Id = dc.BranchLanguageId
WHERE bl.LanguageCode = 'zh'
ORDER BY dc.Path;
```

---

## 六、K3s 运维速查

### 重启后检查清单

```bash
# 1. K3s 是否运行
sudo systemctl status k3s || sudo /usr/local/bin/k3s server &

# 2. 节点状态
kubectl get nodes

# 3. 所有 Pod 状态
kubectl get pods -A

# 4. 数据库完整性（如果发现查询异常）
sudo sqlite3 /var/lib/rancher/k3s/storage/pvc-xxx/opendeepwiki.db \
  "PRAGMA integrity_check; REINDEX;"

# 5. 服务连通性
curl -s -H "Host: local.wiki.com" http://localhost:8880/api/system/version
```

### 关键文件路径

| 文件 | 路径 |
|------|------|
| K3s 二进制 | `/usr/local/bin/k3s` |
| K3s 数据 | `/var/lib/rancher/k3s/` |
| PVC 存储 | `/var/lib/rancher/k3s/storage/` |
| OpenDeepWiki DB | `/var/lib/rancher/k3s/storage/pvc-xxx/.../opendeepwiki.db` |
| WSL 配置 | `C:\Users\KC\.wslconfig` |
| Windows hosts | `C:\Windows\System32\drivers\etc\hosts` |

### Dashboard 长期 Token

```bash
# 创建长期 Secret（只需一次，Helm chart 已包含此资源）
# 如未通过 Helm 部署 Gateway，手动创建：
kubectl apply -f charts/k8s-gateway/templates/dashboard-admin.yaml

# 获取 Token
kubectl get secret admin-user-token -n kubernetes-dashboard \
  -o jsonpath='{.data.token}' | base64 -d
```

### 访问入口

| 服务 | URL |
|------|-----|
| 网关首页 | http://local.gateway.com:8880 |
| K8s Dashboard | https://local.dashboard.com:8443 |
| OpenDeepWiki | http://local.wiki.com:8880 |
| OpenDeepWiki API | http://local.wiki.com:8880/api/system/version |