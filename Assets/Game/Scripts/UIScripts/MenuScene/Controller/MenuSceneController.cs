using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuSceneController : MonoBehaviour
{
    //NamNN edit with AS Agent on 04/05/2026 15:20
    [SerializeField] private MenuSceneView menuSceneView;
    protected CustomFSMManager _customFSMManager;

    private int _currentCategory = 0;
    private int _selectedGameIndex = -1; // single selected game in grid (0-9)
    private HashSet<int> _selectedGames = new HashSet<int>();

    void Start()
    {
        //NamNN edit with AS Agent on 04/05/2026 15:20
        menuSceneView.InitView();

        _customFSMManager = gameObject.AddComponent<CustomFSMManager>();
        _customFSMManager.fsmName = this.GetType().Name + "FSM";
        _customFSMManager.Initialize(typeof(MenuSceneState), this.GetType(), false);
        _customFSMManager.StateMachineChange(MenuSceneState.Initialize);

        // Navigation
        menuSceneView.onClickBack += OnClickBack;
        menuSceneView.onClickHome += OnClickHome;
        menuSceneView.onClickSettings += OnClickSettings;
        menuSceneView.onClickStart += OnClickStart;

        // Setting panel
        menuSceneView.onSettingClose          += OnSettingClose;
        menuSceneView.onSfxVolumeChanged      += OnSfxVolumeChanged;
        menuSceneView.onMusicVolumeChanged    += OnMusicVolumeChanged;
        menuSceneView.onSfxMuteToggle         += OnSfxMuteToggle;
        menuSceneView.onMusicMuteToggle       += OnMusicMuteToggle;
        menuSceneView.onGameTimeSelected      += OnGameTimeSelected;
        menuSceneView.onFeedbackSpeedSelected += OnFeedbackSpeedSelected;
        menuSceneView.onQuestionTimeoutSelected += OnQuestionTimeoutSelected;

        // Category & game
        menuSceneView.onCategorySelected += OnCategorySelected;
        menuSceneView.onGameSelected += OnGameSelected;
        menuSceneView.onClickRandomSelect += OnClickRandomSelect;
    }

    // ===== FSM States =====

    protected void StateMachineEnter_Initialize(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: MenuScene - StateMachineEnter_Initialize");

        // Load team info and avatars from session
        var session = GameSessionManager.Instance;
        menuSceneView.UpdateTeamInfo(
            session.BlueTeamName ?? "Blue",
            session.RedTeamName ?? "Red"
        );
        menuSceneView.UpdateBlueTeamAvatars(session.BlueTeamPlayers);
        menuSceneView.UpdateRedTeamAvatars(session.RedTeamPlayers);

        // Default to first category
        _currentCategory = 0;
        menuSceneView.SetActiveCategory(_currentCategory);
        menuSceneView.UpdateGrid(_currentCategory);
        menuSceneView.SetStartButtonEnabled(false);

        _customFSMManager.StateMachineChange(MenuSceneState.GameSelect);
    }

    protected void StateMachineExit_Initialize(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: MenuScene - StateMachineExit_Initialize");
    }

    protected void StateMachineEnter_GameSelect(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: MenuScene - StateMachineEnter_GameSelect");
    }

    protected void StateMachineExit_GameSelect(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: MenuScene - StateMachineExit_GameSelect");
    }

    // ===== Navigation =====

    private void OnClickBack()
    {
        Debug.Log("NDL: MenuScene - OnClickBack - Loading TeamSelectScene");
        SceneManager.LoadScene("TeamSelectScene");
    }

    private void OnClickHome()
    {
        Debug.Log("NDL: MenuScene - OnClickHome - Loading HomeScene");
        SceneManager.LoadScene("MenuScene");
    }

    private void OnClickSettings()
    {
        Debug.Log("NDL: MenuScene - OnClickSettings");
        var gs = GameSettings.Instance;
        menuSceneView.UpdateSettingUI(gs.SfxVolume, gs.MusicVolume, gs.GameTime, gs.FeedbackSpeedSetting, gs.QuestionTimeout);
        menuSceneView.ShowSettingPanel();
    }

    // ===== Setting Panel Handlers =====

    private void OnSettingClose()
    {
        GameSettings.Instance.Save();
        menuSceneView.HideSettingPanel();
    }

    private void OnSfxVolumeChanged(float volume)
    {
        GameSettings.Instance.SfxVolume = volume;
        menuSceneView.UpdateSfxMuteLabel(volume <= 0f);
        MusicManager.Instance.ApplyVolumes();
    }

    private void OnMusicVolumeChanged(float volume)
    {
        GameSettings.Instance.MusicVolume = volume;
        menuSceneView.UpdateMusicMuteLabel(volume <= 0f);
        MusicManager.Instance.ApplyVolumes();
    }

    private void OnSfxMuteToggle()
    {
        var gs = GameSettings.Instance;
        gs.SfxVolume = gs.SfxVolume > 0f ? 0f : 1f;
        menuSceneView.UpdateSfxMuteLabel(gs.SfxVolume <= 0f);
        menuSceneView.UpdateSettingUI(gs.SfxVolume, gs.MusicVolume, gs.GameTime, gs.FeedbackSpeedSetting, gs.QuestionTimeout);
        MusicManager.Instance.ApplyVolumes();
    }

    private void OnMusicMuteToggle()
    {
        var gs = GameSettings.Instance;
        gs.MusicVolume = gs.MusicVolume > 0f ? 0f : 1f;
        menuSceneView.UpdateMusicMuteLabel(gs.MusicVolume <= 0f);
        menuSceneView.UpdateSettingUI(gs.SfxVolume, gs.MusicVolume, gs.GameTime, gs.FeedbackSpeedSetting, gs.QuestionTimeout);
        MusicManager.Instance.ApplyVolumes();
    }

    private void OnGameTimeSelected(int seconds)
    {
        GameSettings.Instance.GameTime = seconds;
        menuSceneView.SetTimeHighlight(seconds);
    }

    private void OnFeedbackSpeedSelected(FeedbackSpeed speed)
    {
        GameSettings.Instance.FeedbackSpeedSetting = speed;
        menuSceneView.SetFeedbackHighlight(speed);
    }

    private void OnQuestionTimeoutSelected(int seconds)
    {
        GameSettings.Instance.QuestionTimeout = seconds;
        menuSceneView.SetQuestionTimeoutHighlight(seconds);
    }

    // ===== Category =====

    private void OnCategorySelected(int categoryIndex)
    {
        //NamNN edit with AS Agent on 04/05/2026 15:20
        Debug.Log("NDL: MenuScene - OnCategorySelected: " + categoryIndex);
        _currentCategory = categoryIndex;
        menuSceneView.SetActiveCategory(categoryIndex);
        menuSceneView.UpdateGrid(categoryIndex);

        // Reset selection when changing tabs
        _selectedGames.Clear();
        _selectedGameIndex = -1;
        menuSceneView.SetStartButtonEnabled(false);
    }

    // ===== Game Selection =====

    private void OnGameSelected(int gridIndex)
    {
        // gridIndex is the index in the 18-button array
        if (gridIndex < 0 || gridIndex >= MenuSceneView.MAX_GAMES_PER_PAGE) return;

        string sceneName = MenuSceneView.GameSceneNames[_currentCategory, gridIndex];
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.Log("NDL: MenuScene - Game not implemented: category=" + _currentCategory + " grid=" + gridIndex);
            return;
        }

        // Toggle selection
        if (_selectedGames.Contains(gridIndex))
        {
            _selectedGames.Remove(gridIndex);
            menuSceneView.SetSingleGameHighlight(gridIndex, false);
        }
        else
        {
            // Single selection mode: clear previous and select new
            _selectedGames.Clear();
            _selectedGames.Add(gridIndex);
            menuSceneView.SetGameHighlights(_selectedGames);
        }

        _selectedGameIndex = _selectedGames.Count > 0 ? gridIndex : -1;
        menuSceneView.SetStartButtonEnabled(_selectedGames.Count > 0);

        Debug.Log("NDL: MenuScene - GameSelected: " + gridIndex + " (" + MenuSceneView.GameNames[_currentCategory, gridIndex] + ")");
    }

    // ===== Random Selection =====

    private void OnClickRandomSelect()
    {
        Debug.Log("NDL: MenuScene - RandomSelect clicked");

        _selectedGames.Clear();
        List<int> implementedGames = new List<int>();

        // Find all implemented games in current category
        for (int i = 0; i < MenuSceneView.MAX_GAMES_PER_PAGE; i++)
        {
            if (!string.IsNullOrEmpty(MenuSceneView.GameSceneNames[_currentCategory, i]))
            {
                implementedGames.Add(i);
            }
        }

        if (implementedGames.Count == 0) return;

        // Pick one random game
        int randomIdx = UnityEngine.Random.Range(0, implementedGames.Count);
        int picked = implementedGames[randomIdx];
        _selectedGames.Add(picked);
        _selectedGameIndex = picked;

        menuSceneView.SetGameHighlights(_selectedGames);
        menuSceneView.SetStartButtonEnabled(true);

        Debug.Log("NDL: MenuScene - RandomSelected: " + MenuSceneView.GameNames[_currentCategory, picked]);
    }

    // ===== Start =====

    private void OnClickStart()
    {
        if (_selectedGames.Count == 0) return;

        int gridIndex = -1;
        foreach (int idx in _selectedGames)
        {
            gridIndex = idx;
            break;
        }

        string sceneName = MenuSceneView.GameSceneNames[_currentCategory, gridIndex];

        if (sceneName == null)
        {
            Debug.LogWarning("NDL: MenuScene - Selected game has no scene");
            return;
        }

        Debug.Log("NDL: MenuScene - Starting game: " + sceneName);
        GameSessionManager.Instance.LastPlayedGame = sceneName;
        SceneManager.LoadScene(sceneName);
    }
}
