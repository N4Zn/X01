#!/bin/sh
# Build snaptest.jar (dex) để chạy SnapshotClient trên K02 bằng app_process — không cần Gradle/aar.
# Dùng: sh Tools/ip_camera_test/build.sh   -> Tools/ip_camera_test/out/snaptest.jar
set -e
HERE=$(cd "$(dirname "$0")" && pwd)
LIB="$HERE/../../NativePlugins/UnityFacePluginAndroidLib/src/main/java/com/eduxplore/faceplugin"
SDK=${ANDROID_HOME:-$LOCALAPPDATA/Android/Sdk}
JDK=${JAVA_HOME:-/c/Program\ Files/Android/Android\ Studio/jbr}
[ -x "$JDK/bin/javac" ] || [ -x "$JDK/bin/javac.exe" ] || JDK="/c/Program Files/Android/Android Studio/jbr"
BT=$(ls -d "$SDK"/build-tools/* | sort -V | tail -1)
AJAR=$(ls -d "$SDK"/platforms/android-* | sort -V | tail -1)/android.jar
OUT="$HERE/out"

rm -rf "$OUT"; mkdir -p "$OUT/classes"
"$JDK/bin/javac" --release 8 -Xlint:-options -cp "$AJAR" -d "$OUT/classes" \
  "$LIB/SnapshotClient.java" "$LIB/IpCameraConfig.java" "$HERE/SnapshotTest.java"
"$JDK/bin/java" -cp "$BT/lib/d8.jar" com.android.tools.r8.D8 --lib "$AJAR" --output "$OUT" \
  $(find "$OUT/classes" -name '*.class')
( cd "$OUT" && "$JDK/bin/jar" cf snaptest.jar classes.dex )
echo "OK: $OUT/snaptest.jar"
