using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using MasterData;
using UnityEngine.Networking;

public class LoadCsvDataForEditor : Singleton<LoadCsvDataForEditor>
{
    // Cache for loaded CSV content (to avoid reloading)
    private Dictionary<string, string> _csvCache = new Dictionary<string, string>();
    private Dictionary<string, bool> _loadingCsv = new Dictionary<string, bool>();
#pragma warning disable CS0414
    private bool _isPreloadingCsv = false;  // flag preload — đọc khi cần guard async
#pragma warning restore CS0414
    /// <summary>
    /// Get the path to CSV file - works in both Editor and Build
    /// </summary>
    private string GetCsvPath(string csvFileName)
    {
#if UNITY_EDITOR
        // In Editor, read from Assets folder
        return "Assets/MasterData/Csv/" + csvFileName;
#else
        // In Build, read from StreamingAssets
        string streamingPath = Application.streamingAssetsPath;
        // Handle different platforms
        if (Application.platform == RuntimePlatform.Android)
        {
            // On Android, StreamingAssets is inside the APK, need to use WWW or UnityWebRequest
            // But for synchronous reading, we can use the path directly
            return Path.Combine(streamingPath, "MasterData", "Csv", csvFileName);
        }
        else
        {
            return Path.Combine(streamingPath, "MasterData", "Csv", csvFileName);
        }
#endif
    }

    /// <summary>
    /// Read CSV file content - works in both Editor and Build
    /// Uses StreamingAssets for build, Assets folder for Editor
    /// On Android, uses coroutine to load async (safer than blocking wait)
    /// </summary>
    private string ReadCsvFile(string csvFileName)
    {
#if UNITY_EDITOR
        // In Editor, read from Assets folder
        string csvPath = GetCsvPath(csvFileName);
        if (File.Exists(csvPath))
        {
            return File.ReadAllText(csvPath);
        }
        else
        {
            Debug.LogError($"CSV file not found: {csvPath}");
            return null;
        }
#else
        // In Build, read from StreamingAssets
        string streamingPath = GetCsvPath(csvFileName);
        
        // On Android, StreamingAssets is inside APK, CSV should be preloaded
        if (Application.platform == RuntimePlatform.Android)
        {
            // Check cache first
            if (_csvCache.ContainsKey(csvFileName))
            {
                return _csvCache[csvFileName];
            }
            
            // On Android, CSV should be preloaded before calling ReadCsvFile
            // If not in cache, it means preload hasn't completed or failed
            Debug.LogError($"CSV {csvFileName} not in cache. Make sure CSV files are preloaded before loading master data. Cache has {_csvCache.Count} files.");
            return null;
        }
        else
        {
            // Other platforms (iOS, Standalone) - can read directly
            if (File.Exists(streamingPath))
            {
                return File.ReadAllText(streamingPath);
            }
            else
            {
                Debug.LogError($"CSV file not found: {streamingPath}");
                return null;
            }
        }
#endif
    }

    /// <summary>
    /// Coroutine to load CSV from StreamingAssets on Android using UnityWebRequest
    /// </summary>
    private IEnumerator LoadCsvFromStreamingAssetsAndroid(string csvFileName)
    {
        string basePath = Application.streamingAssetsPath;
        string url;

        // Construct URL properly for Android
        if (basePath.StartsWith("jar:file://"))
        {
            url = basePath + "/MasterData/Csv/" + csvFileName;
        }
        else
        {
            url = Path.Combine(basePath, "MasterData", "Csv", csvFileName);
            if (!url.StartsWith("file://"))
            {
                url = "file://" + url;
            }
        }

        Debug.Log($"Loading CSV from Android: {url}");

        UnityWebRequest request = null;
        try
        {
            request = UnityWebRequest.Get(url);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Exception creating UnityWebRequest for CSV {csvFileName}: {ex.Message}\n{ex.StackTrace}");
            _loadingCsv[csvFileName] = false;
            yield break;
        }

        yield return request.SendWebRequest();

        try
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                string result = request.downloadHandler.text;
                if (string.IsNullOrEmpty(result))
                {
                    Debug.LogError($"CSV file is empty: {url}");
                    _loadingCsv[csvFileName] = false;
                    yield break;
                }
                Debug.Log($"Successfully loaded CSV: {csvFileName} ({result.Length} chars)");
                _csvCache[csvFileName] = result;
            }
            else
            {
                Debug.LogError($"Failed to load CSV from {url}: {request.error}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Exception processing CSV {csvFileName}: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            if (request != null)
            {
                request.Dispose();
            }
            _loadingCsv[csvFileName] = false;
        }
    }

