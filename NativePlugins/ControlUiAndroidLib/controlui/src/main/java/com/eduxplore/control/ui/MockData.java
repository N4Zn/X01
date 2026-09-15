package com.eduxplore.control.ui;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** Dữ liệu MẪU (mock) cho roster/năng lực học sinh — chưa có nguồn thật (chưa thiết kế công
 *  thức tính điểm năng lực + chưa có hệ thống lớp/roster, xem CLAUDE.md/lịch sử trao đổi).
 *  Danh sách môn học + game thật thì lấy từ game_registry.json (GameRegistry.loadGameRegistry),
 *  KHÔNG mock ở đây — chỉ roster/điểm/lịch sử chơi là giả, để xem trước giao diện trên tablet
 *  thật trước khi có dữ liệu thật. */
public final class MockData {
    private MockData() {}

    public static class ClassDef {
        public final String key;
        public final String label;
        public final String[] names;
        public ClassDef(String key, String label, String[] names) { this.key = key; this.label = label; this.names = names; }
    }

    public static class Student {
        public final String name;
        public final String avatar;
        public final Map<Integer, Integer> scoresByCategory = new LinkedHashMap<>(); // category index -> 0..100
        public final Map<String, Integer> playsByGame = new LinkedHashMap<>();       // game name -> số lượt
        public String flag; // "slow-sure" | "fast-careless" | null
        public boolean played;
        public Student(String name, String avatar) { this.name = name; this.avatar = avatar; }

        public int total(int categoryCount) {
            int sum = 0;
            for (int c = 0; c < categoryCount; c++) sum += scoreOr0(c);
            return Math.round(sum / (float) categoryCount);
        }
        public int scoreOr0(int cat) { Integer v = scoresByCategory.get(cat); return v == null ? 0 : v; }
        public int weakestCategory(int categoryCount) {
            int min = 0;
            for (int c = 1; c < categoryCount; c++) if (scoreOr0(c) < scoreOr0(min)) min = c;
            return min;
        }
    }

    public static class ClassData {
        public final String label;
        public final List<Student> playedStudents = new ArrayList<>();
        public final List<String> notPlayedNames = new ArrayList<>();
        public ClassData(String label) { this.label = label; }
    }

    private static final ClassDef[] CLASS_DEFS = {
        new ClassDef("c1a", "Lớp 1A", new String[]{"Minh An","Bảo Châu","Gia Hân","Khánh Vy","Đức Anh","Thảo My","Nam Khang","Bảo Ngọc","Tuấn Kiệt","Hà Vi"}),
        new ClassDef("c1b", "Lớp 1B", new String[]{"Quang Huy","Ngọc Linh","Anh Thư","Bảo Long","Chí Bảo","Diệu Linh","Gia Bảo","Hải Đăng","Hoài An","Khôi Nguyên"}),
        new ClassDef("c2a", "Lớp 2A", new String[]{"Lan Anh","Minh Khang","Ngọc Ánh","Phương Anh","Quốc Bảo","Thanh Trúc","Thiên Ân","Tuệ Lâm","Việt Hoàng","Xuân Mai"}),
    };

    public static ClassDef[] classDefs() { return CLASS_DEFS; }

    private static double seededRand(double seed) {
        double x = Math.sin(seed) * 10000;
        return x - Math.floor(x);
    }
    private static String initialsOf(String name) {
        String[] parts = name.split(" ");
        StringBuilder sb = new StringBuilder();
        for (int i = Math.max(0, parts.length - 2); i < parts.length; i++) {
            if (!parts[i].isEmpty()) sb.append(Character.toUpperCase(parts[i].charAt(0)));
        }
        return sb.toString();
    }

    private static final Map<String, ClassData> CLASSES = new LinkedHashMap<>();

    /** Sinh dữ liệu 1 lần cho tất cả lớp — cần biết categoryCount + toàn bộ tên game hiện có
     *  (đọc từ game_registry.json thật) để gán số lượt chơi mock hợp lý cho từng game. */
    public static void generate(int categoryCount, List<String> allGameNames) {
        if (!CLASSES.isEmpty()) return; // chỉ sinh 1 lần
        for (int ci = 0; ci < CLASS_DEFS.length; ci++) {
            ClassDef cd = CLASS_DEFS[ci];
            ClassData data = new ClassData(cd.label);
            for (int i = 0; i < cd.names.length; i++) {
                String name = cd.names[i];
                boolean played = (ci * 7 + i) % 5 != 4;
                Student s = new Student(name, initialsOf(name));
                s.played = played;
                for (int c = 0; c < categoryCount; c++) {
                    int v = (int) Math.round(35 + seededRand(ci * 97 + i * 37 + c * 13 + 7) * 60);
                    s.scoresByCategory.put(c, v);
                }
                if (played) {
                    for (String gameName : allGameNames) {
                        double r = seededRand(ci * 131 + i * 29 + gameName.length() * 11);
                        if (r > 0.45) s.playsByGame.put(gameName, 1 + (int) Math.floor(r * 4));
                    }
                }
                if (i == 2) s.flag = "fast-careless";
                if (i == 5) s.flag = "slow-sure";
                if (played) data.playedStudents.add(s); else data.notPlayedNames.add(name);
            }
            CLASSES.put(cd.key, data);
        }
    }

    public static ClassData classData(String key) { return CLASSES.get(key); }
}
