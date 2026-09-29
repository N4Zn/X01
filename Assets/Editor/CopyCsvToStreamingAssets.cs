using System.IO;
using UnityEditor;
using UnityEngine;

public static class CopyCsvToStreamingAssets
{
    [MenuItem("Tools/MasterData/Copy CSV to StreamingAssets")]
    public static void CopyCsvFiles()
    {
        string sourceDir = "Assets/MasterData/Csv";
        string targetDir = "Assets/StreamingAssets/MasterData/Csv";
        
        // Ensure target directory exists
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }
        
        // Get all CSV files
        string[] csvFiles = Directory.GetFiles(sourceDir, "*.csv");
        
        int copiedCount = 0;
        foreach (string csvFile in csvFiles)
        {
            string fileName = Path.GetFileName(csvFile);
            string targetFile = Path.Combine(targetDir, fileName);
            
            // Copy file
            File.Copy(csvFile, targetFile, true);
            copiedCount++;
            Debug.Log($"Copied: {fileName} -> {targetFile}");
        }
        
        AssetDatabase.Refresh();
        Debug.Log($"CopyCsvToStreamingAssets: Copied {copiedCount} CSV files to StreamingAssets");
    }
}
