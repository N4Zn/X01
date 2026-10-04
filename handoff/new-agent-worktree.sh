#!/bin/sh
# Dùng: sh handoff/new-agent-worktree.sh <ten-viec> [base-branch]
# Tạo nhánh agent/<ten-viec> + thư mục làm việc RIÊNG ../eduX-agent-<ten-viec> để agent không dẫm lên cây của bạn.
# Lưu ý: worktree không có Library/ của Unity — agent Android thuần (NativePlugins/) không cần; việc Unity thì mở project ở worktree sẽ import lại lâu.
name="$1"; [ -n "$name" ] || { echo "Thiếu tên việc"; exit 2; }
cd "$(git rev-parse --show-toplevel)" || exit 2
base="${2:-$(git branch --show-current)}"
dir="../eduX-agent-$name"
git worktree add -b "agent/$name" "$dir" "$base" && echo "Xong. Mở Android Studio tại: $(cd "$dir" && pwd)"
