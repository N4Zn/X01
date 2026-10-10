# 0007 — FloorStory "flow": game dữ liệu chạy chung web builder và Unity

**Bối cảnh**: 15 game FloorStory là world C# viết tay; người không rành Unity không đổi được ảnh/cơ chế; web builder không chơi thử được chúng.
**Quyết định**: tách mỗi câu hỏi thành 7 lớp (nguồn → view → dòng thời gian → chạm/chấm → phản hồi → vars/scene) ghi bằng dữ liệu `meta.fsData` (zip `meta.kind="fsdata"`). Một engine JS (`WebTools/GenericGameBuilder/fs/`) chơi thử trên web; một engine C# (`FloorStoryGame/Flow/`) chạy cùng dữ liệu trong Unity. Biểu thức + RNG mulberry32 viết giống hệt ở 2 phía; tham số mặc định sinh từ `MOD` (`gen_mod_cs.py`). Không dùng thư viện JSON ngoài (`FsJson`).
**Hệ quả**: thêm cơ chế = sửa cả JS và C# (luật PHẢI khớp; kiểm bằng test parity cùng seed). Cả 16 game xuất được zip (`gen_games.py` là nguồn). Chưa chạy trong Unity Editor/K02. Chi tiết: `docs/floor-story-mechanics.md`.
