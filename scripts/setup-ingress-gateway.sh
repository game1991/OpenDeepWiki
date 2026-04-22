#!/bin/bash
# setup-ingress-gateway.sh - 部署 Ingress Controller 并配置本地域名访问

set -e

NAMESPACE="ingress-nginx"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# 颜色输出
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

log_step() {
    echo -e "${BLUE}[STEP]${NC} $1"
}

# 获取 Minikube IP
get_minikube_ip() {
    minikube ip
}

# 预加载镜像
preload_images() {
    log_step "预加载 Ingress 镜像..."

    # 本地拉取镜像
    docker pull registry.cn-hangzhou.aliyuncs.com/google_containers/nginx-ingress-controller:v1.14.3 2>/dev/null || true
    docker pull registry.cn-hangzhou.aliyuncs.com/google_containers/kube-webhook-certgen:v1.6.7 2>/dev/null || true

    # 加载到 Minikube
    minikube image load registry.cn-hangzhou.aliyuncs.com/google_containers/nginx-ingress-controller:v1.14.3 2>/dev/null || true
    minikube image load registry.cn-hangzhou.aliyuncs.com/google_containers/kube-webhook-certgen:v1.6.7 2>/dev/null || true

    log_info "镜像加载完成"
}

# 部署 Ingress Controller
deploy_ingress_controller() {
    log_step "部署 Ingress Controller..."

    # 使用官方 YAML 但修改镜像地址
    cat > /tmp/ingress-nginx-deploy.yaml << 'EOF'
apiVersion: v1
kind: Namespace
metadata:
  name: ingress-nginx
---
apiVersion: v1
kind: ServiceAccount
metadata:
  labels:
    app.kubernetes.io/component: admission-webhook
    app.kubernetes.io/instance: ingress-nginx
    app.kubernetes.io/name: ingress-nginx
  name: ingress-nginx-admission
  namespace: ingress-nginx
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  labels:
    app.kubernetes.io/instance: ingress-nginx
    app.kubernetes.io/name: ingress-nginx
  name: ingress-nginx
rules:
- apiGroups:
  - ""
  resources:
  - configmaps
  - endpoints
  - nodes
  - pods
  - secrets
  - services
  verbs:
  - list
  - watch
- apiGroups:
  - ""
  resources:
  - nodes
  verbs:
  - get
- apiGroups:
  - ""
  resources:
  - services
  verbs:
  - get
  - list
  - watch
- apiGroups:
  - networking.k8s.io
  resources:
  - ingresses
  verbs:
  - get
  - list
  - watch
- apiGroups:
  - ""
  resources:
  - events
  verbs:
  - create
  - patch
- apiGroups:
  - networking.k8s.io
  resources:
  - ingresses/status
  verbs:
  - update
- apiGroups:
  - networking.k8s.io
  resources:
  - ingressclasses
  verbs:
  - get
  - list
  - watch
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  labels:
    app.kubernetes.io/instance: ingress-nginx
    app.kubernetes.io/name: ingress-nginx
  name: ingress-nginx
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: ingress-nginx
subjects:
- kind: ServiceAccount
  name: ingress-nginx
  namespace: ingress-nginx
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  labels:
    app.kubernetes.io/component: admission-webhook
    app.kubernetes.io/instance: ingress-nginx
    app.kubernetes.io/name: ingress-nginx
  name: ingress-nginx-admission
rules:
- apiGroups:
  - admissionregistration.k8s.io
  resources:
  - validatingwebhookconfigurations
  verbs:
  - get
  - update
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  labels:
    app.kubernetes.io/component: admission-webhook
    app.kubernetes.io/instance: ingress-nginx
    app.kubernetes.io/name: ingress-nginx
  name: ingress-nginx-admission
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: ingress-nginx-admission
subjects:
- kind: ServiceAccount
  name: ingress-nginx-admission
  namespace: ingress-nginx
---
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  labels:
    app.kubernetes.io/component: controller
    app.kubernetes.io/instance: ingress-nginx
    app.kubernetes.io/name: ingress-nginx
  name: ingress-nginx
  namespace: ingress-nginx
rules:
- apiGroups:
  - ""
  resources:
  - namespaces
  verbs:
  - get
- apiGroups:
  - ""
  resources:
  - configmaps
  - pods
  - secrets
  - endpoints
  verbs:
  - get
  - list
  - watch
- apiGroups:
  - ""
  resources:
  - services
  verbs:
  - get
  - list
  - watch
- apiGroups:
  - networking.k8s.io
  resources:
  - ingresses
  verbs:
  - get
  - list
  - watch
- apiGroups:
  - networking.k8s.io
  resources:
  - ingresses/status
  verbs:
  - update
- apiGroups:
  - networking.k8s.io
  resources:
  - ingressclasses
  verbs:
  - get
  - list
  - watch
- apiGroups:
  - ""
  resources:
  - configmaps
  resourceNames:
  - ingress-controller-leader
  verbs:
  - get
  - create
  - update
- apiGroups:
  - coordination.k8s.io
  resources:
  - leases
  resourceNames:
  - ingress-controller-leader
  verbs:
  - get
  - create
  - update
- apiGroups:
  - ""
  resources:
  - events
  verbs:
  - create
  - patch
- apiGroups:
  - discovery.k8s.io
  resources:
  - endpointslices
  verbs:
  - list
  - watch
  - get
---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  labels:
    app.kubernetes.io/component: controller
    app.kubernetes.io/instance: ingress-nginx
    app.kubernetes.io/name: ingress-nginx
  name: ingress-nginx
  namespace: ingress-nginx
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: Role
  name: ingress-nginx
subjects:
- kind: ServiceAccount
  name: ingress-nginx
  namespace: ingress-nginx
---
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  labels:
    app.kubernetes.io/component: admission-webhook
    app.kubernetes.io/instance: ingress-nginx
    app.kubernetes.io/name: ingress-nginx
  name: ingress-nginx-admission
  namespace: ingress-nginx
rules:
- apiGroups:
  - ""
  resources:
  - secrets
  verbs:
  - get
  - create
---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  labels:
    app.kubernetes.io/component: admission-webhook
    app.kubernetes.io/instance: ingress-nginx
    app.kubernetes.io/name: ingress-nginx
  name: ingress-nginx-admission
  namespace: ingress-nginx
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: Role
  name: ingress-nginx-admission
subjects:
- kind: ServiceAccount
  name: ingress-nginx-admission
  namespace: ingress-nginx
---
apiVersion: v1
data:
  allow-snippet-annotations: "true"
kind: ConfigMap
metadata:
  labels:
    app.kubernetes.io/component: controller
    app.kubernetes.io/instance: ingress-nginx
    app.kubernetes.io/name: ingress-nginx
  name: ingress-nginx-controller
  namespace: ingress-nginx
---
apiVersion: v1
kind: Service
metadata:
  labels:
    app.kubernetes.io/component: controller
    app.kubernetes.io/instance: ingress-nginx
    app.kubernetes.io/name: ingress-nginx
  name: ingress-nginx-controller
  namespace: ingress-nginx
spec:
  externalTrafficPolicy: Local
  ipFamilies:
  - IPv4
  ipFamilyPolicy: SingleStack
  ports:
  - appProtocol: http
    name: http
    port: 80
    protocol: TCP
    targetPort: http
  - appProtocol: https
    name: https
    port: 443
    protocol: TCP
    targetPort: https
  selector:
    app.kubernetes.io/component: controller
    app.kubernetes.io/instance: ingress-nginx
    app.kubernetes.io/name: ingress-nginx
  type: NodePort
EOF

    # 部署基础资源
    kubectl apply -f /tmp/ingress-nginx-deploy.yaml

    # 创建 Deployment（使用阿里云镜像）
    cat > /tmp/ingress-controller.yaml << 'EOF'
apiVersion: apps/v1
kind: Deployment
metadata:
  labels:
    app.kubernetes.io/component: controller
    app.kubernetes.io/instance: ingress-nginx
    app.kubernetes.io/name: ingress-nginx
  name: ingress-nginx-controller
  namespace: ingress-nginx
spec:
  minReadySeconds: 0
  revisionHistoryLimit: 10
  selector:
    matchLabels:
      app.kubernetes.io/component: controller
      app.kubernetes.io/instance: ingress-nginx
      app.kubernetes.io/name: ingress-nginx
  template:
    metadata:
      labels:
        app.kubernetes.io/component: controller
        app.kubernetes.io/instance: ingress-nginx
        app.kubernetes.io/name: ingress-nginx
    spec:
      containers:
      - args:
        - /nginx-ingress-controller
        - --election-id=ingress-controller-leader
        - --controller-class=k8s.io/ingress-nginx
        - --ingress-class=nginx
        - --configmap=$(POD_NAMESPACE)/ingress-nginx-controller
        - --validating-webhook=:8443
        - --validating-webhook-certificate=/usr/local/certificates/cert
        - --validating-webhook-key=/usr/local/certificates/key
        env:
        - name: POD_NAME
          valueFrom:
            fieldRef:
              fieldPath: metadata.name
        - name: POD_NAMESPACE
          valueFrom:
            fieldRef:
              fieldPath: metadata.namespace
        - name: LD_PRELOAD
          value: /usr/local/lib/libmimalloc.so
        image: registry.cn-hangzhou.aliyuncs.com/google_containers/nginx-ingress-controller:v1.14.3
        imagePullPolicy: IfNotPresent
        lifecycle:
          preStop:
            exec:
              command:
              - /wait-shutdown
        livenessProbe:
          failureThreshold: 5
          httpGet:
            path: /healthz
            port: 10254
            scheme: HTTP
          initialDelaySeconds: 10
          periodSeconds: 10
          successThreshold: 1
          timeoutSeconds: 1
        name: controller
        ports:
        - containerPort: 80
          name: http
          protocol: TCP
        - containerPort: 443
          name: https
          protocol: TCP
        - containerPort: 8443
          name: webhook
          protocol: TCP
        readinessProbe:
          failureThreshold: 3
          httpGet:
            path: /readyz
            port: 10254
            scheme: HTTP
          initialDelaySeconds: 10
          periodSeconds: 10
          successThreshold: 1
          timeoutSeconds: 1
        resources:
          requests:
            cpu: 100m
            memory: 90Mi
        securityContext:
          allowPrivilegeEscalation: true
          readOnlyRootFilesystem: false
          runAsNonRoot: true
          runAsUser: 101
          seccompProfile:
            type: RuntimeDefault
        volumeMounts:
        - mountPath: /usr/local/certificates/
          name: webhook-cert
          readOnly: true
      dnsPolicy: ClusterFirst
      nodeSelector:
        kubernetes.io/os: linux
      serviceAccountName: ingress-nginx
      terminationGracePeriodSeconds: 300
      volumes:
      - name: webhook-cert
        secret:
          secretName: ingress-nginx-admission
EOF

    # 先创建空的 secret 避免挂载失败
    kubectl create secret generic ingress-nginx-admission \
        --namespace ingress-nginx \
        --from-literal=cert="" \
        --from-literal=key="" 2>/dev/null || true

    # 部署 Controller
    kubectl apply -f /tmp/ingress-controller.yaml

    log_info "Ingress Controller 部署完成，等待就绪..."
}

