#!/system/bin/sh
# [CAMERA IP] 2026-10-21: dựng mạng K02 -> camera IP (Hikvision) qua cổng Ethernet (adapter USB-LAN, eth0). Chạy 1 lần rồi thoát (lúc boot do ipcam_net_watch.sh gọi, và mỗi khi link eth0 rớt rồi lên lại).
# Do ipcam_net_watch.sh gọi (root). Quy trình: chờ eth0 có link -> chờ camera boot xong (ping được) -> gán IP + ip rule
# -> kiểm tra lại -> OK thì thoát (script này không chạy nền; việc theo dõi link là của ipcam_net_watch.sh). Hết hạn chờ mà camera không lên: ghi log rồi thoát (chạy lại tay được).
#
# Vì sao cần: eth0 do K02 cấu hình TAY (không DHCP) —
#  1) Android XOÁ IP của eth0 mỗi lần link rớt -> cần gán lại;
#  2) Android coi Wi-Fi là mạng mặc định nên gói tới dải camera đi qua wlan0 -> cần `ip rule` ép dải camera qua bảng main (route dev eth0).
# Chạy tay khi cần: adb shell "su 0 sh /data/local/tmp/ipcam_net.sh"
#
# Nguồn dữ liệu duy nhất: /sdcard/EduXplore/ipcamera.json (cùng file app đọc): "source":"ip", "host", "localIp".
# Không có file / source khác "ip" -> thoát ngay. Giả định mask /24, giao diện eth0.
# Test: IPCAM_CONF=<file> sh ipcam_net.sh   (IPCAM_IF đổi giao diện, IPCAM_WAIT đổi thời gian chờ tối đa, giây)
CONF=${IPCAM_CONF:-/data/media/0/EduXplore/ipcamera.json}
IF=${IPCAM_IF:-eth0}
MAXWAIT=${IPCAM_WAIT:-240}
LOG=/data/local/tmp/ipcam_net_log.txt
PREF=5000

log() { echo "$(date '+%m-%d %H:%M:%S') $1" >> $LOG; }

# Lúc boot sdcard (/data/media) có thể chưa sẵn sàng: chờ đọc được file cấu hình.
n=0
while [ ! -r "$CONF" ] && [ $n -lt 60 ]; do sleep 2; n=$((n + 2)); done
if [ ! -r "$CONF" ]; then log "không đọc được $CONF -> bỏ qua"; exit 0; fi
if ! grep -q '"source"[[:space:]]*:[[:space:]]*"ip"' "$CONF"; then log "source != ip -> bỏ qua"; exit 0; fi

LIP=$(sed -n 's/.*"localIp"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$CONF" | head -n 1)
HOST=$(sed -n 's/.*"host"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$CONF" | head -n 1)
if [ -z "$LIP" ] || [ -z "$HOST" ]; then log "ipcamera.json thiếu host/localIp"; exit 1; fi
NET="${HOST%.*}.0/24"

# Đặt IP + luật (idempotent). Trả 0 khi cả hai đã có.
apply() {
  ip -4 addr show dev $IF 2>/dev/null | grep -q "inet $LIP/" || ip addr add $LIP/24 dev $IF 2>/dev/null
  ip rule 2>/dev/null | grep -q "^$PREF:.*to $NET lookup main" || ip rule add to $NET lookup main pref $PREF 2>/dev/null
  ip -4 addr show dev $IF 2>/dev/null | grep -q "inet $LIP/" && ip rule 2>/dev/null | grep -q "^$PREF:.*to $NET lookup main"
}

log "bắt đầu: $IF $LIP -> camera $HOST (chờ tối đa ${MAXWAIT}s)"
t=0
while [ $t -lt $MAXWAIT ]; do
  if [ "$(cat /sys/class/net/$IF/carrier 2>/dev/null)" = "1" ]; then
    # Gán trước khi ping (ping cần có IP nguồn); camera boot ~30-90 s nên ping có thể mất vài vòng.
    if apply && ping -c 1 -W 2 $HOST >/dev/null 2>&1; then
      # Kiểm tra lần cuối: cấu hình còn nguyên và camera vẫn trả lời.
      sleep 1
      if apply && ping -c 1 -W 2 $HOST >/dev/null 2>&1; then
        log "OK sau ${t}s: $IF=$LIP, rule to $NET, ping $HOST được -> dừng"
        exit 0
      fi
    fi
  fi
  sleep 3; t=$((t + 3))
done
log "HẾT HẠN sau ${MAXWAIT}s: carrier=$(cat /sys/class/net/$IF/carrier 2>/dev/null) addr=$(ip -4 addr show dev $IF 2>/dev/null | grep -c "inet $LIP/") -> camera chưa trả lời, chạy lại tay nếu cần"
exit 1
