using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HomeSceneController : MonoBehaviour
{
    [SerializeField] private HomeSceneView homeSceneView;
    protected CustomFSMManager _customFSMManager;

    void Start()
    {
        _customFSMManager = gameObject.AddComponent<CustomFSMManager>();
        _customFSMManager.fsmName = this.GetType().Name + "FSM";
        _customFSMManager.Initialize(typeof(HomeSceneState), this.GetType(), false);
        _customFSMManager.StateMachineChange(HomeSceneState.Initialize);

        homeSceneView.InitView();

        // Main buttons
        homeSceneView.onClickStart += OnClickStart;
        homeSceneView.onClickSettings += OnClickSettings;

        // Setting panel
        homeSceneView.onSettingClose += OnSettingClose;
        homeSceneView.onSfxVolumeChanged += OnSfxVolumeChanged;
        homeSceneView.onMusicVolumeChanged += OnMusicVolumeChanged;
        homeSceneView.onSfxMuteToggle += OnSfxMuteToggle;
        homeSceneView.onMusicMuteToggle += OnMusicMuteToggle;
        homeSceneView.onGameTimeSelected += OnGameTimeSelected;
        homeSceneView.onFeedbackSpeedSelected += OnFeedbackSpeedSelected;
        homeSceneView.onQuestionTimeoutSelected += OnQuestionTimeoutSelected;
    }

    // ===== FSM States =====

    protected void StateMachineEnter_Initialize(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: HomeScene - StateMachineEnter_Initialize");
        _customFSMManager.StateMachineChange(HomeSceneState.Idle);
    }

    protected void StateMachineExit_Initialize(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: HomeScene - StateMachineExit_Initialize");
    }

    protected void StateMachineEnter_Idle(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: HomeScene - StateMachineEnter_Idle");
        MusicManager.Instance.PlayMainMusic();
    }

    protected void StateMachineExit_Idle(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: HomeScene - StateMachineExit_Idle");
    }

    // ===== Main Buttons =====

    private void OnClickStart()
    {
        Debug.Log("NDL: HomeScene - OnClickStart - Loading TeamSelectScene");
        SceneManager.LoadScene("TeamSelectScene");
    }

    private void OnClickSettings()
    {
        Debug.Log("NDL: HomeScene - OnClickSettings");
        var gs = GameSettings.Instance;
        homeSceneView.UpdateSettingUI(gs.SfxVolume, gs.MusicVolume, gs.GameTime, gs.FeedbackSpeedSetting, gs.QuestionTimeout);
        homeSceneView.ShowSettingPanel();
    }

    // ===== Setting Panel =====

    private void OnSettingClose()
    {
        GameSettings.Instance.Save();
        homeSceneView.HideSettingPanel();
    }

    private void OnSfxVolumeChanged(float volume)
    {
        GameSettings.Instance.SfxVolume = volume;
        homeSceneView.UpdateSfxMuteLabel(volume <= 0f);
        MusicManager.Instance.ApplyVolumes();
    }

    private void OnMusicVolumeChanged(float volume)
    {
        GameSettings.Instance.MusicVolume = volume;
        homeSceneView.UpdateMusicMuteLabel(volume <= 0f);
        MusicManager.Instance.ApplyVolumes();
    }

    private void OnSfxMuteToggle()
    {
        var gs = GameSettings.Instance;
        gs.SfxVolume = gs.SfxVolume > 0f ? 0f : 1f;
        homeSceneView.UpdateSfxMuteLabel(gs.SfxVolume <= 0f);
        MusicManager.Instance.ApplyVolumes();
        if (homeSceneView != null)
        {
            homeSceneView.UpdateSettingUI(gs.SfxVolume, gs.MusicVolume, gs.GameTime, gs.FeedbackSpeedSetting, gs.QuestionTimeout);
        }
    }

    private void OnMusicMuteToggle()
    {
        var gs = GameSettings.Instance;
        gs.MusicVolume = gs.MusicVolume > 0f ? 0f : 1f;
        homeSceneView.UpdateMusicMuteLabel(gs.MusicVolume <= 0f);
        MusicManager.Instance.ApplyVolumes();
        if (homeSceneView != null)
        {
            homeSceneView.UpdateSettingUI(gs.SfxVolume, gs.MusicVolume, gs.GameTime, gs.FeedbackSpeedSetting, gs.QuestionTimeout);
        }
    }

    private void OnGameTimeSelected(int seconds)
    {
        GameSettings.Instance.GameTime = seconds;
        homeSceneView.SetTimeHighlight(seconds);
    }

    private void OnFeedbackSpeedSelected(FeedbackSpeed speed)
    {
        GameSettings.Instance.FeedbackSpeedSetting = speed;
        homeSceneView.SetFeedbackHighlight(speed);
    }

    private void OnQuestionTimeoutSelected(int seconds)
    {
        GameSettings.Instance.QuestionTimeout = seconds;
        homeSceneView.SetQuestionTimeoutHighlight(seconds);
    }
}
