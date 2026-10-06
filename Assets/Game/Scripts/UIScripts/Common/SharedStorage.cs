using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Thư mục dữ liệu LÂU DÀI của app: /sdcard/EduXplore (cùng nơi ClassRepo/ScoreStore/AttendanceStore
/// ghi lớp, khuôn mặt, điểm). Thư mục riêng của app (Application.persistentDataPath =
/// /sdcard/Android/data/&lt;pkg&gt;/files) bị Android XOÁ khi gỡ/cài lại app → lịch sử chơi, calib
/// Lidar, cấu hình Sheets mất mỗi lần cài bản mới. Mọi file cần sống qua lần cài lại phải đi qua đây.
///
/// Lần đầu truy cập: tự COPY (không xoá nguồn) dữ liệu cũ từ persistentDataPath sang đây nếu
/// đích chưa có. Không ghi được /sdcard (chưa cấp quyền…) thì rơi về persistentDataPath như cũ.
/// Editor/PC: dùng persistentDataPath.
/// </summary>
public static class SharedStorage
{
    const string AndroidDir = "/sdcard/EduXplore";

    // File riêng lẻ cần giữ qua lần cài lại (tên nằm ngay gốc persistentDataPath).
    static readonly string[] MigrateFiles =
    {
        "lidar_config.json", "interaction_area_calib.json", "sheets_sync_config.json",
        "frtest_config.json", "game_data.json", "attendance_log.csv",
    };

    static string _dir;
    static bool _migrated;

    /// <summary>Thư mục gốc dữ liệu lâu dài (đã tạo, đã migrate lần đầu).</summary>
    public static string Dir
    {
        get
        {
            if (_dir == null) Init();
            return _dir;
        }
    }

    /// <summary>Lịch sử chơi (GameLog_*.json, FRTest_*.json…).</summary>
    public static string LogsDir
    {
        get
        {
            string p = Path.Combine(Dir, "GameLogs");
            try { Directory.CreateDirectory(p); } catch { }
            return p;
        }
    }

    /// <summary>Đường dẫn 1 file trong thư mục lâu dài.</summary>
    public static string PathFor(string fileName) => Path.Combine(Dir, fileName);

    static void Init()
    {
        _dir = Application.persistentDataPath;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            Directory.CreateDirectory(AndroidDir);
            // Thử ghi thật: có thư mục chưa chắc ghi được (quyền storage).
            string probe = Path.Combine(AndroidDir, ".write_probe");
            File.WriteAllText(probe, "1");
            File.Delete(probe);
            _dir = AndroidDir;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SharedStorage] {AndroidDir} không ghi được ({e.Message}) — dùng persistentDataPath, dữ liệu sẽ mất khi cài lại.");
            return;
        }
        MigrateOldData();
#endif
    }

    static void MigrateOldData()
    {
        if (_migrated) return;
        _migrated = true;
        string old = Application.persistentDataPath;
        if (string.IsNullOrEmpty(old) || old == _dir || !Directory.Exists(old)) return;
        try
        {
            int n = 0;
            foreach (var f in MigrateFiles)
                n += CopyIfMissing(Path.Combine(old, f), Path.Combine(_dir, f));

            // Lịch sử chơi cũ: GameLogs/ + các file session nằm thẳng ở gốc (GameLog_*, FRTest_*, TestTongHop_*).
            string logsDst = Path.Combine(_dir, "GameLogs");
            Directory.CreateDirectory(logsDst);
            string logsSrc = Path.Combine(old, "GameLogs");
            if (Directory.Exists(logsSrc))
                foreach (var f in Directory.GetFiles(logsSrc, "*.json"))
                    n += CopyIfMissing(f, Path.Combine(logsDst, Path.GetFileName(f)));
            foreach (var pat in new[] { "GameLog_*.json", "FRTest_*.json", "TestTongHop_*.json" })
                foreach (var f in Directory.GetFiles(old, pat))
                    n += CopyIfMissing(f, Path.Combine(logsDst, Path.GetFileName(f)));

            if (n > 0) Debug.Log($"[SharedStorage] Đã chép {n} file dữ liệu cũ sang {_dir}");
        }
        catch (Exception e) { Debug.LogWarning($"[SharedStorage] Migrate lỗi: {e.Message}"); }
    }

    static int CopyIfMissing(string src, string dst)
    {
        try
        {
            if (!File.Exists(src) || File.Exists(dst)) return 0;
            File.Copy(src, dst);
            return 1;
        }
        catch { return 0; }
    }
}
