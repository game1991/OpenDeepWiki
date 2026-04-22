#!/bin/bash
# sync-wiki-db.sh
# 将 OpenDeepWiki SQLite 数据库拷贝到 WSL2 本地目录，供 Windows 数据库工具只读查看
# 用法：bash scripts/sync-wiki-db.sh

set -e

NAMESPACE="opendeepwiki"
DATA_DIR="$(cd "$(dirname "$0")/.." && pwd)/data"
DB_FILE="opendeepwiki.db"

# 确保目录存在
mkdir -p "$DATA_DIR"

# 获取后端 Pod 名称
POD=$(kubectl get pods -n "$NAMESPACE" -l app.kubernetes.io/component=backend -o jsonpath='{.items[0].metadata.name}' 2>/dev/null)

if [ -z "$POD" ]; then
    echo "Error: 后端 Pod 不存在，请确认 OpenDeepWiki 已部署"
    exit 1
fi

# 拷贝数据库文件
echo "同步 $POD:/data/$DB_FILE -> $DATA_DIR/$DB_FILE"
kubectl cp "$NAMESPACE/$POD:/data/$DB_FILE" "$DATA_DIR/$DB_FILE"

# 将 WAL 模式转为 DELETE 模式，并清除 WAL 辅助文件
sqlite3 "$DATA_DIR/$DB_FILE" "PRAGMA journal_mode=DELETE;" > /dev/null
rm -f "$DATA_DIR/$DB_FILE-shm" "$DATA_DIR/$DB_FILE-wal"

# 拷贝到 Windows 本地目录（\\wsl$\ 9P 协议不支持文件锁，Navicat 直接打开会报 database is locked）
# 自动检测 Windows 用户目录
WIN_USER_DIR=$(ls -d /mnt/c/Users/*/ 2>/dev/null | grep -v -E '(Public|Default|All Users)' | head -1)
WIN_DIR="${WIN_USER_DIR}OpenDeepWiki"
mkdir -p "$WIN_DIR"
cp "$DATA_DIR/$DB_FILE" "$WIN_DIR/$DB_FILE"

FILE_SIZE=$(ls -lh "$DATA_DIR/$DB_FILE" | awk '{print $5}')
echo "完成! 文件大小: $FILE_SIZE"
echo ""
WIN_DISPLAY=$(echo "$WIN_DIR/$DB_FILE" | sed 's|/mnt/c/|C:\\|; s|/|\\|g')
echo "Navicat 打开路径（Windows 本地磁盘，文件锁正常）："
echo "  $WIN_DISPLAY"