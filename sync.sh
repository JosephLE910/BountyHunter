#!/bin/bash
# sync.sh — 将 KartingMicrogame 中被修改的模板文件同步回 BountyHunter 仓库
#
# 用法：
#   bash sync.sh <KartingMicrogame 项目根目录>
#
# 示例：
#   bash sync.sh "D:/Computer/KartingMicrogame"
#
# 说明：
#   每次修改模板文件后运行此脚本，确保 BountyHunter 仓库中的备份是最新的。
#   修改记录见 README.md「遇到的问题及修复记录」章节。

set -e

if [ -z "$1" ]; then
    echo "用法: bash sync.sh <KartingMicrogame 项目路径>"
    echo "示例: bash sync.sh \"D:/Computer/KartingMicrogame\""
    exit 1
fi

TEMPLATE="$1"
SCRIPTS="$TEMPLATE/Assets/Karting/Scripts"
BH="$(cd "$(dirname "$0")" && pwd)"

if [ ! -d "$SCRIPTS/KartSystems" ]; then
    echo "错误：找不到 $SCRIPTS/KartSystems，请确认路径正确"
    exit 1
fi

echo "=== BountyHunter 同步开始 ==="
echo "来源：$TEMPLATE"
echo "目标：$BH"
echo ""

# ── 模板修改文件（需手动维护此列表）────────────────────────────────────────
# 格式：cp "<模板中的路径>" "<BountyHunter 中的备份路径>"
# 每次新增对模板文件的修改，在此添加一行。

echo "[模板修改文件]"
cp "$SCRIPTS/KartSystems/ArcadeKart.cs" "$BH/Physics/ArcadeKart.cs"
echo "  ✓ Physics/ArcadeKart.cs"

# ── BountyHunter 自有脚本（全量同步）────────────────────────────────────────
echo ""
echo "[BountyHunter 脚本]"
BH_SRC="$SCRIPTS/BountyHunter"

cp "$BH_SRC/Shared/"*.cs     "$BH/Shared/"
cp "$BH_SRC/"*.asmdef        "$BH/Shared/"    2>/dev/null || true
echo "  ✓ Shared"

cp "$BH_SRC/Physics/"*.cs    "$BH/Physics/"
cp "$BH_SRC/Physics/"*.asmdef "$BH/Physics/"
rm -f "$BH/Physics/ArcadeKart.cs.bak" 2>/dev/null || true
echo "  ✓ Physics"

cp "$BH_SRC/AI/"*.cs         "$BH/AI/"
cp "$BH_SRC/AI/"*.asmdef     "$BH/AI/"
cp "$BH_SRC/AI/States/"*.cs  "$BH/AI/States/"
echo "  ✓ AI"

cp "$BH_SRC/Network/"*.cs    "$BH/Network/"
cp "$BH_SRC/Network/"*.asmdef "$BH/Network/"
echo "  ✓ Network"

cp "$BH_SRC/Editor/"*.cs     "$BH/Editor/"
cp "$BH_SRC/Editor/"*.asmdef "$BH/Editor/"
echo "  ✓ Editor"

echo ""
echo "=== 同步完成 ==="
echo "建议接着运行 git add . && git commit 提交变更。"
