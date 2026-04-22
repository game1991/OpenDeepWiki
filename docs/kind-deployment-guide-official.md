# Kind 部署 K8s 完全指南（WSL2 CentOS 7.9 + NAT 网络 + 域名访问）

> **版本**：Kind v0.20.0 + K8s v1.27.3 + Dashboard v2.7.0 + Ingress v1.15.1
> **环境**：WSL2 + CentOS 7.9 + Docker 24.0.9 + NAT 网络模式
> **最后更新**：2026-04-22

---

## 目录

1. [架构概览](#一架构概览)
2. [前置条件检查](#二前置条件检查)
3. [问题预防：DNS 配置](#三问题预防dns-配置关键)
4. [安装 Kind + kubectl](#四安装-kind--kubectl)
5. [预下载镜像（离线/弱网必需）](#五预下载镜像离线弱网必需)
6. [创建 Kind 集群](#六创建-kind-集群)
7. [部署 Dashboard](#七部署-dashboard)
8. [部署 Ingress Controller（关键步骤）](#八部署-ingress-controller关键步骤)
9. [部署统一网关（域名访问方案）](#九部署统一网关域名访问方案) → **详见 [k8s-gateway/](k8s-gateway/) 目录**
10. [Windows 域名配置](#十windows-域名配置)
11. [故障排查手册](#十一故障排查手册)
12. [经验总结与最佳实践](#十二经验总结与最佳实践)

---

## 一、架构概览

```
┌─────────────────────────────────────────────────────────────────┐
│                         Windows 宿主机                           │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  浏览器访问                                              │    │
│  │  ├─ http://local.gateway.com:8880/   → 统一网关页       │    │
│  │  └─ https://local.dashboard.com:8443/ → Dashboard       │    │
│  └─────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ NAT 网络
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    WSL2 CentOS 7.9                              │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  Ingress Controller (nginx)                             │    │
│  │  ├─ :8880 (HTTP)  → local.gateway.com                  │    │
│  │  └─ :8443 (HTTPS) → local.dashboard.com                │    │
│  └─────────────────────────────────────────────────────────┘    │
│              │                                    │             │
│    ┌─────────┘                                    └─────────┐   │
│    ▼                                                        ▼   │
│ ┌─────────┐                                      ┌────────────┐ │
│ │ gateway │                                      │ Dashboard  │ │
│ │ (HTTP)  │                                      │ (HTTPS)    │ │
│ └─────────┘                                      └────────────┘ │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  Kind Cluster (k8s-nat)                                  │    │
│  │  - K8s v1.27.3                                          │    │
│  │  - 单节点控制平面                                        │    │
│  └─────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
```

---

## 二、前置条件检查

### 2.1 环境要求

| 组件 | 版本 | 检查命令 |
|------|------|----------|
| Docker | 20.10+ | `docker version` |
| WSL2 | 任意 | `wsl -l -v` |
| 内存 | 建议 4GB+ | `free -h` |
| 磁盘 | 建议 20GB+ | `df -h` |

### 2.2 Docker 状态确认

```bash
# 确认 Docker 已运行
docker info
docker version

# 预期输出：Server Version: 24.0.x
```

---

## 三、问题预防：DNS 配置（关键！）

`★ 坑点 #1 ─────────────────────────────────────`
**问题**：WSL2 CentOS 7.9 默认 DNS 配置有问题，导致 `docker pull` 失败
**症状**：`Error response from daemon: Get "https://registry-1.docker.io/...": dial tcp: lookup registry-1.docker.io on [::1]:53: read udp [::1]:xxx->[::1]:53: read: connection refused`
**解决**：手动配置 DNS
`─────────────────────────────────────────────────`

### 3.1 修复 DNS（部署前必做）

```bash
# 检查当前 DNS
cat /etc/resolv.conf

# 如果包含 [::1]:53 或无法解析，立即修复
sudo tee /etc/resolv.conf > /dev/null << 'EOF'
nameserver 8.8.8.8
nameserver 114.114.114.114
EOF

# 验证修复
curl -I http://www.baidu.com
# 预期：HTTP/1.1 200 OK
```

### 3.2 持久化 DNS 配置

```bash
# 防止 WSL2 覆盖 resolv.conf
sudo tee /etc/wsl.conf > /dev/null << 'EOF'
[network]
generateResolvConf = false
EOF
```

---

## 四、安装 Kind + kubectl

### 4.1 安装 Kind v0.20.0

```bash
# 下载
[ $(uname -m) = x86_64 ] && curl -Lo ./kind https://kind.sigs.k8s.io/dl/v0.20.0/kind-linux-amd64

# 安装
chmod +x ./kind
sudo mv ./kind /usr/local/bin/kind

# 验证
kind version
# 预期：kind v0.20.0 go1.20.4 linux/amd64
```

### 4.2 安装 kubectl（版本匹配 Kind）

```bash
# Kind v0.20.0 默认使用 K8s v1.27.3
curl -LO "https://dl.k8s.io/release/v1.27.3/bin/linux/amd64/kubectl"
chmod +x kubectl
sudo mv kubectl /usr/local/bin/kubectl

# 验证
kubectl version --client
```

---

## 五、预下载镜像（离线/弱网必需）

`★ 坑点 #2 ─────────────────────────────────────`
**问题**：网络不稳定时，K8s Pod 无法拉取镜像导致 `ImagePullBackOff`
**症状**：Pod 状态一直 `ContainerCreating` 或 `ImagePullBackOff`
**解决**：预下载镜像 → `kind load` 加载到集群 → 设置 `imagePullPolicy: Never`
`─────────────────────────────────────────────────`

### 5.1 下载必需镜像

```bash
#!/bin/bash
# download-images.sh
# 预下载所有必需镜像

set -e

echo "=== 下载 Kind 核心镜像 ==="
docker pull kindest/node:v1.27.3

echo ""
echo "=== 下载 Dashboard 镜像 ==="
docker pull kubernetesui/dashboard:v2.7.0
docker pull kubernetesui/metrics-scraper:v1.0.8

echo ""
echo "=== 下载 Ingress 镜像（版本必须匹配 YAML）==="
docker pull registry.k8s.io/ingress-nginx/controller:v1.15.1
docker pull registry.k8s.io/ingress-nginx/kube-webhook-certgen:v1.6.9

echo ""
echo "✅ 镜像下载完成"
docker images | grep -E "(kindest|dashboard|ingress-nginx|kube-webhook)"
```

执行：
```bash
chmod +x download-images.sh
./download-images.sh
```

---

## 六、创建 Kind 集群

### 6.1 创建配置文件

```bash
WSL_IP=$(ip addr show eth0 | grep -oP '(?<=inet\s)\d+(\.\d+){3}')
echo "WSL2 IP: $WSL_IP"

cat > ~/kind-config.yaml << EOF
kind: Cluster
apiVersion: kind.x-k8s.io/v1alpha4
name: k8s-nat
nodes:
- role: control-plane
  image: kindest/node:v1.27.3
  labels:
    ingress-ready: "true"
  extraPortMappings:
  # HTTP 统一入口
  - containerPort: 80
    hostPort: 8880
    listenAddress: "0.0.0.0"
    protocol: TCP
  # HTTPS 统一入口
  - containerPort: 443
    hostPort: 8443
    listenAddress: "0.0.0.0"
    protocol: TCP
networking:
  apiServerAddress: "0.0.0.0"
  apiServerPort: 6443
EOF
```

### 6.2 创建集群并加载镜像

```bash
# 删除已存在的集群
kind delete cluster --name k8s-nat 2>/dev/null || true

# 创建集群
echo "=== 创建 Kind 集群 ==="
kind create cluster --config=~/kind-config.yaml --wait=5m

# 加载镜像到集群（关键步骤！）
echo "=== 加载镜像到集群 ==="
kind load docker-image kindest/node:v1.27.3 --name k8s-nat 2>/dev/null || true
kind load docker-image kubernetesui/dashboard:v2.7.0 --name k8s-nat
kind load docker-image kubernetesui/metrics-scraper:v1.0.8 --name k8s-nat
kind load docker-image registry.k8s.io/ingress-nginx/controller:v1.15.1 --name k8s-nat
kind load docker-image registry.k8s.io/ingress-nginx/kube-webhook-certgen:v1.6.9 --name k8s-nat

echo "✅ 集群创建完成"
kubectl get nodes
```

---

## 七、部署 Dashboard

### 7.1 部署并配置

```bash
# 部署 Dashboard
echo "=== 部署 Dashboard ==="
kubectl apply -f https://raw.githubusercontent.com/kubernetes/dashboard/v2.7.0/aio/deploy/recommended.yaml

# 创建管理员账号
cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: ServiceAccount
metadata:
  name: admin-user
  namespace: kubernetes-dashboard
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: admin-user
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: cluster-admin
subjects:
- kind: ServiceAccount
  name: admin-user
  namespace: kubernetes-dashboard
EOF

# 关键：修改 Dashboard 使用 admin-user（避免权限问题）
kubectl patch deployment kubernetes-dashboard -n kubernetes-dashboard \
  -p '{"spec":{"template":{"spec":{"serviceAccountName":"admin-user"}}}}'

# 等待就绪
echo "等待 Dashboard 就绪..."
kubectl wait --for=condition=ready pod -n kubernetes-dashboard \
  -l k8s-app=kubernetes-dashboard --timeout=120s

echo "✅ Dashboard 部署完成"
```

### 7.2 Dashboard 访问方式（通过 Ingress HTTPS）

Dashboard 已通过 Ingress 配置 HTTPS 域名访问，无需 port-forward。

```bash
# 验证 Dashboard Ingress 是否就绪
kubectl get ingress -n kubernetes-dashboard

# 测试访问（WSL2 内部）
curl -k -s -o /dev/null -w "Dashboard: %{http_code}\n" -H "Host: local.dashboard.com" https://localhost:8443/
# 预期：200

# Windows 浏览器访问（配置 hosts 后）
# https://local.dashboard.com:8443/
```

> Dashboard Ingress YAML 详见 [k8s-gateway/yamls/04-dashboard-ingress.yaml](k8s-gateway/yamls/04-dashboard-ingress.yaml)

---

## 八、部署 Ingress Controller（关键步骤）

`★ 坑点 #3 ─────────────────────────────────────`
**问题**：官方 YAML 使用 `@sha256:xxx` 格式，与本地镜像 tag 不匹配
**症状**：Ingress Pod 状态 `ImagePullBackOff`，事件显示 sha256 不匹配
**核心原因**：Pod 使用 `image:v1.15.1@sha256:abc...`，而本地只有 `image:v1.15.1`
**解决**：下载 YAML → 移除 sha256 → 修改 imagePullPolicy 为 Never
`─────────────────────────────────────────────────`

### 8.1 下载并修改 Ingress YAML

```bash
echo "=== 下载并修改 Ingress YAML ==="

# 下载官方 YAML
curl -s https://raw.githubusercontent.com/kubernetes/ingress-nginx/main/deploy/static/provider/kind/deploy.yaml > /tmp/ingress.yaml

# 关键修改：
# 1. 移除 @sha256:xxx 部分（只保留 tag）
# 2. 将 imagePullPolicy 改为 Never（强制使用本地镜像）
sed -i \
  -e 's|@sha256:[a-f0-9]*||g' \
  -e 's/imagePullPolicy: IfNotPresent/imagePullPolicy: Never/g' \
  /tmp/ingress.yaml

# 验证修改
echo "修改后的镜像引用："
grep "image:" /tmp/ingress.yaml | head -5
# 预期：
# image: registry.k8s.io/ingress-nginx/controller:v1.15.1
# image: registry.k8s.io/ingress-nginx/kube-webhook-certgen:v1.6.9
```

### 8.2 应用配置并验证

```bash
# 应用修改后的 YAML
echo "=== 部署 Ingress Controller ==="
kubectl apply -f /tmp/ingress.yaml

# 等待就绪
echo "等待 Ingress Controller 就绪..."
kubectl wait --namespace ingress-nginx \
  --for=condition=ready pod \
  --selector=app.kubernetes.io/component=controller \
  --timeout=120s

echo "✅ Ingress Controller 部署完成"
kubectl get pods -n ingress-nginx
```

---

## 九、部署统一网关（域名访问方案）

> **详细配置和问题排查请查看 [k8s-gateway/](k8s-gateway/) 目录**
>
> 包含完整的 YAML 文件、部署步骤、问题排查手册

### 9.1 快速部署（使用 k8s-gateway 目录中的 YAML）

```bash
# 1. 创建网关 HTML ConfigMap
kubectl apply -f k8s-gateway/yamls/05-gateway-html-configmap.yaml

# 2. 创建网关 Pod + Service
kubectl apply -f k8s-gateway/yamls/01-gateway-pod.yaml
kubectl apply -f k8s-gateway/yamls/02-gateway-service.yaml

# 3. 创建 Ingress 路由
kubectl apply -f k8s-gateway/yamls/03-gateway-ingress.yaml
kubectl apply -f k8s-gateway/yamls/04-dashboard-ingress.yaml

# 4. 等待就绪
kubectl wait --for=condition=ready pod gateway --timeout=300s
```

### 9.2 YAML 文件清单

| 文件 | 说明 |
|------|------|
| `00-kind-cluster.yaml` | Kind 集群配置（NAT 端口映射） |
| `01-gateway-pod.yaml` | 网关 Pod（挂载 ConfigMap） |
| `02-gateway-service.yaml` | 网关 Service |
| `03-gateway-ingress.yaml` | 网关 Ingress（HTTP） |
| `04-dashboard-ingress.yaml` | Dashboard Ingress（HTTPS） |
| `05-gateway-html-configmap.yaml` | 网关页面 HTML |
| `06-dashboard-admin.yaml` | Dashboard 管理员账号 |

---

## 十、Windows 域名配置

### 10.1 获取 WSL2 IP

在 **WSL2** 中执行：
```bash
ip addr show eth0 | grep -oP '(?<=inet\s)\d+(\.\d+){3}'
# 示例输出：172.17.247.87
```

### 10.2 配置 Windows hosts

以**管理员身份**编辑文件：
```
C:\Windows\System32\drivers\etc\hosts
```

添加以下内容：
```
# K8s 本地域名
172.17.247.87 local.gateway.com
172.17.247.87 local.dashboard.com
```

### 10.3 浏览器访问

| 服务 | 地址 | 协议 | 说明 |
|------|------|------|------|
| **统一网关** | http://local.gateway.com:8880/ | HTTP | 显示所有服务链接 |
| **Dashboard** | https://local.dashboard.com:8443/ | HTTPS | Token 登录 |

**Dashboard 登录步骤**：
1. 访问 `https://local.dashboard.com:8443/`
2. 浏览器提示证书不安全 → 点击"高级" → "继续前往"（接受自签名证书）
3. 选择 Token 登录
4. 在 WSL2 中执行：`kubectl create token admin-user -n kubernetes-dashboard`
5. 复制 Token 粘贴登录

---

## 十一、故障排查手册

### 11.1 DNS 解析失败

```bash
# 症状：docker pull 报 connection refused
# 解决：
sudo tee /etc/resolv.conf > /dev/null << 'EOF'
nameserver 8.8.8.8
nameserver 114.114.114.114
EOF
```

### 11.2 Pod ImagePullBackOff

```bash
# 症状：Pod 无法启动，事件显示镜像拉取失败
# 解决：

# 1. 检查镜像是否在本地
docker images | grep <镜像名>

# 2. 加载到 Kind 集群
kind load docker-image <镜像名>:<tag> --name k8s-nat

# 3. 修改 imagePullPolicy
kubectl patch deployment <name> -p '{"spec":{"template":{"spec":{"containers":[{"imagePullPolicy":"Never"}]}}}}'

# 4. 删除 Pod 重建
kubectl delete pod <pod-name> --force
```

### 11.3 Ingress 部署失败（sha256 不匹配）

```bash
# 症状：Ingress Pod ImagePullBackOff，事件显示 sha256 不匹配
# 解决：重新下载并修改 YAML

curl -s https://raw.githubusercontent.com/kubernetes/ingress-nginx/main/deploy/static/provider/kind/deploy.yaml | \
  sed -e 's|@sha256:[a-f0-9]*||g' \
      -e 's/imagePullPolicy: IfNotPresent/imagePullPolicy: Never/g' | \
  kubectl apply -f -
```

### 11.4 Dashboard Forbidden

```bash
# 症状：浏览器返回 Forbidden
# 解决：修改 Dashboard 使用 admin-user

kubectl patch deployment kubernetes-dashboard -n kubernetes-dashboard \
  -p '{"spec":{"template":{"spec":{"serviceAccountName":"admin-user"}}}}'
```

### 11.5 Dashboard 通过 Ingress 访问失败

```bash
# 症状：访问 https://local.dashboard.com:8443/ 返回 502/503/404

# 检查 Dashboard Ingress 状态
kubectl get ingress -n kubernetes-dashboard

# 检查 Dashboard Pod 是否就绪
kubectl get pods -n kubernetes-dashboard

# 检查 Ingress Controller 日志
kubectl logs -n ingress-nginx deployment/ingress-nginx-controller --tail=30 | grep dashboard

# 常见原因：
# 1. Ingress annotations 中缺少 backend-protocol: HTTPS
# 2. Dashboard Pod 未就绪
# 3. ServiceAccount 权限不足（见 11.4）
```

### 11.6 503 Service Unavailable

```bash
# 症状：Ingress 返回 503
# 原因：后端 Service 找不到或 Pod 未就绪
# 解决：

# 1. 检查 Pod 状态
kubectl get pods

# 2. 检查 Service 是否存在
kubectl get svc

# 3. 检查 Ingress 后端配置
kubectl get ingress <name> -o yaml

# 4. 查看 Ingress Controller 日志
kubectl logs -n ingress-nginx deployment/ingress-nginx-controller --tail=50
```

---

## 十二、经验总结与最佳实践

### 12.1 核心经验

| 序号 | 经验 | 说明 |
|------|------|------|
| 1 | **DNS 先行** | 部署前必须先修复 DNS，否则所有镜像拉取失败 |
| 2 | **镜像预下载** | 网络不稳定时，预下载 + `kind load` 是唯一可靠方案 |
| 3 | **移除 sha256** | Ingress 等组件必须移除 sha256 才能使用本地镜像 |
| 4 | **imagePullPolicy: Never** | 离线环境强制使用本地镜像，避免拉取尝试 |
| 5 | **ServiceAccount 权限** | Dashboard 需使用 admin-user 避免 Forbidden |
| 6 | **Ingress HTTPS 统一入口** | Dashboard 通过 Ingress HTTPS 访问，无需 port-forward |
| 7 | **Service YAML 手动编写** | 避免 `kubectl expose` 自动添加标签导致 selector 不匹配 |

### 12.2 架构设计选择

**Dashboard 访问方式演进**：

```
方案 A: kubectl proxy（已弃用）
  - 权限复杂，易 Forbidden
  - 需要后台运行进程
  ✗ 不推荐

方案 B: port-forward（已弃用）
  - 直接暴露 HTTPS 端口
  - 需要后台运行进程，断开需重启
  ✗ 不推荐

方案 C: Ingress HTTPS（当前方案 ✓）
  - 统一入口：8880(HTTP) + 8443(HTTPS)
  - 域名路由：local.dashboard.com
  - 无需后台进程
  - 生产级架构
  ✓ 推荐
```

**Dashboard Ingress HTTPS 关键配置**：

```yaml
annotations:
  nginx.ingress.kubernetes.io/backend-protocol: "HTTPS"    # 后端是 HTTPS
  nginx.ingress.kubernetes.io/ssl-redirect: "true"        # 强制 HTTPS
  nginx.ingress.kubernetes.io/proxy-ssl-verify: "off"     # 跳过自签名证书验证
```

### 12.3 一键部署脚本

保存为 `kind-full-setup.sh`：

```bash
#!/bin/bash
# Kind K8s 完整一键部署脚本
# 环境：WSL2 CentOS 7.9 + NAT 网络

set -e

WSL_IP=$(ip addr show eth0 | grep -oP '(?<=inet\s)\d+(\.\d+){3}')
CLUSTER_NAME="k8s-nat"

echo "=============================================="
echo "Kind K8s 完整部署脚本"
echo "WSL2 IP: $WSL_IP"
echo "=============================================="

# 1. 修复 DNS
echo ""
echo "[1/7] 修复 DNS..."
if ! curl -s --max-time 5 -I http://www.baidu.com > /dev/null 2>&1; then
    sudo tee /etc/resolv.conf > /dev/null << 'EOF'
nameserver 8.8.8.8
nameserver 114.114.114.114
EOF
fi
echo "✅ DNS 配置完成"

# 2. 安装 Kind/kubectl
echo ""
echo "[2/7] 检查 Kind/kubectl..."
if ! command -v kind &> /dev/null; then
    curl -Lo ./kind https://kind.sigs.k8s.io/dl/v0.20.0/kind-linux-amd64
    chmod +x ./kind
    sudo mv ./kind /usr/local/bin/kind
fi
if ! command -v kubectl &> /dev/null; then
    curl -LO "https://dl.k8s.io/release/v1.27.3/bin/linux/amd64/kubectl"
    chmod +x kubectl
    sudo mv kubectl /usr/local/bin/kubectl
fi
echo "✅ Kind/kubectl 就绪"

# 3. 预下载镜像
echo ""
echo "[3/7] 预下载镜像..."
docker pull kindest/node:v1.27.3 2>/dev/null || true
docker pull kubernetesui/dashboard:v2.7.0 2>/dev/null || true
docker pull registry.k8s.io/ingress-nginx/controller:v1.15.1 2>/dev/null || true
docker pull registry.k8s.io/ingress-nginx/kube-webhook-certgen:v1.6.9 2>/dev/null || true
echo "✅ 镜像下载完成"

# 4. 创建集群
echo ""
echo "[4/7] 创建 Kind 集群..."
cat > /tmp/kind-config.yaml << EOF
kind: Cluster
apiVersion: kind.x-k8s.io/v1alpha4
name: ${CLUSTER_NAME}
nodes:
- role: control-plane
  image: kindest/node:v1.27.3
  labels:
    ingress-ready: "true"
  extraPortMappings:
  - containerPort: 80
    hostPort: 8880
    listenAddress: "0.0.0.0"
    protocol: TCP
  - containerPort: 443
    hostPort: 8443
    listenAddress: "0.0.0.0"
    protocol: TCP
networking:
  apiServerAddress: "0.0.0.0"
EOF

kind delete cluster --name ${CLUSTER_NAME} 2>/dev/null || true
kind create cluster --config=/tmp/kind-config.yaml --wait=5m
echo "✅ 集群创建完成"

# 5. 加载镜像
echo ""
echo "[5/7] 加载镜像到集群..."
kind load docker-image kubernetesui/dashboard:v2.7.0 --name ${CLUSTER_NAME} 2>/dev/null || true
kind load docker-image registry.k8s.io/ingress-nginx/controller:v1.15.1 --name ${CLUSTER_NAME} 2>/dev/null || true
kind load docker-image registry.k8s.io/ingress-nginx/kube-webhook-certgen:v1.6.9 --name ${CLUSTER_NAME} 2>/dev/null || true
echo "✅ 镜像加载完成"

# 6. 部署 Dashboard
echo ""
echo "[6/7] 部署 Dashboard..."
kubectl apply -f https://raw.githubusercontent.com/kubernetes/dashboard/v2.7.0/aio/deploy/recommended.yaml
cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: ServiceAccount
metadata:
  name: admin-user
  namespace: kubernetes-dashboard
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: admin-user
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: cluster-admin
subjects:
- kind: ServiceAccount
  name: admin-user
  namespace: kubernetes-dashboard
EOF
kubectl patch deployment kubernetes-dashboard -n kubernetes-dashboard \
  -p '{"spec":{"template":{"spec":{"serviceAccountName":"admin-user"}}}}'
echo "✅ Dashboard 部署完成"

# 7. 部署 Ingress + 网关 + Dashboard HTTPS
echo ""
echo "[7/7] 部署 Ingress + 网关 + Dashboard HTTPS..."
curl -s https://raw.githubusercontent.com/kubernetes/ingress-nginx/main/deploy/static/provider/kind/deploy.yaml | \
  sed -e 's|@sha256:[a-f0-9]*||g' -e 's/imagePullPolicy: IfNotPresent/imagePullPolicy: Never/g' | \
  kubectl apply -f -
kubectl wait --namespace ingress-nginx --for=condition=ready pod \
  --selector=app.kubernetes.io/component=controller --timeout=120s 2>/dev/null || true

# 创建网关
kubectl run gateway --image=registry.k8s.io/ingress-nginx/controller:v1.15.1 \
  --image-pull-policy=Never --port=8080 \
  -- sh -c 'while true; do echo -e "HTTP/1.1 200 OK\r\n\r\n<h1>K8s Gateway</h1><p>WSL2 IP: '${WSL_IP}'</p><p>Dashboard: <a href=https://local.dashboard.com:8443/>https://local.dashboard.com:8443/</a></p>" | nc -l -p 8080; done'
kubectl expose pod gateway --port=80 --target-port=8080

# 创建网关 Ingress 路由（HTTP）
cat <<EOF | kubectl apply -f -
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: domain-routing
  annotations:
    nginx.ingress.kubernetes.io/rewrite-target: /
spec:
  ingressClassName: nginx
  rules:
  - host: local.gateway.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: gateway
            port:
              number: 80
EOF

# 创建 Dashboard Ingress 路由（HTTPS）
cat <<EOF | kubectl apply -f -
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: dashboard-ingress
  namespace: kubernetes-dashboard
  annotations:
    nginx.ingress.kubernetes.io/backend-protocol: "HTTPS"
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    nginx.ingress.kubernetes.io/proxy-ssl-verify: "off"
    nginx.ingress.kubernetes.io/proxy-ssl-protocols: "TLSv1.2 TLSv1.3"
spec:
  ingressClassName: nginx
  rules:
  - host: local.dashboard.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: kubernetes-dashboard
            port:
              number: 443
EOF

echo "✅ Ingress + 网关 + Dashboard HTTPS 部署完成"

# 输出信息
echo ""
echo "=============================================="
echo "🎉 部署完成！"
echo "=============================================="
echo ""
echo "📍 WSL2 IP: $WSL_IP"
echo ""
echo "📝 Windows hosts 配置："
echo "   $WSL_IP local.gateway.com"
echo "   $WSL_IP local.dashboard.com"
echo ""
echo "🌐 访问地址："
echo "   统一网关: http://local.gateway.com:8880/"
echo "   Dashboard: https://local.dashboard.com:8443/"
echo ""
echo "🔑 Dashboard Token:"
kubectl create token admin-user -n kubernetes-dashboard 2>/dev/null || echo "   Token 生成失败，稍后重试"
echo ""
echo "=============================================="
```

---

## QA：问题与解决方案汇总（本次部署实录）

> 记录 2026-04-21 实际部署过程中遇到的问题及解决方案

### Q1: DNS 解析失败，docker pull 报 connection refused

**现象**：
```
Error response from daemon: Get "https://registry-1.docker.io/...":
dial tcp: lookup registry-1.docker.io on [::1]:53: read udp [::1]:xxx->[::1]:53: read: connection refused
```

**原因**：WSL2 CentOS 7.9 默认使用 systemd-resolved，DNS 配置指向 [::1]:53 但服务未运行

**解决**：
```bash
# 手动配置公共 DNS
sudo tee /etc/resolv.conf > /dev/null << 'EOF'
nameserver 8.8.8.8
nameserver 114.114.114.114
EOF

# 验证
curl -I http://www.baidu.com
```

**预防**：将此步骤加入部署前必做检查清单

---

### Q2: Pod 状态 ImagePullBackOff，无法拉取镜像

**现象**：
```
kubectl get pods
NAME        READY   STATUS             RESTARTS   AGE
nginx-app   0/1     ImagePullBackOff   0          5m
```

**原因**：
1. 网络不稳定无法连接到 docker.io
2. Kind 节点内没有该镜像

**解决**：
```bash
# 步骤1：宿主机预下载镜像
docker pull nginx:alpine

# 步骤2：加载到 Kind 集群节点
kind load docker-image nginx:alpine --name k8s-nat

# 步骤3：设置 imagePullPolicy 为 Never（强制使用本地）
kubectl patch deployment nginx-app -p '{"spec":{"template":{"spec":{"containers":[{"imagePullPolicy":"Never"}]}}}}'

# 步骤4：删除 Pod 重建
kubectl delete pod <pod-name> --force
```

**验证**：
```bash
# 检查 Kind 节点内镜像
docker exec k8s-nat-control-plane crictl images | grep nginx
```

---

### Q3: Ingress Controller 部署失败，sha256 不匹配

**现象**：
```
Failed to pull image "registry.k8s.io/ingress-nginx/controller:v1.15.1@sha256:abc..."
rpc error: code = Unknown desc = failed to resolve reference ... sha256:abc...
```

**原因**：官方 YAML 使用 `image@sha256:xxx` 格式，而 `kind load` 加载的镜像只有 tag，K8s 认为不匹配

**解决**：修改 YAML，移除 sha256 并改 imagePullPolicy
```bash
# 下载并修改 YAML
curl -s https://raw.githubusercontent.com/kubernetes/ingress-nginx/main/deploy/static/provider/kind/deploy.yaml | \
  sed -e 's|@sha256:[a-f0-9]*||g' \
      -e 's/imagePullPolicy: IfNotPresent/imagePullPolicy: Never/g' | \
  kubectl apply -f -
```

**关键修改点**：
- `image:v1.15.1@sha256:xxx` → `image:v1.15.1`
- `imagePullPolicy: IfNotPresent` → `imagePullPolicy: Never`

---

### Q4: Dashboard 返回 Forbidden，无法登录

**现象**：浏览器访问 Dashboard 返回 `403 Forbidden`

**原因**：Dashboard 默认使用 `kubernetes-dashboard` ServiceAccount，权限不足

**解决**：修改 Deployment 使用 `admin-user` ServiceAccount
```bash
# 方法1：直接 patch
kubectl patch deployment kubernetes-dashboard -n kubernetes-dashboard \
  -p '{"spec":{"template":{"spec":{"serviceAccountName":"admin-user"}}}}'

# 方法2：edit 修改（如 patch 失败）
kubectl edit deployment kubernetes-dashboard -n kubernetes-dashboard
# 修改 spec.template.spec.serviceAccountName: admin-user
```

**验证**：
```bash
kubectl get deployment kubernetes-dashboard -n kubernetes-dashboard \
  -o jsonpath='{.spec.template.spec.serviceAccountName}'
# 预期输出：admin-user
```

---

### Q5: Dashboard 访问方式选择：proxy vs port-forward vs Ingress

**三种方式对比**：

| 方式 | 命令 | 优点 | 缺点 | 适用场景 |
|------|------|------|------|----------|
| **kubectl proxy** | `kubectl proxy --address=0.0.0.0 --port=8001` | 标准方式 | 权限复杂，易 Forbidden | 简单环境 |
| **port-forward** | `kubectl port-forward svc/dashboard 10443:443` | 直接暴露 HTTPS，可靠 | 需后台运行 | 开发调试 |
| **Ingress HTTPS** | `kind: Ingress` + annotations | 统一入口，生产标准，支持域名 | 配置较复杂 | **生产模拟（推荐）** |

**推荐方案（Ingress HTTPS）**：

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: dashboard-ingress
  namespace: kubernetes-dashboard
  annotations:
    nginx.ingress.kubernetes.io/backend-protocol: "HTTPS"      # ← 后端是 HTTPS
    nginx.ingress.kubernetes.io/ssl-redirect: "true"          # ← 强制 HTTPS
    nginx.ingress.kubernetes.io/proxy-ssl-verify: "off"       # ← 跳过自签名证书验证
    nginx.ingress.kubernetes.io/proxy-ssl-protocols: "TLSv1.2 TLSv1.3"
spec:
  ingressClassName: nginx
  rules:
  - host: local.dashboard.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: kubernetes-dashboard
            port:
              number: 443
```

**访问地址**：
```
https://local.dashboard.com:8443/
```

**Windows hosts 配置**：
```
172.17.247.87 local.dashboard.com
```

**优势**：
- 统一入口端口（8880 HTTP + 8443 HTTPS）
- 基于域名的虚拟主机路由
- 无需保持 kubectl 进程运行
- 完全模拟生产环境架构

---

### Q6: Ingress 返回 503 Service Unavailable

**现象**：
```
curl http://localhost:8880/
<html><body><h1>503 Service Unavailable</h1></body></html>
```

**原因**：
1. 后端 Pod 未就绪
2. Service 不存在或选择器不匹配
3. Ingress 配置的 ServiceName/namespace 错误

**排查步骤**：
```bash
# 1. 检查 Pod 状态
kubectl get pods -l app=<label>

# 2. 检查 Service 是否存在
kubectl get svc <service-name>

# 3. 检查 Service 端点
kubectl get endpoints <service-name>
# 预期显示 Pod IP，如没有则检查 selector

# 4. 检查 Ingress 配置
kubectl get ingress <name> -o yaml
# 确认 service.name 和 service.port 正确

# 5. 查看 Ingress Controller 日志
kubectl logs -n ingress-nginx deployment/ingress-nginx-controller --tail=50
```

**本次原因**：Ingress 创建在 `default` namespace，但 Service 在 `kubernetes-dashboard` namespace

**解决**：在 Ingress YAML 中指定正确的 namespace
```yaml
metadata:
  name: domain-routing
  namespace: kubernetes-dashboard  # ← 与 Service 同 namespace
```

---

### Q7: 网关 503 Service Unavailable（Service selector 不匹配）

**现象**：
```
curl -H "Host: local.gateway.com" http://localhost:8880/
503 Service Unavailable
```

**原因**：`kubectl expose pod` 会自动添加 `run: gateway` 标签到 Service selector，但 Pod 只有 `app: gateway` 标签，导致 Service 无法匹配到 Pod，没有 Endpoint。

```bash
# 检查：endpoints 显示 <none>
kubectl get endpoints gateway
# NAME      ENDPOINTS   AGE
# gateway   <none>      5m

# 对比：Pod labels 和 Service selector 不匹配
kubectl get pod gateway --show-labels
# LABELS: app=gateway

kubectl get svc gateway -o yaml | grep -A 2 selector
# selector:
#   app: gateway
#   run: gateway    ← 多了这个！
```

**解决**：
```bash
# 删除错误的 Service，重新创建
kubectl delete svc gateway
kubectl apply -f k8s-gateway/yamls/02-gateway-service.yaml

# 等待 Ingress Controller 同步（约 10 秒）
sleep 10

# 验证
kubectl get endpoints gateway
# 预期显示 Pod IP
```

**预防**：始终使用手动编写的 Service YAML，避免 `kubectl expose pod` 自动添加标签

---

### Q8: 如何验证域名路由是否工作？

**测试方法**：

```bash
# 1. 从 WSL2 内部测试（带 Host 头部）
curl -s -H "Host: local.gateway.com" http://localhost:8880/

# 2. 从 Windows 测试（配置 hosts 后）
curl http://local.gateway.com:8880/

# 3. 查看 Ingress 路由表
kubectl get ingress
kubectl describe ingress domain-routing
```

**预期输出**：
- 200 OK：路由正常
- 404：Ingress 规则匹配但后端无内容
- 503：后端 Service/Pod 有问题

---

### Q9: 如何在 Windows 上配置域名访问？

**步骤**：

1. **获取 WSL2 IP**
   ```bash
   # 在 WSL2 中执行
   ip addr show eth0 | grep -oP '(?<=inet\s)\d+(\.\d+){3}'
   # 示例：172.17.247.87
   ```

2. **编辑 Windows hosts 文件**（管理员权限）
   ```
   C:\Windows\System32\drivers\etc\hosts
   ```

3. **添加域名映射**
   ```
   172.17.247.87 local.gateway.com
   ```

4. **浏览器访问**
   ```
   http://local.gateway.com:8880/
   ```

---

### Q10: 集群重启后如何恢复？

**WSL2 重启后**：

```bash
# 1. DNS 可能丢失，重新配置
sudo tee /etc/resolv.conf > /dev/null << 'EOF'
nameserver 8.8.8.8
nameserver 114.114.114.114
EOF

# 2. 验证 Ingress Controller 是否运行
kubectl get pods -n ingress-nginx

# 3. 验证 Dashboard 和网关
kubectl get pods -n kubernetes-dashboard
kubectl get pods | grep gateway

# 4. 测试访问
curl -k -H "Host: local.dashboard.com" https://localhost:8443/
curl -H "Host: local.gateway.com" http://localhost:8880/
```

**注意**：由于使用 Ingress，无需再启动 port-forward

---

## 附录：常用命令速查

```bash
# 查看所有资源
kubectl get pods,svc,ingress -A

# 查看 Pod 日志
kubectl logs <pod-name> -n <namespace>

# 查看 Pod 事件（故障排查）
kubectl describe pod <pod-name>

# 进入 Pod 容器
kubectl exec -it <pod-name> -- /bin/sh

# 删除集群
kind delete cluster --name k8s-nat

# 查看集群列表
kind get clusters

# 导出集群日志
kind export logs

# 查看 Kind 节点内镜像
docker exec k8s-nat-control-plane crictl images
```

---

**文档版本**：v4.0（Ingress HTTPS 统一入口版）
**更新日期**：2026-04-22
**更新内容**：
- 移除所有 port-forward 旧方案
- Dashboard 统一通过 Ingress HTTPS 访问
- 新增 Q7：Service selector 不匹配问题
- 新增经验 #7：手动编写 Service YAML
- 统一网关配置独立到 k8s-gateway/ 目录
**作者**：基于真实部署经验整理
**适用场景**：WSL2 CentOS 7.9 + NAT 网络 + 离线/弱网环境
