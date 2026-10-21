#!/system/bin/sh
# [CAMERA IP] 2026-10-21: chạy ipcam_net.sh lúc boot, rồi ngồi chờ SỰ KIỆN link/địa chỉ của eth0 (`ip monitor`, không poll,
# không tốn CPU) để gán lại IP + luật khi Android xoá IP vì link rớt (cáp lỏng, camera mất nguồn rồi bật lại).
# Do mirror_hdmi.sh.usbgrant gọi cuối script boot (exec -> thành tiến trình chính của service, init không giết).
# Mỗi lần có sự kiện: nếu IP + luật còn nguyên thì bỏ qua, thiếu thì chạy ipcam_net.sh (chờ camera lên, gán, kiểm tra, thoát).
SETUP=/data/local/tmp/ipcam_net.sh
CONF=${IPCAM_CONF:-/data/media/0/EduXplore/ipcamera.json}
IF=${IPCAM_IF:-eth0}
PREF=5000

# Còn nguyên khi: eth0 có IP đúng localIp và ip rule của dải camera còn. File cấu hình không "source":"ip" -> coi như ổn (không làm gì).
healthy() {
  [ -r "$CONF" ] && grep -q '"source"[[:space:]]*:[[:space:]]*"ip"' "$CONF" || return 0
  LIP=$(sed -n 's/.*"localIp"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$CONF" | head -n 1)
  HOST=$(sed -n 's/.*"host"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$CONF" | head -n 1)
  ip -4 addr show dev $IF 2>/dev/null | grep -q "inet $LIP/" && ip rule 2>/dev/null | grep -q "^$PREF:.*to ${HOST%.*}.0/24 lookup main"
}

sh $SETUP

# Sự kiện do chính ipcam_net.sh gây ra (thêm IP) chỉ dẫn tới 1 vòng healthy() rồi bỏ qua nên không lặp vô hạn.
ip monitor link address | while read -r line; do
  case "$line" in *"$IF"*) ;; *) continue ;; esac
  sleep 2   # link rớt/lên thường kéo theo nhiều dòng sự kiện liên tiếp: chờ chúng lắng xuống
  healthy || sh $SETUP
done
