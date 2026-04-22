#!/bin/bash
# OpenDeepWiki Harbor 构建推送脚本

set -e

HARBOR_URL="harbor.eagleye.com"
PROJECT="open-deep-wiki"
BACKEND_IMAGE="opendeepwiki-backend"
FRONTEND_IMAGE="opendeepwiki-web"
VERSION="1.0.0-fix$(date +%Y%m%d)"

echo "========================================"
echo "OpenDeepWiki Harbor 构建推送"
echo "========================================"
echo "Harbor: ${HARBOR_URL}/${PROJECT}"
echo "版本: ${VERSION}"
echo ""

# 检查 Docker
docker info > /dev/null 2>&1 || {
    echo "错误: Docker 未运行"
    exit 1
}

# 登录 Harbor
echo "1. 登录 Harbor..."
echo "提示: 输入 Harbor 用户名和密码"
docker login ${HARBOR_URL} || {
    echo "错误: Harbor 登录失败"
    exit 1
}

# 构建后端镜像（带 Git 修复）
echo ""
echo "2. 构建后端镜像..."
docker build \
    -f src/OpenDeepWiki/Dockerfile \
    -t ${HARBOR_URL}/${PROJECT}/${BACKEND_IMAGE}:${VERSION} \
    -t ${HARBOR_URL}/${PROJECT}/${BACKEND_IMAGE}:latest \
    .

# 构建前端镜像
echo ""
echo "3. 构建前端镜像..."
docker build \
    -f web/Dockerfile \
    -t ${HARBOR_URL}/${PROJECT}/${FRONTEND_IMAGE}:${VERSION} \
    -t ${HARBOR_URL}/${PROJECT}/${FRONTEND_IMAGE}:latest \
    ./web

# 推送镜像
echo ""
echo "4. 推送镜像到 Harbor..."
docker push ${HARBOR_URL}/${PROJECT}/${BACKEND_IMAGE}:${VERSION}
docker push ${HARBOR_URL}/${PROJECT}/${BACKEND_IMAGE}:latest
docker push ${HARBOR_URL}/${PROJECT}/${FRONTEND_IMAGE}:${VERSION}
docker push ${HARBOR_URL}/${PROJECT}/${FRONTEND_IMAGE}:latest

# 生成部署 YAML
echo ""
echo "5. 生成部署配置..."
cat > harbor-deployment.yaml << EOF
apiVersion: apps/v1
kind: Deployment
metadata:
  name: opendeepwiki-backend
  namespace: opendeepwiki
spec:
  replicas: 1
  selector:
    matchLabels:
      app: opendeepwiki-backend
  template:
    metadata:
      labels:
        app: opendeepwiki-backend
    spec:
      containers:
      - name: backend
        image: ${HARBOR_URL}/${PROJECT}/${BACKEND_IMAGE}:${VERSION}
        imagePullPolicy: Always
        ports:
        - containerPort: 8080
        env:
        - name: ENDPOINT
          value: "https://kspmas.ksyun.com/v1"
        - name: CHAT_REQUEST_MODEL_ID
          value: "kimi-k2.5"
        volumeMounts:
        - name: data
          mountPath: /data
      volumes:
      - name: data
        persistentVolumeClaim:
          claimName: opendeepwiki-backend-data
      imagePullSecrets:
      - name: harbor-secret
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: opendeepwiki-frontend
  namespace: opendeepwiki
spec:
  replicas: 1
  selector:
    matchLabels:
      app: opendeepwiki-frontend
  template:
    metadata:
      labels:
        app: opendeepwiki-frontend
    spec:
      containers:
      - name: frontend
        image: ${HARBOR_URL}/${PROJECT}/${FRONTEND_IMAGE}:${VERSION}
        imagePullPolicy: Always
        ports:
        - containerPort: 3000
        env:
        - name: API_PROXY_URL
          value: "http://opendeepwiki-backend:8080"
      imagePullSecrets:
      - name: harbor-secret
EOF

echo ""
echo "========================================"
echo "构建推送完成!"
echo "========================================"
echo "镜像:"
echo "  ${HARBOR_URL}/${PROJECT}/${BACKEND_IMAGE}:${VERSION}"
echo "  ${HARBOR_URL}/${PROJECT}/${FRONTEND_IMAGE}:${VERSION}"
echo ""
echo "部署步骤:"
echo "1. 创建镜像拉取密钥:"
echo "   kubectl create secret docker-registry harbor-secret \\"
echo "     --docker-server=${HARBOR_URL} \\"
echo "     --docker-username=<harbor用户名> \\"
echo "     --docker-password=<harbor密码> \\"
echo "     -n opendeepwiki"
echo ""
echo "2. 部署更新:"
echo "   kubectl apply -f harbor-deployment.yaml"
echo ""
