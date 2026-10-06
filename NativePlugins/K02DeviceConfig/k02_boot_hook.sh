#!/system/bin/sh
# K02 #1: hook root chạy lúc boot — (1) chặn thanh thông báo, (2) tự cấp quyền USB (lidar + camera) cho app.
# Được gọi bởi /system/etc/init/lidar_config.rc:
#   on property:sys.boot_completed=1 -> exec u:r:su:s0 -- /data/local/tmp/your_script.sh
# `exec` của init chạy ĐỒNG BỘ nên mọi việc chờ/lặp nằm trong tiến trình nền, script thoát ngay.
PKG=com.EduXplore.X01a
DEX=/data/local/tmp/UsbGrant.dex
LOG=/data/local/tmp/boot_hook_log.txt
(
  echo "=== $(date) boot hook start" > $LOG

  # (1) Thanh thông báo: mặc định chặn; app Launcher ghi cờ shade_enabled=1 thì để mở.
  SHADE=block
  for f in /data/media/0/Android/data/com.launcher.eduxplore/files/shade_enabled \
           /data/media/0/Android/data/com.launcher.eduxplore.debug/files/shade_enabled; do
    [ "$(cat $f 2>/dev/null)" = "1" ] && SHADE=allow
  done
  echo "shade: $SHADE" >> $LOG
  if [ "$SHADE" = "block" ]; then
    (
      sleep 8
      i=0
      while [ $i -lt 20 ]; do
        cmd statusbar disable-for-setup true >> $LOG 2>&1 && echo "statusbar try $i ok" >> $LOG && break
        i=$((i+1)); sleep 2
      done
      sleep 15; cmd statusbar disable-for-setup true >> $LOG 2>&1
    ) &
  fi

  # (2) USB: quyền USB trên Android 10 chỉ sống trong bộ nhớ (mất khi reboot / cài lại app / rút cắm cáp)
  # nên cấp lại mỗi khi "dấu vết" đổi: thư mục cài app (đổi mỗi lần cài) hoặc danh sách thiết bị USB.
  # UsbGrant.dex cấp mọi device đang cắm cho uid của app (lidar CP2102 4292:60000, camera 1410:2848, ...).
  LAST=""
  while true; do
    KEY="$(ls -d /data/app/$PKG-* 2>/dev/null)|$(ls -R /dev/bus/usb 2>/dev/null)"
    if [ "$KEY" != "$LAST" ]; then
      sleep 3   # chờ thiết bị/app ổn định rồi mới cấp
      KEY="$(ls -d /data/app/$PKG-* 2>/dev/null)|$(ls -R /dev/bus/usb 2>/dev/null)"
      AUID=$(pm list packages -U $PKG 2>/dev/null | sed -n 's/.*uid:\([0-9]*\).*/\1/p')
      if [ -n "$AUID" ]; then
        OUT=$(CLASSPATH=$DEX app_process /system/bin UsbGrant $AUID 2>&1 | tr '\n' ' ')
        echo "$(date +%H:%M:%S) usb grant uid=$AUID: $OUT" >> $LOG
      else
        echo "$(date +%H:%M:%S) usb: chưa thấy package $PKG" >> $LOG
      fi
      LAST="$KEY"
    fi
    sleep 5
  done
) >/dev/null 2>&1 &
exit 0
