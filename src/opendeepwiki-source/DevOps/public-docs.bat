@echo off
REM 构建并推送 docs 文档镜像
cd /d %~dp0..
docker build -t crpi-j9ha7sxwhatgtvj4.cn-shenzhen.personal.cr.aliyuncs.com/open-deepwiki/opendeepwiki-docs:latest -f docs/Dockerfile docs
if %errorlevel% neq 0 (
    echo 构建失败!
    pause
    exit /b 1
)
echo 构建完成，开始推送镜像...
docker push crpi-j9ha7sxwhatgtvj4.cn-shenzhen.personal.cr.aliyuncs.com/open-deepwiki/opendeepwiki-docs:latest
if %errorlevel% neq 0 (
    echo 推送失败!
) else (
    echo 推送完成: crpi-j9ha7sxwhatgtvj4.cn-shenzhen.personal.cr.aliyuncs.com/open-deepwiki/opendeepwiki-docs:latest
)
pause
