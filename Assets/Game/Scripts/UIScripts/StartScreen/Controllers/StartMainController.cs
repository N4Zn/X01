using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

using MasterData;

public class StartMainController : MonoBehaviour
{
    public bool useCsvDataDirect = true;

    public Text statusText;

    private string[] masterDataGroup = new string[]
    {
        // "BallMaster",
        // "BallRewardMaster",
        // "LevelMaster",
        // "LevelBallWeightMaster",
        // "UpgradeMaster",
        // "ConstantMaster",
        // "BossPathMaster",
        // "BossMaster",
        // "BonusCoinMaster",
        // "ChestDropMaster",
        // "SpecItemMaster",
        // "ThemeMaster",
        // "CannonMaster",
        // "ShopMaster",
        "AddUpMaster",
        "TrainPathMaster"
    };

    private void Awake()
    {
        // Warm up the face-recognition plugin (native init + USB camera permission handshake)
        // as early as possible. Touching .Instance creates + Start()s it right away; the CSV/
        // config loading below takes several real seconds on its own, giving the camera plenty
        // of overlapping time to connect before any mini-game's "Start in 3,2,1" ever needs it —
        // instead of only starting to warm up at that exact moment.
        _ = FaceRecognitionPlugin.Instance;

        // Same reasoning for Lidar (USB permission handshake + native pipeline startup) —
        // xem LidarTouchBridge.cs.
        _ = LidarTouchBridge.Instance;

        LoadData();
    }

    private void LoadData()
    {
        // Always use CSV mode (works in both Editor and Build)
        // CSV files should be copied to StreamingAssets before building
        StartCoroutine(MasterInitializeEditor());
    }

    public IEnumerator MasterInitializeEditor()
    {
#if !UNITY_EDITOR
        // On Android, preload all CSV files first
        if (Application.platform == RuntimePlatform.Android)
        {
            // Map master names to CSV file names
            string[] csvFileNames = new string[masterDataGroup.Length];
            for (int i = 0; i < masterDataGroup.Length; i++)
            {
                // Convert master name to CSV file name
                // "AddUpMaster" -> "add_up_master.csv"
                // "TrainPathMaster" -> "train_path_master.csv"
                string masterName = masterDataGroup[i];
                string csvName = ConvertMasterNameToCsvFileName(masterName);
                csvFileNames[i] = csvName;
            }
            
            Debug.Log("NDL: Preloading CSV files on Android...");
            yield return LoadCsvDataForEditor.Instance.PreloadAllCsvFiles(csvFileNames);
            Debug.Log("NDL: CSV files preloaded");
        }
#endif
        
        foreach (string masterName in masterDataGroup)
        {
            Debug.Log("NDL:3 masterName=" + masterName);
            LoadCsvDataForEditor.Instance.LoadMasterData(masterName);
            yield return new WaitForSeconds(0.1f); // Small delay between each load
        }
        
        // Wait a bit more to ensure all data is cached
        yield return new WaitForSeconds(1.0f);

        // Load TestTongHop runtime config (JSON editable on-device)
        Debug.Log("NDL: Loading TongHopConfig...");
        yield return TongHopConfig.Load();
        Debug.Log("NDL: TongHopConfig loaded");

        // Preload audio asset overrides từ persistentDataPath (nếu có)
        Debug.Log("NDL: Preloading asset overrides...");
        yield return AssetOverrideLoader.PreloadAudio();
        Debug.Log("NDL: Asset overrides ready");

        // Verify AddUpMaster is loaded
        try
        {
            AddUpMaster addUpMaster = MasterDataCache.GetCache<AddUpMaster>();
            if (addUpMaster != null && addUpMaster.param != null)
            {
                Debug.Log($"NDL: AddUpMaster verified - {addUpMaster.param.Count} questions loaded");
            }
            else
            {
                Debug.LogWarning("NDL: AddUpMaster verification failed, but continuing...");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"NDL: AddUpMaster verification error: {ex.Message}");
        }
        
        // EduXplore 2.0: skip HomeScene, go straight to game selection
        SceneManager.LoadScene("MenuScene");
        yield return null;
    }

    /// <summary>
    /// Convert master name to CSV file name
    /// </summary>
    private string ConvertMasterNameToCsvFileName(string masterName)
    {
        // Remove "Master" suffix
        string name = masterName.Replace("Master", "");
        
        // Convert CamelCase to snake_case
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0)
            {
                sb.Append('_');
            }
            sb.Append(char.ToLower(name[i]));
        }
        
        return sb.ToString() + "_master.csv";
    }
}
