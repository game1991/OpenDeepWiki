# K3s 本地开发环境部署指南

## 环境信息

| 组件 | 版本 |
|------|------|
| WSL2 | CentOS 7.9（systemd 不可用，通过 init.wsl 自启） |
| 网络 | NAT 模式（`hostAddressLoopback=true`） |
| K3s | v1.34.6+k3s1 |
| Traefik | v3.6.10（k3s 默认，hostPort 模式） |
| Dashboard | v2.7.0 |

## 统一访问入口

| 服务 | 域名 | 端口 | 协议 | 路由方式 |
|------|------|------|------|----------|
| 统一网关 | local.gateway.com | 8880 | HTTP | 标准 Ingress |
| OpenDeepWiki | local.wiki.com | 8880 | HTTP | 标准 Ingress + Middleware |
| Dashboard | local.dashboard.com | 8443 | HTTPS | IngressRoute CRD + ServersTransport |

**端口映射**：

| WSL 端口 | 来源 | 说明 |
|----------|------|------|
| 8880 | Traefik hostPort | HTTP 入口（web entrypoint → 容器 8000 → hostPort 8880） |
| 8443 | Traefik hostPort | HTTPS 入口（websecure entrypoint → 容器 8443 → hostPort 8443） |

**Windows hosts 配置**：
```
172.17.247.87 local.gateway.com local.dashboard.com local.wiki.com
```

---

## Traefik 网关架构

### 端口流转

```
Windows 浏览器
  │
  ├─ http://local.wiki.com:8880 ──→ WSL:8880 ──→ Traefik(hostPort) ──→ Ingress ──→ Wiki Frontend(:3000) / Backend(:8080/api)
  ├─ http://local.gateway.com:8880 ──→ WSL:8880 ──→ Traefik(hostPort) ──→ Ingress ──→ Gateway Nginx(:80)
  └─ https://local.dashboard.com:8443 ──→ WSL:8443 ──→ Traefik(hostPort) ──→ IngressRoute ──→ Dashboard(:443)
```

### 路由配置总览

| 资源 | 类型 | 命名空间 | 用途 |
|------|------|----------|------|
| gateway-ingress | Ingress | kube-system | Gateway 首页 |
| opendeepwiki | Ingress | opendeepwiki | Wiki 前端 + API |
| dashboard-route | IngressRoute | kubernetes-dashboard | K8s Dashboard（HTTPS 后端） |
| wiki-limits | Middleware | opendeepwiki | Wiki 大文件上传限制 |
| dashboard-transport | ServersTransport | kubernetes-dashboard | 跳过 Dashboard 自签名证书 |

### 为什么 Dashboard 用 IngressRoute 而不是标准 Ingress？

Dashboard 后端使用自签名证书，Traefik 连接后端时需要跳过证书验证。**标准 K8s Ingress 无法关联 ServersTransport**，必须使用 Traefik 的 IngressRoute CRD。

| 场景 | 推荐方式 | 原因 |
|------|---------|------|
| 简单 HTTP 路由 | 标准 Ingress | 兼容性好 |
| HTTPS 后端（自签名证书） | IngressRoute CRD | 标准 Ingress 无法关联 ServersTransport |
| 需要自定义 Middleware | IngressRoute CRD | 标准 Ingress 只能通过 annotation 引用 |

### Nginx Ingress → Traefik 配置映射

| Nginx Annotation | Traefik 等效 |
|------------------|-------------|
| `backend-protocol: HTTPS` | IngressRoute + `serversTransport` |
| `proxy-ssl-verify: off` | ServersTransport `insecureSkipVerify: true` |
| `ssl-redirect: true` | `router.entrypoints: websecure` |
| `proxy-body-size: 100m` | Middleware `buffering.maxRequestBodyBytes` |
| `proxy-read-timeout: 300` | 无直接等效，需 Middleware 或全局配置 |

---

## 一键部署

```bash
bash scripts/deploy-k3s-local.sh
```

该脚本会自动完成以下步骤：
1. 安装 K3s（`--disable=servicelb`，使用 hostPort 暴露端口）
2. 安装 Kubernetes Dashboard
3. 部署 K8s Gateway（Helm：ConfigMap + Deployment + Service + Ingress + IngressRoute + Middleware）
4. 部署 OpenDeepWiki（Helm install）

---

## 分步部署

