---
id: yyyy-mm-dd-ten-viec
status: TODO            # TODO → DOING → REVIEW → DONE
assignee: android-agent # android-agent | claude-code | user
branch: agent/ten-viec  # mỗi việc 1 nhánh (+ worktree riêng, xem README)
scope:                  # CHỈ được sửa các đường dẫn này (glob: * = trong 1 thư mục, ** = mọi cấp)
  - NativePlugins/ControlUiAndroidLib/**
  - Assets/Plugins/Android/controlui-release.aar
---
# <Tên việc>

## Mục tiêu
1–3 câu: kết quả cuối cùng cần có.

## Ngoài phạm vi (tuyệt đối không làm)
- Game trong `handoff/FROZEN.txt`
- ...

## Tiêu chí nghiệm thu (kiểm chứng được)
- [ ] ...
- [ ] `sh handoff/check-scope.sh handoff/<file này>` thoát 0
- [ ] `sh handoff/check-frozen.sh` thoát 0

## Cách kiểm tra
Lệnh build / bước test cụ thể.

## Bối cảnh & hợp đồng
Link tới `docs/contracts/...`, ADR liên quan. Không chép dài vào đây.

## Kết quả
(agent điền khi xong: file đã sửa / chưa làm / chưa test / chỗ nghi ngờ)

## Câu hỏi
(ghi ở đây, không hỏi trong chat)
