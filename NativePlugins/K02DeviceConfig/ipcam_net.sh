#!/system/bin/sh
# [CAMERA IP] 2026-10-21: giữ mạng K02 -> camera IP (Hikvision) qua cổng Ethernet (adapter USB-LAN, eth0).
# Chạy VĨNH VIỄN (vòng lặp) do mirror_hdmi.sh.usbgrant gọi ở cuối, bằng root.
#
# Vì sao cần vòng lặp: eth0 do K02 cấu hình TAY (không DHCP, Android không coi là mạng nên không tự giữ IP) —
#  1) mỗi lần link rớt (cáp lỏng, camera/PoE mất nguồn) Android XOÁ IP của eth0 -> app báo EADDRNOTAVAIL;
#  2) Android coi Wi-Fi là mạng mặc định nên gói tới dải camera mặc định đi qua wlan0 (ping -I eth0 vẫn chạy
#     nên dễ tưởng mạng ổn) -> cần luật `ip rule` ép dải camera đi qua bảng main (có route dev eth0).
#
# Nguồn dữ liệu duy nhất: /sdcard/EduXplore/ipcamera.json (cùng file app đọc): "source":"ip", "host", "localIp".
# Không có file / source khác "ip" -> không làm gì. Sửa file có hiệu lực trong vài giây, không cần reboot.
# Giả định mask /24, giao diện eth0. Test: IPCAM_CONF=<file> sh ipcam_net.sh
CONF=${IPCAM_CONF:-/data/media/0/EduXplore/ipcamera.json}
IF=${IPCAM_IF:-eth0}
LOG=/data/local/tmp/ipcam_net_log.txt
PREF=5000
LASTMSG=""

log() { [ "$1" != "$LASTMSG" ] && { echo "$(date '+%m-%d %H:%M:%S') $1" >> $LOG; LASTMSG="$1"; }; }

while true; do
  if [ -r "$CONF" ] && grep -q '"source"[[:space:]]*:[[:space:]]*"ip"' "$CONF"; then
    LIP=$(sed -n 's/.*"localIp"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$CONF" | head -n 1)
    HOST=$(sed -n 's/.*"host"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$CONF" | head -n 1)
    if [ -n "$LIP" ] && [ -n "$HOST" ]; then
      NET="${HOST%.*}.0/24"
      if [ "$(cat /sys/class/net/$IF/carrier 2>/dev/null)" = "1" ]; then
        if ! ip -4 addr show dev $IF 2>/dev/null | grep -q "inet $LIP/"; then
          ip addr add $LIP/24 dev $IF 2>/dev/null && log "link up: gán $LIP/24 cho $IF"
        fi
        if ! ip rule 2>/dev/null | grep -q "^$PREF:.*to $NET lookup main"; then
          ip rule add to $NET lookup main pref $PREF 2>/dev/null && log "thêm ip rule to $NET lookup main pref $PREF"
        fi
      else
        log "$IF chưa có link (carrier=0)"
      fi
    else
      log "ipcamera.json thiếu host/localIp"
    fi
  fi
  sleep 3
done