### 1. 安装 K3s

```bash
curl -sfL https://rancher.io/install-k3s | sh -s - --cluster-init --disable=servicelb
```

> `--disable=servicelb` 禁用 k3s 自带的 svclb（我们用 hostPort 暴露端口）

### 2. 安装 Kubernetes Dashboard

```bash
kubectl apply -f https://raw.githubusercontent.com/kubernetes/dashboard/v2.7.0/aio/deploy/recommended.yaml
```

### 3. 部署 K8s Gateway（Helm）

```bash
helm install k8s-gateway charts/k8s-gateway

# 修补 Dashboard 使用管理员账号
kubectl patch deployment kubernetes-dashboard -n kubernetes-dashboard \
  -p '{"spec":{"template":{"spec":{"serviceAccountName":"admin-user"}}}}'
```

自定义配置：
```bash
helm install k8s-gateway charts/k8s-gateway \
  --set gateway.host=my-gateway.local \
  --set dashboard.host=my-dashboard.local \
  --set wiki.maxRequestBodyBytes=209715200
```

单独关闭模块：
```bash
# 不部署 Gateway 首页
helm install k8s-gateway charts/k8s-gateway --set gateway.enabled=false

# 不部署 Dashboard 路由
helm install k8s-gateway charts/k8s-gateway --set dashboard.enabled=false

# 不部署 Wiki Middleware
helm install k8s-gateway charts/k8s-gateway --set wiki.enabled=false
```

### 4. 部署 OpenDeepWiki

```bash
kubectl create namespace opendeepwiki
bash scripts/create-opendeepwiki-secret.sh
helm install opendeepwiki charts/opendeepwiki -n opendeepwiki -f config/values-k3s.yaml
```

### 5. 获取 Dashboard Token

```bash
# 长期 Token（不会过期）
kubectl get secret admin-user-token -n kubernetes-dashboard \
  -o jsonpath='{.data.token}' | base64 -d
```

---

## WSL2 自启动配置

### CentOS 7 不支持 systemd

CentOS 7 的 systemd 版本为 219，远低于 WSL2 原生 systemd 支持所需的 245+。启用 `systemd=true` 会导致 `Failed to get D-Bus connection` 等错误。

### 实际自启动机制

```
用户打开终端
  → /etc/profile source /etc/profile.d/docker.sh
  → 执行 /etc/init.wsl
  → 启动 Docker + K3s
```

关键文件：

| 文件 | 作用 |
|------|------|
| `/etc/profile.d/docker.sh` | 内容为 `/etc/init.wsl`，被 login shell 自动 source |
| `/etc/init.wsl` | 启动 Docker 和 K3s 的脚本，含 pgrep 防重复 |
| `/etc/wsl.conf` | `command=/etc/init.wsl`，WSL 启动时也触发（双保险） |

### 重启后检查

```bash
kubectl get nodes
kubectl get pods -A
```

常见问题：
- PV 节点亲和性失效（节点名变化）
- SQLite 索引损坏（需 `REINDEX`）
- Traefik hostPort 冲突（旧 Pod 残留，需 `kubectl delete pod --force`）

---

## 数据库操作

```bash
# 找到数据库
sudo find /var/lib/rancher/k3s/storage -name "opendeepwiki.db"

# 操作前先缩容后端
kubectl scale deployment opendeepwiki-backend -n opendeepwiki --replicas=0

# 修复索引
sudo sqlite3 /var/lib/rancher/k3s/storage/pvc-xxx/.../opendeepwiki.db \
  "PRAGMA integrity_check; REINDEX;"

# 恢复后端
kubectl scale deployment opendeepwiki-backend -n opendeepwiki --replicas=1
```

---

## 快速参考

```bash
# Gateway 部署/更新
helm upgrade k8s-gateway charts/k8s-gateway

# Wiki 部署/更新
helm upgrade opendeepwiki charts/opendeepwiki -n opendeepwiki -f config/values-k3s.yaml

# 健康检查
curl -s -H "Host: local.wiki.com" http://localhost:8880/api/system/version

# 查看路由
kubectl get ingress,ingressroute,middleware,serverstransport --all-namespaces

# 清理
helm uninstall k8s-gateway
helm uninstall opendeepwiki -n opendeepwiki
kubectl delete namespace opendeepwiki
/usr/local/bin/k3s-uninstall.sh
```