    /// <summary>
    /// Preload all CSV files from StreamingAssets (Android only)
    /// Should be called before loading any master data
    /// </summary>
    public IEnumerator PreloadAllCsvFiles(string[] csvFileNames)
    {
#if !UNITY_EDITOR
        if (Application.platform == RuntimePlatform.Android)
        {
            _isPreloadingCsv = true;
            
            foreach (string csvFileName in csvFileNames)
            {
                if (!_csvCache.ContainsKey(csvFileName) && (!_loadingCsv.ContainsKey(csvFileName) || !_loadingCsv[csvFileName]))
                {
                    _loadingCsv[csvFileName] = true;
                    yield return StartCoroutine(LoadCsvFromStreamingAssetsAndroid(csvFileName));
                }
            }
            
            _isPreloadingCsv = false;
            Debug.Log($"Preloaded {_csvCache.Count} CSV files");
        }
        else
        {
            yield return null;
        }
#else
        yield return null;
#endif
    }

    public void LoadMasterData(string masterDataClassName)
    {
        if (masterDataClassName == "AddUpMaster")
        {
            LoadAddUpMaster();
        }
        else if (masterDataClassName == "TrainPathMaster")
        {
            LoadTrainPathMaster();
        }
        else
        {
            Debug.LogWarning($"LoadMasterData: {masterDataClassName} is not supported.");
        }
    }

    private void LoadAddUpMaster()
    {
        string csvContent = ReadCsvFile("add_up_master.csv");
        if (string.IsNullOrEmpty(csvContent))
        {
            Debug.LogError("Failed to load add_up_master.csv");
            return;
        }
        
        AddUpMaster data = ScriptableObject.CreateInstance<AddUpMaster>();
        using (StringReader sr = new StringReader(csvContent))
        {

            // ヘッダをやり過ごす
            sr.ReadLine();

            // ファイルの終端まで繰り返す
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                string[] dataStrs = line.Split(',');

                // 追加するパラメータを生成
                AddUpMaster.Param p = new AddUpMaster.Param();
                // 値を設定する
                p.question_id = int.Parse(dataStrs[0]);
                p.number_a = int.Parse(dataStrs[1]);
                p.number_b = int.Parse(dataStrs[2]);
                p.hidden_position = dataStrs[3].Trim();
                p.answer_1 = int.Parse(dataStrs[4]);
                p.answer_2 = int.Parse(dataStrs[5]);
                p.answer_3 = int.Parse(dataStrs[6]);
                p.correct_answer = int.Parse(dataStrs[7]);
                p.image_type = dataStrs[8].Trim();
                p.difficulty = int.Parse(dataStrs[9]);

                data.param.Add(p);
            }
        }

        MasterDataCache.CacheByName("AddUpMaster", data);

        AddUpMaster addUpMaster = MasterDataCache.GetCache<AddUpMaster>();
        foreach (AddUpMaster.Param param in addUpMaster.param)
        {
            Debug.Log("NDL: AddUpMaster param=" + param.question_id + ", A=" + param.number_a + ", B=" + param.number_b + ", hidden=" + param.hidden_position + ", correct=" + param.correct_answer);
        }
    }

    private void LoadTrainPathMaster()
    {
        string csvContent = ReadCsvFile("train_path_master.csv");
        if (string.IsNullOrEmpty(csvContent))
        {
            Debug.LogError("Failed to load train_path_master.csv");
            return;
        }

        TrainPathMaster data = ScriptableObject.CreateInstance<TrainPathMaster>();
        using (StringReader sr = new StringReader(csvContent))
        {
            // Skip header
            sr.ReadLine();

            string line;
            while ((line = sr.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] dataStrs = line.Split(',');

                TrainPathMaster.Param p = new TrainPathMaster.Param();
                p.template_id = int.Parse(dataStrs[0]);
                p.entry = dataStrs[1].ToString();
                p.path = dataStrs[2].ToString();
                p.exit = dataStrs[3].ToString();
                p.difficulty = int.Parse(dataStrs[4]);

                data.param.Add(p);
            }
        }

        MasterDataCache.CacheByName("TrainPathMaster", data);

        TrainPathMaster trainPathMaster = MasterDataCache.GetCache<TrainPathMaster>();
        foreach (TrainPathMaster.Param param in trainPathMaster.param)
        {
            Debug.Log("NDL: TrainPathMaster param=" + param.template_id + ", path=" + param.path + ", difficulty=" + param.difficulty);
        }
    }
}
