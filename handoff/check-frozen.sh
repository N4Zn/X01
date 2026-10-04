#!/bin/sh
# Dùng: sh handoff/check-frozen.sh [--staged | --range <base>...<head>]   (không cờ = kiểm tra mọi thay đổi chưa commit)
# Thoát 1 nếu có file thuộc game đóng băng bị sửa. Hook pre-commit gọi với --staged.
cd "$(git rev-parse --show-toplevel)" || exit 0
if [ "$1" = "--staged" ]; then files=$(git diff --cached --name-only)
elif [ "$1" = "--range" ]; then files=$(git diff --name-only "$2")   # CI: base...head
else files=$( (git diff --name-only HEAD; git ls-files -o --exclude-standard) | sort -u); fi
pat=$(grep -vE '^\s*(#|$)' handoff/FROZEN.txt)
bad=$(printf '%s\n' "$files" | grep -E "$(printf '%s\n' "$pat" | paste -sd'|' -)")
if [ -n "$bad" ]; then
  echo "CHẶN: file thuộc game ĐÓNG BĂNG (handoff/FROZEN.txt) bị sửa:"; echo "$bad"
  echo "Hoàn tác: git checkout HEAD -- <file>  (hoặc git restore --staged --worktree <file>)"; exit 1
fi
exit 0
