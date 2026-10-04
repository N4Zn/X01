#!/bin/sh
# Kiểm tra + build các module Android (.aar) rồi copy vào Assets/Plugins/Android/.
# Dùng:  sh Tools/build-aar.sh [controlui|lidarlib|faceenroll|all|changed] [--check-only]
#   changed (mặc định) = chỉ module có source mới hơn aar đã build
#   --check-only       = chỉ kiểm tra tiền điều kiện, không build
# Thoát 0 = OK, !=0 = lỗi (xem build-aar.log). KHÔNG commit/push; in lệnh git add gợi ý ở cuối.
# libvlc/unityplugin aar là file ngoài, script này không build. liblidar_unity.so: xem NativePlugins/LidarUnity/CLAUDE.md.
set -u
ADD=""
cd "$(dirname "$0")/.." || exit 2
ROOT=$(pwd)
LOG="$ROOT/build-aar.log"
WHAT=changed; CHECK_ONLY=0
for a in "$@"; do
  case "$a" in
    --check-only) CHECK_ONLY=1 ;;
    controlui|lidarlib|faceenroll|all|changed) WHAT=$a ;;
    *) echo "Tham số lạ: $a"; exit 2 ;;
  esac
done

fail() { echo "LỖI: $*" >&2; exit 1; }

# module | thư mục project gradle | gradle task | aar đầu ra (tương đối project) | đích trong Unity
mod_info() {
  case "$1" in
    controlui)  DIR=NativePlugins/ControlUiAndroidLib;  TASK=:controlui:assembleRelease;  OUT=controlui/build/outputs/aar/controlui-release.aar;   DEST=Assets/Plugins/Android/controlui-release.aar;  SRC=controlui/src ;;
    lidarlib)   DIR=NativePlugins/LidarNativeAndroidLib; TASK=:lidarlib:assembleRelease;   OUT=lidarlib/build/outputs/aar/lidarlib-release.aar;     DEST=Assets/Plugins/Android/lidarlib-release.aar;   SRC=lidarlib/src ;;
    faceenroll) DIR=NativePlugins/FaceEnrollAndroidLib;  TASK=:faceenroll:assembleRelease; OUT=faceenroll/build/outputs/aar/faceenroll-release.aar; DEST=Assets/Plugins/Android/faceenroll-release.aar; SRC=faceenroll/src ;;
  esac
}

# --- Tiền điều kiện ---
JAVA_HOME=${JAVA_HOME:-}
if [ ! -x "$JAVA_HOME/bin/java" ] && [ ! -x "$JAVA_HOME/bin/java.exe" ]; then
  JAVA_HOME=
  for c in "/c/Program Files/Android/Android Studio/jbr" "/c/Program Files/Android/Android Studio1/jbr"; do
    [ -d "$c" ] && JAVA_HOME=$c && break
  done
fi
[ -n "${JAVA_HOME:-}" ] && [ -d "$JAVA_HOME" ] || fail "không tìm thấy JDK (đặt JAVA_HOME = jbr của Android Studio)"
export JAVA_HOME
echo "JAVA_HOME=$JAVA_HOME"

sh handoff/check-frozen.sh >/dev/null 2>&1 || fail "check-frozen.sh báo sửa game đóng băng (chạy: sh handoff/check-frozen.sh)"

case "$WHAT" in
  all) MODS="controlui lidarlib faceenroll" ;;
  changed)
    MODS=""
    for m in controlui lidarlib faceenroll; do
      mod_info $m
      # So với commit gần nhất đã chạm aar: source/gradle đổi (đã commit hoặc chưa) => cần build
      BASE=$(git log -1 --format=%H -- "$DEST" 2>/dev/null)
      P="$DIR/$SRC $DIR/$(dirname "$SRC")/build.gradle.kts"
      if [ ! -f "$DEST" ] || [ -z "$BASE" ]          || [ -n "$(git diff --name-only "$BASE" -- $P 2>/dev/null | head -1)" ]          || [ -n "$(git ls-files --others --exclude-standard -- $P | head -1)" ]; then
        MODS="$MODS $m"
      fi
    done ;;
  *) MODS=$WHAT ;;
esac
if [ -z "${MODS# }" ]; then echo "Không module nào thay đổi — không cần build."; exit 0; fi
echo "Module cần build:$MODS"

# Kiểm tra riêng từng module
for m in $MODS; do
  mod_info $m
  [ -f "$DIR/gradlew" ] || fail "$m: thiếu $DIR/gradlew"
  [ -f "$DIR/local.properties" ] || fail "$m: thiếu local.properties (sdk.dir)"
  if [ "$m" = controlui ]; then
    REG=NativePlugins/ControlUiAndroidLib/controlui/src/main/assets/game_registry.json
    python -c "import json,sys;json.load(open(sys.argv[1],encoding='utf-8'))" "$REG" 2>/dev/null \
      || python3 -c "import json,sys;json.load(open(sys.argv[1],encoding='utf-8'))" "$REG" 2>/dev/null \
      || fail "controlui: $REG không phải JSON hợp lệ"
  fi
done
echo "Tiền điều kiện OK."
[ "$CHECK_ONLY" = 1 ] && exit 0

: > "$LOG"
for m in $MODS; do
  mod_info $m
  echo ">>> Build $m ($TASK)"
  ( cd "$DIR" && ./gradlew --console=plain "$TASK" ) >> "$LOG" 2>&1 \
    || { tail -40 "$LOG"; fail "$m: gradle build thất bại (log đầy đủ: build-aar.log)"; }
  AAR="$DIR/$OUT"
  [ -f "$AAR" ] || fail "$m: không thấy $AAR sau build"
  # Kiểm tra cấu trúc aar
  LIST=$(unzip -l "$AAR" 2>/dev/null) || fail "$m: aar hỏng (unzip lỗi)"
  echo "$LIST" | grep -q 'AndroidManifest.xml' || fail "$m: aar thiếu AndroidManifest.xml"
  echo "$LIST" | grep -q 'classes.jar' || fail "$m: aar thiếu classes.jar"
  cp -f "$AAR" "$DEST" || fail "$m: copy vào $DEST thất bại"
  SIZE=$(wc -c < "$DEST")
  echo "    OK: $DEST ($SIZE byte)"
  ADD="$ADD $DEST"
done
echo
echo "XONG. Gợi ý: git add${ADD:-}   (aar vào LFS tự động) — rồi build lại APK trong Unity."
