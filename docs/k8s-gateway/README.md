# K8s 统一网关配置

> Kind v0.20.0 + K8s v1.27.3 + Ingress v1.15.1 + Dashboard v2.7.0
>
> WSL2 CentOS 7.9 + NAT 网络模式

---

## 架构概览

```
┌──────────────────────────────────────────────────┐
│  Windows 浏览器                                   │
│  ├─ http://local.gateway.com:8880/   → 统一网关  │
│  └─ https://local.dashboard.com:8443/ → Dashboard│
└──────────────────────────────────────────────────┘
                       │
                       │ NAT + hosts 域名解析
                       ▼
┌──────────────────────────────────────────────────┐
│  WSL2 CentOS 7.9                                 │
│                                                   │
│  Ingress Controller (nginx)                       │
│  ├─ :8880 (HTTP)  → local.gateway.com            │
│  └─ :8443 (HTTPS) → local.dashboard.com          │
│                                                   │
│  ┌──────────┐    ┌──────────────────┐            │
│  │ gateway  │    │   Dashboard      │            │
│  │ (HTTP)   │    │   (HTTPS backend)│            │
│  └──────────┘    └──────────────────┘            │
│                                                   │
│  Kind Cluster (k8s-nat)                          │
└──────────────────────────────────────────────────┘
```

---

## YAML 文件清单

| 序号 | 文件 | 说明 |
|------|------|------|
| 00 | `00-kind-cluster.yaml` | Kind 集群配置（NAT 模式端口映射） |
| 01 | `01-gateway-pod.yaml` | 网关 Pod（挂载 ConfigMap） |
| 02 | `02-gateway-service.yaml` | 网关 Service |
| 03 | `03-gateway-ingress.yaml` | 网关 Ingress 路由（HTTP） |
| 04 | `04-dashboard-ingress.yaml` | Dashboard Ingress 路由（HTTPS） |
| 05 | `05-gateway-html-configmap.yaml` | 网关页面 HTML（赛博科技风格） |
| 06 | `06-dashboard-admin.yaml` | Dashboard 管理员账号 |

---

## 快速部署

### 前置条件

```bash
# 1. 修复 DNS（部署前必做）
sudo tee /etc/resolv.conf > /dev/null << 'EOF'
nameserver 8.8.8.8
nameserver 114.114.114.114
EOF

# 2. 确认 Docker 已运行
docker info

# 3. 预下载镜像
docker pull kindest/node:v1.27.3
docker pull kubernetesui/dashboard:v2.7.0
docker pull kubernetesui/metrics-scraper:v1.0.8
docker pull registry.k8s.io/ingress-nginx/controller:v1.15.1
docker pull registry.k8s.io/ingress-nginx/kube-webhook-certgen:v1.6.9
```

### 部署步骤

```bash
# 1. 创建集群
kind create cluster --config=yamls/00-kind-cluster.yaml --wait=5m

# 2. 加载镜像到集群
kind load docker-image kubernetesui/dashboard:v2.7.0 --name k8s-nat
kind load docker-image kubernetesui/metrics-scraper:v1.0.8 --name k8s-nat
kind load docker-image registry.k8s.io/ingress-nginx/controller:v1.15.1 --name k8s-nat
kind load docker-image registry.k8s.io/ingress-nginx/kube-webhook-certgen:v1.6.9 --name k8s-nat

# 3. 部署 Dashboard
kubectl apply -f https://raw.githubusercontent.com/kubernetes/dashboard/v2.7.0/aio/deploy/recommended.yaml
kubectl apply -f yamls/06-dashboard-admin.yaml
kubectl patch deployment kubernetes-dashboard -n kubernetes-dashboard \
  -p '{"spec":{"template":{"spec":{"serviceAccountName":"admin-user"}}}}'

# 4. 部署 Ingress Controller（关键：修改 YAML）
curl -s https://raw.githubusercontent.com/kubernetes/ingress-nginx/main/deploy/static/provider/kind/deploy.yaml | \
  sed -e 's|@sha256:[a-f0-9]*||g' -e 's/imagePullPolicy: IfNotPresent/imagePullPolicy: Never/g' | \
  kubectl apply -f -
kubectl wait --namespace ingress-nginx --for=condition=ready pod \
  --selector=app.kubernetes.io/component=controller --timeout=120s

# 5. 部署统一网关
kubectl apply -f yamls/05-gateway-html-configmap.yaml
kubectl apply -f yamls/01-gateway-pod.yaml
kubectl apply -f yamls/02-gateway-service.yaml
kubectl apply -f yamls/03-gateway-ingress.yaml
kubectl apply -f yamls/04-dashboard-ingress.yaml

# 6. 等待就绪
kubectl wait --for=condition=ready pod gateway --timeout=30s
```

