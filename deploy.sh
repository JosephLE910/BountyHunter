#!/bin/bash
# deploy.sh — 将 BountyHunter 代码部署到 Karting Microgame 模板
#
# 用法：
#   bash deploy.sh <KartingMicrogame 项目根目录>
#
# 示例：
#   bash deploy.sh "D:/Computer/KartingMicrogame"
#
# 效果：
#   1. 覆盖模板中的同名文件（如 ArcadeKart.cs）
#   2. 在模板中创建 BountyHunter 目录并写入我们的脚本
#   3. 在 Packages/manifest.json 中添加 Netcode for GameObjects 依赖

set -e

# ── 参数检查 ────────────────────────────────────────────────────────────────
if [ -z "$1" ]; then
    echo "用法: bash deploy.sh <KartingMicrogame 项目路径>"
    echo "示例: bash deploy.sh \"D:/Computer/KartingMicrogame\""
    exit 1
fi

TEMPLATE="$1"
SCRIPTS="$TEMPLATE/Assets/Karting/Scripts"
BH_SRC="$(cd "$(dirname "$0")" && pwd)"  # BountyHunter 仓库根目录

# 检查模板目录是否存在
if [ ! -d "$SCRIPTS/KartSystems" ]; then
    echo "错误：找不到 $SCRIPTS/KartSystems，请确认路径是 Karting Microgame 项目根目录"
    exit 1
fi

echo "=== BountyHunter 部署开始 ==="
echo "来源：$BH_SRC"
echo "目标：$TEMPLATE"
echo ""

# ── 1. 覆盖模板文件（ArcadeKart.cs）──────────────────────────────────────
echo "[1/3] 覆盖模板文件..."
cp "$BH_SRC/Physics/ArcadeKart.cs" "$SCRIPTS/KartSystems/ArcadeKart.cs"
echo "  ✓ KartSystems/ArcadeKart.cs"

# ── 2. 部署 BountyHunter 脚本目录 ────────────────────────────────────────
echo "[2/3] 部署 BountyHunter 脚本..."

BH_DST="$SCRIPTS/BountyHunter"
mkdir -p "$BH_DST/Shared" "$BH_DST/Physics" "$BH_DST/AI/States" "$BH_DST/Network"

# Shared
cp "$BH_SRC/Shared/"*.cs    "$BH_DST/Shared/"
cp "$BH_SRC/Shared/"*.asmdef "$BH_DST/"
echo "  ✓ Shared"

# Physics
cp "$BH_SRC/Physics/"*.cs     "$BH_DST/Physics/"
cp "$BH_SRC/Physics/"*.asmdef "$BH_DST/Physics/"
# ArcadeKart.cs 属于模板覆盖，不放到 BountyHunter 子目录
rm -f "$BH_DST/Physics/ArcadeKart.cs"
echo "  ✓ Physics"

# AI
cp "$BH_SRC/AI/"*.cs      "$BH_DST/AI/"
cp "$BH_SRC/AI/"*.asmdef  "$BH_DST/AI/"
cp "$BH_SRC/AI/States/"*.cs "$BH_DST/AI/States/"
echo "  ✓ AI"

# Network
cp "$BH_SRC/Network/"*.cs     "$BH_DST/Network/"
cp "$BH_SRC/Network/"*.asmdef "$BH_DST/Network/"
echo "  ✓ Network"

# ── 3. 更新 manifest.json（添加 NGO）─────────────────────────────────────
echo "[3/3] 检查 Packages/manifest.json..."

MANIFEST="$TEMPLATE/Packages/manifest.json"
NGO_KEY="com.unity.netcode.gameobjects"
NGO_VER="1.12.0"

if grep -q "$NGO_KEY" "$MANIFEST"; then
    echo "  ✓ Netcode for GameObjects 已存在，跳过"
else
    # 在第一个 dependency 前插入 NGO
    sed -i "s/\"dependencies\": {/\"dependencies\": {\n    \"$NGO_KEY\": \"$NGO_VER\",/" "$MANIFEST"
    echo "  ✓ 已添加 $NGO_KEY: $NGO_VER"
fi

echo ""
echo "=== 部署完成 ==="
echo "请在 Unity 中打开项目，等待包解析完成后即可运行。"
