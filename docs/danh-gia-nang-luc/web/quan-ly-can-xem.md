# Người quản lý cần xem gì — đối chiếu với báo cáo web

> **TL;DR**: 6 nhóm câu hỏi của quản lý (hiện trạng · tiến bộ · tham gia · nhóm cần hỗ trợ · chất lượng nội dung · việc cần làm) và báo cáo đã trả lời được cái nào, bằng dữ liệu nào, còn thiếu gì.
> **Đọc khi**: quyết định thêm số liệu/biểu đồ vào báo cáo, hoặc thêm trường vào `report.json`.
> **Cập nhật**: 2026-10-16 (v3: mọi so sánh chỉ trong cùng khối; đa môn; tab Toàn trường).
> **Nguyên tắc**: các khối học chương trình khác nhau → không có số liệu gộp/so sánh giữa khối. "Toàn trường" = xếp hạng lớp *trong từng khối*, theo từng môn. Cột "Trạng thái": ✅ đã có · 🟡 làm được, cần thêm dữ liệu · ⬜ chưa làm.

| # | Câu hỏi của quản lý | Chỉ số | Ở đâu trong web | Trạng thái |
|---|---|---|---|---|
| **1. Hiện trạng** | | | | |
| 1.1 | Khối/lớp đang ở mức nào? | điểm TB, % đạt mục tiêu, phân bố 4 mức | Khối: KPI + Cột/Phân bố mức | ✅ |
| 1.5 | Lớp nào đứng đầu khối, từng môn (1A1 đầu Toán, 1A2 đầu Tiếng Việt) | hạng lớp trong khối theo môn + Tất cả môn | Toàn trường: bảng mỗi khối, ★, dải "Đứng đầu khối" | ✅ |
| 1.2 | Lớp nào cao nhất/thấp nhất, chênh bao nhiêu? | xếp hạng lớp, biên độ (box plot) | Khối: Cột, Biên độ điểm, "Cần chú ý" | ✅ |
| 1.3 | Lớp đều hay phân hoá? | độ rộng hộp, Q1–Q3 | Khối: Biên độ điểm | ✅ |
| 1.4 | Mạnh/yếu ở học phần nào? | điểm TB theo học phần | Bản đồ nhiệt, Radar, thanh học phần | ✅ |
| **2. Tiến bộ (lịch sử)** | | | | |
| 2.1 | Khối/lớp có đang tiến bộ? | Δ điểm TB so tuần đầu / tuần trước (ghép cặp từng bé) | Thẻ khối, KPI, Mức tiến bộ | ✅ |
| 2.2 | Xu hướng theo tuần của từng lớp | điểm TB lớp mỗi tuần vs TB khối/trường | Khối: Xu hướng tuần; Lớp: biểu đồ Xu hướng | ✅ |
| 2.3 | Bé nào tiến bộ nhiều, bé nào đi xuống? | Δ từng bé + sparkline; cờ "giảm ≥ 5 điểm" | Lớp: bảng xếp hạng + "Cần chú ý"; HS: lọc *Giảm điểm* | ✅ |
| 2.4 | Tiến bộ từng học phần của 1 bé | điểm HP mỗi tuần, Δ so tuần đầu | HS: bảng Phân tích kỹ năng | ✅ |
| 2.5 | Bé đạt mục tiêu chưa, còn thiếu bao nhiêu | khoảng cách tới mốc 75 theo học phần | HS: khung Mục tiêu | ✅ |
| **3. Tham gia & chuyên cần** | | | | |
| 3.0 | Khối/lớp nào chơi nhiều / ít / chưa chơi | chơi đều / chơi ít (< 30 câu) / chưa chơi; câu/bé | Toàn trường (cột Mức độ chơi), Khối: Mức độ chơi, Lớp | ✅ |
| 3.1 | Bao nhiêu % bé chơi mỗi tuần? | bé có chơi / sĩ số | Khối: Tham gia theo tuần, KPI | ✅ |
| 3.5 | Bé nào chưa chơi | danh sách theo lớp | Lớp: dưới bảng + "Cần chú ý"; HS: lọc *Chưa chơi* | ✅ |
| 3.2 | Bé nào bỏ chơi ≥ 2 tuần | tuần chơi gần nhất | Lọc *Ít chơi*, "Cần chú ý" | ✅ |
| 3.3 | Mỗi bé chơi bao nhiêu câu/buổi | n câu, số ngày chơi | HS: Lịch sử theo tuần, Nhận xét | ✅ |
| 3.4 | Điểm có đáng tin không (ít mẫu) | dưới 10 câu/học phần → ô mờ, dấu * | mọi bảng | ✅ |
| **4. Nhóm cần hỗ trợ** | | | | |
| 4.1 | Bao nhiêu bé Mức 1–2, ở lớp nào | số bé / % | KPI, Phân bố mức, lọc *Mức 1–2* | ✅ |
| 4.2 | Vì sao yếu: sai nhiều hay chậm? | nhận định (chậm-sai, nhanh-nhầm, đúng-chậm, chưa vững) | Lớp: cột "Cần chú ý"; HS: Nhận định | ✅ |
| 4.3 | Bé điểm thấp đáng chú ý | Mức 1 / điểm thấp nhất | Lớp: "Cần chú ý", lọc *Mức 1–2* | ✅ |
| 4.4 | Bé yếu môn nào | điểm theo môn (Tất cả môn → từng môn) | HS: bảng theo môn | ✅ |
| **5. Chất lượng nội dung** | | | | |
| 5.1 | Câu/đề nào quá khó hoặc quá dễ | tỉ lệ đúng theo câu | — | 🟡 cần log mức câu (hiện chỉ có tổng hợp học phần) |
| 5.2 | Mọi lớp đều yếu cùng 1 học phần? | học phần yếu nhất theo phạm vi | KPI + "Cần chú ý" | ✅ |
| **6. Việc cần làm** | | | | |
| 6.1 | Danh sách ưu tiên cho giáo viên | gộp: giảm điểm + ít chơi + Mức 1 | các lọc ở tab HS | 🟡 chưa có màn "việc cần làm" gộp |
| 6.2 | So sánh các kỳ / năm | nhiều `report.json` theo kỳ | — | ⬜ |
| 6.3 | Xuất PDF/phiếu A4 từng bé | phiếu `mau/*.html` đã có | — | 🟡 nối nút "In phiếu" sang `mau/` |
| 6.4 | Phân quyền: giáo viên chỉ thấy lớp mình | lọc ở API | xem INTEGRATION.md | 🟡 làm phía máy chủ |

**Ghi chú diễn giải**
- "Tiến bộ" luôn ghép cặp theo từng bé (tuần có chơi gần nhất − tuần có chơi đầu), không so TB lớp tuần này với tuần trước, vì sĩ số chơi mỗi tuần khác nhau.
- Điểm tuần dùng đúng công thức kỳ (80 × tỉ lệ đúng × hệ số tốc độ) nhưng ít câu hơn, nên dao động mạnh; đọc xu hướng qua ≥ 3 tuần.
- Dữ liệu thật hiện có 08–09/10, chỉ môn Toán (1 tuần) nên các màn tiến bộ chỉ hiện đầy đủ với dữ liệu minh hoạ cho tới khi tích luỹ ≥ 2 tuần.