# 等待 Ingress Controller 就绪
wait_for_ingress() {
    log_step "等待 Ingress Controller 就绪..."

    kubectl wait --for=condition=ready pod \
        -l app.kubernetes.io/component=controller \
        -n ingress-nginx \
        --timeout=120s || {
        log_error "Ingress Controller 启动超时"
        log_info "查看日志: kubectl logs -n ingress-nginx -l app.kubernetes.io/component=controller"
        exit 1
    }

    log_info "Ingress Controller 已就绪"
}

# 创建 Dashboard Ingress
create_dashboard_ingress() {
    log_step "创建 Dashboard Ingress..."

    cat > /tmp/dashboard-ingress.yaml << 'EOF'
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: kubernetes-dashboard
  namespace: kubernetes-dashboard
  annotations:
    nginx.ingress.kubernetes.io/backend-protocol: "HTTPS"
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
spec:
  ingressClassName: nginx
  rules:
  - host: local.k8s-dashboard.com
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

    kubectl apply -f /tmp/dashboard-ingress.yaml
    log_info "Dashboard Ingress 创建完成"
}

# 生成 hosts 配置
generate_hosts_config() {
    log_step "生成 hosts 配置..."

    MINIKUBE_IP=$(get_minikube_ip)

    cat > /tmp/k8s-hosts.txt << EOF
# ==========================================
# K8s 本地域名 hosts 配置
# 请将这些内容添加到 Windows 的 hosts 文件中
# 路径: C:\Windows\System32\drivers\etc\hosts
# ==========================================

${MINIKUBE_IP} local.k8s-dashboard.com

# 预留的其他服务域名（后续可添加）
# ${MINIKUBE_IP} local.opendeepwiki.com
# ${MINIKUBE_IP} local.grafana.com
# ${MINIKUBE_IP} local.prometheus.com

EOF

    log_info "hosts 配置已生成: /tmp/k8s-hosts.txt"

    echo ""
    echo "=========================================="
    echo "请手动添加以下内容到 Windows hosts 文件:"
    echo "路径: C:\Windows\System32\drivers\etc\hosts"
    echo "=========================================="
    cat /tmp/k8s-hosts.txt
    echo "=========================================="
}

