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
        
        SceneManager.LoadScene("HomeScene");
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
