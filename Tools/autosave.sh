#!/bin/sh
# Commit local an toàn để còn revert. Gọi bởi hook SessionEnd và /logwork.
# Commit nếu: (a) cây bẩn VÀ (b) commit gần nhất > 4h trước HOẶC gọi với --force (kết thúc phiên/logwork).
# Không push. Không bao giờ dùng --no-verify: hook chặn game đóng băng vẫn chạy; nếu bị chặn thì báo ra, không im lặng.
cd "$(git rev-parse --show-toplevel)" || exit 0
[ -n "$(git status --porcelain)" ] || exit 0
last=$(git log -1 --format=%ct 2>/dev/null || echo 0); now=$(date +%s)
if [ "$1" != "--force" ] && [ $((now - last)) -lt 14400 ]; then exit 0; fi
git add -A -- . ':!.claude/settings.local.json' >/dev/null 2>&1
if git commit -q -m "wip: autosave $(date '+%Y-%m-%d %H:%M')" >/dev/null 2>&1; then
  echo "autosave: đã commit $(git log -1 --format='%h %s')"
else
  echo "autosave: KHÔNG commit được (có thể bị hook chặn file đóng băng). Chạy: sh handoff/check-frozen.sh --staged" >&2; git reset -q
fi
exit 0