# 显示访问信息
show_access_info() {
    MINIKUBE_IP=$(get_minikube_ip)

    echo ""
    echo "=========================================="
    echo "      Ingress 网关配置完成"
    echo "=========================================="
    echo ""
    log_info "Minikube IP: $MINIKUBE_IP"
    echo ""
    log_info "访问地址:"
    echo "  Dashboard: https://local.k8s-dashboard.com"
    echo ""
    log_info "配置步骤:"
    echo "  1. 以管理员身份编辑 Windows hosts 文件"
    echo "     C:\Windows\System32\drivers\etc\hosts"
    echo ""
    echo "  2. 添加以下行:"
    echo "     ${MINIKUBE_IP} local.k8s-dashboard.com"
    echo ""
    echo "  3. 保存后，在 Windows 浏览器访问:"
    echo "     https://local.k8s-dashboard.com"
    echo ""
    log_info "查看所有 Ingress:"
    echo "  kubectl get ingress -A"
    echo ""
    log_info "删除 Ingress:"
    echo "  kubectl delete ingress kubernetes-dashboard -n kubernetes-dashboard"
    echo "=========================================="
}

# 主函数
main() {
    log_info "开始部署 Ingress 网关..."

    preload_images
    deploy_ingress_controller
    wait_for_ingress
    create_dashboard_ingress
    generate_hosts_config
    show_access_info

    log_info "完成！"
}

# 运行
main "$@"
