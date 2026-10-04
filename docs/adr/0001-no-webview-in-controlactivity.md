# ADR 0001 — ControlActivity dùng Android View thuần, không WebView
- **Trạng thái**: chấp nhận (2026-09)
- **Bối cảnh**: Chromium không init được GL context trên K02 (lỗi driver GPU tầng hệ thống, xác nhận thực tế, không sửa được từ app).
- **Quyết định**: UI điều khiển dựng bằng View/code Java, màu qua token `res/values/colors.xml`.
- **Hệ quả**: không dùng được web UI cho tablet; đổi giao diện = sửa Java + build lại aar (`/build-aar`).
