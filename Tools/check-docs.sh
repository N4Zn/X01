#!/bin/sh
# Kiểm tra link tài liệu: mọi đường dẫn `docs/...`, `handoff/...`, `Tools/...`, `notes/...` trong *.md gốc + docs/ phải tồn tại.
# Dùng: sh Tools/check-docs.sh   (thoát 1 nếu có link gãy)
cd "$(dirname "$0")/.." || exit 2
bad=0
for f in CLAUDE.md AGENTS.md TODO.md handoff/README.md docs/*.md docs/*/*.md NativePlugins/*/CLAUDE.md; do
  [ -f "$f" ] || continue
  for p in $(grep -oE '`(docs|handoff|Tools|notes)/[A-Za-z0-9_./-]+`' "$f" | tr -d '`' | sort -u); do
    case "$p" in *YYYY*|*'<'*) continue ;; esac
    [ -e "$p" ] || { echo "GÃY: $f -> $p"; bad=1; }
  done
done
[ $bad = 0 ] && echo "Link tài liệu OK."
exit $bad
