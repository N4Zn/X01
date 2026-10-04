# GenericGame: import zip thành scene riêng

> **TL;DR**: Mỗi zip từ web builder = 1 scene riêng + prefab layout; tool Tools/GenericGame/Import Zip. game.json là bản cuối; importer chèn registry nhưng KHÔNG rebuild aar.
> **Đọc khi**: import/update game GenericGame, sửa layer/icon/marker, GenericGameRegistryWriter.
> **Cập nhật**: 2026-10-04 (tách nguyên văn từ CLAUDE.md gốc, chưa sửa nội dung)

### Import zip → scene riêng cho từng game (2026-10-03)

Mỗi game `.zip` xuất từ web builder = 1 scene riêng chỉnh được trong Editor (thay vì scene dùng chung `GenericGamePlayer` dựng lúc runtime). Chi tiết quy trình: `GenericGame/SCHEMA.md`, mục "Scene riêng cho từng game".

- **Tool**: `Tools/GenericGame/Import Zip (1 file)...` / `Import Zips (cả thư mục)...` (`Assets/Game/Scripts/Editor/MiniGameKit/GenericGameZipImporter.cs`). `gameId` suy từ TÊN FILE zip (bỏ dấu, PascalCase) — **đổi tên file zip = ra game mới**.
- **Kết quả mỗi game**: `Assets/Game/GenericGames/<id>/{game.json, <id>_LayoutBase.prefab, <id>_Layout.prefab (Variant)}`, scene `Assets/Game/Scenes/_GenericGame/Games/<id>.unity` (nhân bản từ scene mẫu), ảnh/âm thanh vào `Resources/TestTongHop/{images,audio}/GenericGames/<id>/`, tự thêm vào Build Settings. **Đăng ký registry**: nếu `gameId` chưa có trong `GameRegistry.cs`, importer hiện hộp thoại (tên hiển thị / category / group) rồi tự chèn `Set(...)` NGAY TRÊN marker `// <GENERIC-GAMES>` (đừng xoá marker) + thêm mục vào `game_registry.json` (`NativePlugins/ControlUiAndroidLib/.../assets/`) — code ở `GenericGameRegistryWriter.cs`. **Importer KHÔNG rebuild aar**: sau khi json đổi phải chạy tay (PowerShell, từ gốc repo): `cd NativePlugins\ControlUiAndroidLib; .\gradlew.bat :controlui:assembleRelease; Copy-Item controlui\build\outputs\aar\controlui-release.aar ..\..\Assets\Plugins\Android\controlui-release.aar -Force` rồi build lại APK.
- **Update bằng cách import lại cùng zip tên**: có hộp xác nhận. Ghi đè `game.json`/ảnh/âm thanh/`LayoutBase`; **giữ nguyên scene + Variant** (chỉnh tay còn). Chưa kiểm chứng: Variant có mất override khi `LayoutBase` dựng lại không.
- **Nguyên tắc: `game.json` là bản cuối.** Unity chỉ port phần json không có. Mọi xung đột phải báo user thống nhất, KHÔNG ép "luôn nằm trên cùng" bất kể `layerOrder`.
  - Marker (`GenericBakedRect`) trong Variant ghi đè vị trí json lúc chạy; lệch >0.1% → `LogWarning "XUNG ĐỘT"` (đang dùng vị trí scene). Câu hỏi còn mở: marker thắng hay json thắng?
  - Layer: `sortingOrder = (vị trí trong layerOrder + 1) × 10`; icon ✔/✖ = layer `feedback`. **GameHUD** (không có trong json) = `feedback − 5`, và nền web (`layout.background`) thay luôn nền HUD (`GameHUD.SetBackground`). Log Play: `[GenericGameKit] Layer order ...`.
- **Gotcha Canvas trên object đang tắt**: Canvas thêm vào object tắt (icon ✔/✖) mất `overrideSorting`/`sortingOrder` khi bật lần đầu → `ApplyLayer` bật tạm rồi tắt lại. Đã sửa và xác nhận bằng Play (icon lên top); các log chẩn đoán layer/icon đã xoá.
- **Chỉnh kích thước icon ✔/✖**: icon nằm trong scene (không phải prefab layout), kích thước do **Anchors** quyết định; **đừng chỉnh Scale** vì `FeedbackEffect` ép `BaseScale = 1.5` mỗi lần hiện. 4 icon (Left/Right × Correct/Wrong) chỉnh riêng.
- **Chưa làm**: đăng ký `CuaHangKemTruocSau`/`ThuNghiem12` vào GameRegistry (import lại zip → hộp thoại đăng ký sẽ hiện); chưa test hộp thoại/ghi registry trong Unity; so schema `game_builder_v2.2_1003.html` (mới hơn v2) với types Unity; tool so/reset marker Variant theo json mới.
