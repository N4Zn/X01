package com.eduxplore.control.ui;

import android.util.JsonReader;
import android.util.JsonToken;
import android.util.Log;

import java.io.File;
import java.io.FileInputStream;
import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;

/** Danh sách lớp + học sinh THẬT do "Quản lý lớp" (FaceEnrollAndroidLib/AttendanceStore.kt) ghi ở
 *  /sdcard/EduXplore/ — thay cho MockData cũ:
 *   • classes.json  — mảng tên lớp (gồm cả lớp rỗng chưa có ai).
 *   • enrolled.json — mảng người đã đăng ký; chỉ đọc name/alias/className, BỎ QUA mảng samples (embedding
 *     rất nặng) bằng JsonReader streaming nên không phải dựng cả file vào bộ nhớ.
 *  Chỉ đọc, không bao giờ ghi 2 file này. Gọi load() ở luồng nền. */
public final class ClassRepo {
    private static final String TAG = "ClassRepo";
    private static final String DIR = "/sdcard/EduXplore";

    private ClassRepo() {}

    public static final class Student {
        /** Khoá nhận diện (tên thật) — cùng khoá Unity gửi lên trong breakdown người chơi. */
        public final String name;
        /** Tên thường gọi hiện trên UI (alias nếu có, không thì tên thật — giống AttendanceStore.aliasOf). */
        public final String display;
        public final String className;
        Student(String name, String alias, String className) {
            this.name = name;
            this.display = alias != null && !alias.trim().isEmpty() ? alias.trim() : name;
            this.className = className;
        }
    }

    public static final class Snapshot {
        public final List<String> classes = new ArrayList<>();
        public final List<Student> students = new ArrayList<>();

        public List<Student> inClass(String className) {
            List<Student> out = new ArrayList<>();
            if (className == null) return out;
            for (Student s : students) if (className.equals(s.className)) out.add(s);
            return out;
        }
    }

    public static Snapshot load() {
        Snapshot snap = new Snapshot();
        Set<String> classSet = new LinkedHashSet<>();

        File classesFile = new File(DIR, "classes.json");
        if (classesFile.exists()) {
            try (JsonReader r = new JsonReader(new InputStreamReader(new FileInputStream(classesFile), StandardCharsets.UTF_8))) {
                r.beginArray();
                while (r.hasNext()) {
                    if (r.peek() == JsonToken.STRING) classSet.add(r.nextString()); else r.skipValue();
                }
                r.endArray();
            } catch (Exception e) {
                Log.e(TAG, "đọc classes.json lỗi: " + e);
            }
        }

        File enrolledFile = new File(DIR, "enrolled.json");
        if (enrolledFile.exists()) {
            try (JsonReader r = new JsonReader(new InputStreamReader(new FileInputStream(enrolledFile), StandardCharsets.UTF_8))) {
                r.beginArray();
                while (r.hasNext()) {
                    String name = null, alias = null, cls = null;
                    r.beginObject();
                    while (r.hasNext()) {
                        String key = r.nextName();
                        if (r.peek() == JsonToken.NULL) { r.skipValue(); continue; }
                        if ("name".equals(key)) name = r.nextString();
                        else if ("alias".equals(key)) alias = r.nextString();
                        else if ("className".equals(key)) cls = r.nextString();
                        else r.skipValue(); // samples/embedding/gender/birthdate...
                    }
                    r.endObject();
                    if (name == null) continue;
                    snap.students.add(new Student(name, alias, cls));
                    if (cls != null && !cls.isEmpty()) classSet.add(cls);
                }
                r.endArray();
            } catch (Exception e) {
                Log.e(TAG, "đọc enrolled.json lỗi: " + e);
            }
        }

        snap.classes.addAll(classSet);
        return snap;
    }
}
