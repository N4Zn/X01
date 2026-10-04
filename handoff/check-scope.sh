#!/bin/sh
# Dùng: sh handoff/check-scope.sh handoff/<viec>.md [base-ref]
# Đọc `scope:` trong front matter, báo lỗi (exit 1) nếu có file đã sửa nằm NGOÀI scope.
# So: thay đổi chưa commit + commit từ base-ref (mặc định: merge-base với origin/main, không có thì HEAD~0 chỉ so working tree).
task="$1"; [ -f "$task" ] || { echo "Thiếu file việc: $task"; exit 2; }
cd "$(git rev-parse --show-toplevel)" || exit 2
base="${2:-$(git merge-base HEAD origin/main 2>/dev/null)}"
globs=$(awk '/^---/{n++; next} n==1 && /^scope:/{s=1; next} n==1 && s && /^  - /{sub(/^  - /,""); sub(/[ \t]*#.*/,""); print; next} n==1 && s && !/^  - /{s=0}' "$task")
[ -n "$globs" ] || { echo "Không đọc được scope trong $task"; exit 2; }
# glob -> regex
re=$(printf '%s\n' "$globs" | sed 's/[.+^$(){}|]/\&/g; s/\*\*/\x01/g; s/\*/[^\/]*/g; s/\x01/.*/g; s/^/^/; s/$/$/' | paste -sd'|' -)
re="$re|^$(printf '%s' "$task" | sed 's/[.]/\./g')\$|^handoff/README\.md\$"
files=$( { [ -n "$base" ] && git diff --name-only "$base" HEAD; git diff --name-only HEAD; git ls-files -o --exclude-standard; } 2>/dev/null | sort -u)
bad=$(printf '%s\n' "$files" | grep -v '^$' | grep -vE "$re")
if [ -n "$bad" ]; then echo "NGOÀI SCOPE ($task):"; echo "$bad"; exit 1; fi
echo "OK: mọi thay đổi nằm trong scope."; exit 0
