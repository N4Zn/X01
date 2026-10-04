using UnityEngine;
using UnityEditor;

/// <summary>
/// Helper tool to simulate UnitySendMessage calls from ControlActivity in the Editor.
/// </summary>
public class GameControlBridgeTester : EditorWindow
{
    private string _jsonPayload = "{\"musicVolume\":0.5,\"sfxVolume\":0.8,\"gameTime\":120,\"roundEndDelay\":3.0,\"flowSpeed\":2.5,\"waitForClear\":0}";

    [MenuItem("Window/EduXplore/GameControlBridge Tester")]
    public static void ShowWindow()
    {
        GetWindow<GameControlBridgeTester>("Bridge Tester");
    }

    private void OnGUI()
    {
        GUILayout.Label("Simulate ControlActivity -> Unity", EditorStyles.boldLabel);

        _jsonPayload = EditorGUILayout.TextArea(_jsonPayload, GUILayout.Height(100));

        if (GUILayout.Button("Trigger OnSettingsChanged"))
        {
            if (Application.isPlaying)
            {
                var bridge = GameObject.Find("GameControlBridge");
                if (bridge != null)
                {
                    bridge.GetComponent<GameControlBridge>().OnSettingsChanged(_jsonPayload);
                    Debug.Log("Simulated OnSettingsChanged sent.");
                }
                else
                {
                    Debug.LogError("GameControlBridge GameObject not found! Is the scene running?");
                }
            }
            else
            {
                Debug.LogWarning("Simulation only works in Play Mode.");
            }
        }

        if (GUILayout.Button("Reset to Default JSON"))
        {
            _jsonPayload = "{\"musicVolume\":0.5,\"sfxVolume\":0.8,\"gameTime\":120,\"roundEndDelay\":3.0,\"flowSpeed\":2.5,\"waitForClear\":0}";
        }
    }
}