### Windows hosts 配置

以管理员身份编辑 `C:\Windows\System32\drivers\etc\hosts`：

```
# K8s 本地域名（替换为你的 WSL2 IP）
172.17.247.87 local.gateway.com
172.17.247.87 local.dashboard.com
```

### 访问地址

| 服务 | 地址 | 协议 |
|------|------|------|
| **统一网关** | http://local.gateway.com:8880/ | HTTP |
| **Dashboard** | https://local.dashboard.com:8443/ | HTTPS |

---

## 问题排查手册

### P1: 网关页面无法访问（503）

**原因**：Service selector 与 Pod labels 不匹配

```bash
# 检查
kubectl get endpoints gateway
# 预期显示 Pod IP，如 <none> 则 selector 不匹配

# 修复：删除并重建 Service
kubectl delete svc gateway
kubectl apply -f yamls/02-gateway-service.yaml

# 等待 Ingress Controller 同步（约 10 秒）
sleep 10
```

**坑点**：`kubectl expose pod` 会自动添加 `run: gateway` 标签到 selector，但 Pod 没有 `run` 标签，导致不匹配。必须手动创建 Service YAML。

### P2: Ingress 返回 404/503

**原因**：Ingress 的 namespace 与 Service 不在同一 namespace

```bash
# Dashboard 的 Ingress 必须在 kubernetes-dashboard namespace
# 网关的 Ingress 必须在 default namespace
kubectl get ingress -A
```

### P3: nc 发送的 HTTP 响应格式不正确

**原因**：`nc` 的 `-e` 或 `echo -e` 方式可能丢失 `\r\n`

**解决**：使用 `printf` 确保 HTTP 头部格式正确
```bash
printf "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: %s\r\nConnection: close\r\n\r\n" "$(wc -c < /var/www/html/index.html)"
cat /var/www/html/index.html
```

### P4: ConfigMap 更新后页面未刷新

**原因**：Pod 不会自动检测 ConfigMap 变更

```bash
# 重启 Pod 使 ConfigMap 生效
kubectl delete pod gateway
kubectl apply -f yamls/01-gateway-pod.yaml
```

### P5: Dashboard 通过 Ingress 访问返回 Forbidden

**原因**：Dashboard 默认 ServiceAccount 权限不足

**解决**：
```bash
# 修改 Dashboard 使用 admin-user
kubectl patch deployment kubernetes-dashboard -n kubernetes-dashboard \
  -p '{"spec":{"template":{"spec":{"serviceAccountName":"admin-user"}}}}'
```

### P6: 新增服务的域名路由

```bash
# 1. 创建 Service 和 Deployment
# 2. 创建 Ingress（指定正确的 namespace）
cat <<EOF | kubectl apply -f -
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: new-service-ingress
  annotations:
    nginx.ingress.kubernetes.io/rewrite-target: /
spec:
  ingressClassName: nginx
  rules:
  - host: local.newservice.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: new-service
            port:
              number: 80
EOF

# 3. Windows hosts 添加
# 172.17.247.87 local.newservice.com
```

---

## 清理

```bash
# 删除集群（清理所有资源）
kind delete cluster --name k8s-nat
```