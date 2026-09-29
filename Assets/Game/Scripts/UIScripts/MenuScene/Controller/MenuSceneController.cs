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
        menuSceneView.onRoundDelaySelected    += OnRoundDelaySelected;
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
        // MenuScene là màn hình gốc — Back không làm gì
        Debug.Log("NDL: MenuScene - OnClickBack - (MenuScene is home, ignored)");
    }

    private void OnClickHome()
    {
        Debug.Log("NDL: MenuScene - OnClickHome - Reloading MenuScene");
        SceneManager.LoadScene("MenuScene");
    }

    private void OnClickSettings()
    {
        Debug.Log("NDL: MenuScene - OnClickSettings");
        var gs = GameSettings.Instance;
        menuSceneView.UpdateSettingUI(gs.SfxVolume, gs.MusicVolume, gs.GameTime, gs.RoundEndDelay, gs.QuestionTimeout);
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
        MusicManager.Instance?.ApplyVolumes();
    }

    private void OnMusicVolumeChanged(float volume)
    {
        GameSettings.Instance.MusicVolume = volume;
        menuSceneView.UpdateMusicMuteLabel(volume <= 0f);
        MusicManager.Instance?.ApplyVolumes();
    }

    private void OnSfxMuteToggle()
    {
        var gs = GameSettings.Instance;
        gs.SfxVolume = gs.SfxVolume > 0f ? 0f : 1f;
        menuSceneView.UpdateSfxMuteLabel(gs.SfxVolume <= 0f);
        menuSceneView.UpdateSettingUI(gs.SfxVolume, gs.MusicVolume, gs.GameTime, gs.RoundEndDelay, gs.QuestionTimeout);
        MusicManager.Instance?.ApplyVolumes();
    }

    private void OnMusicMuteToggle()
    {
        var gs = GameSettings.Instance;
        gs.MusicVolume = gs.MusicVolume > 0f ? 0f : 1f;
        menuSceneView.UpdateMusicMuteLabel(gs.MusicVolume <= 0f);
        menuSceneView.UpdateSettingUI(gs.SfxVolume, gs.MusicVolume, gs.GameTime, gs.RoundEndDelay, gs.QuestionTimeout);
        MusicManager.Instance?.ApplyVolumes();
    }

    private void OnGameTimeSelected(int seconds)
    {
        GameSettings.Instance.GameTime = seconds;
        menuSceneView.SetTimeHighlight(seconds);
    }

    private void OnRoundDelaySelected(float seconds)
    {
        float clamped = Mathf.Clamp(seconds, 1f, 4f);
        GameSettings.Instance.RoundEndDelay = clamped;
        menuSceneView.SetRoundDelayText(clamped);
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
        if (gridIndex < 0 || gridIndex >= GameRegistry.MAX_PER_CATEGORY) return;

        var entry = GameRegistry.Games[_currentCategory, gridIndex];
        if (!entry.IsImplemented)
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

        Debug.Log("NDL: MenuScene - GameSelected: " + gridIndex + " (" + GameRegistry.Games[_currentCategory, gridIndex].name + ")");
    }

    // ===== Random Selection =====

    private void OnClickRandomSelect()
    {
        Debug.Log("NDL: MenuScene - RandomSelect clicked");

        _selectedGames.Clear();
        List<int> implementedGames = new List<int>();

        // Find all implemented games in current category
        for (int i = 0; i < GameRegistry.MAX_PER_CATEGORY; i++)
        {
            if (GameRegistry.Games[_currentCategory, i].IsImplemented)
                implementedGames.Add(i);
        }

        if (implementedGames.Count == 0) return;

        // Pick one random game
        int randomIdx = UnityEngine.Random.Range(0, implementedGames.Count);
        int picked = implementedGames[randomIdx];
        _selectedGames.Add(picked);
        _selectedGameIndex = picked;

        menuSceneView.SetGameHighlights(_selectedGames);
        menuSceneView.SetStartButtonEnabled(true);

        Debug.Log("NDL: MenuScene - RandomSelected: " + GameRegistry.Games[_currentCategory, picked].name);
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

        var selected = GameRegistry.Games[_currentCategory, gridIndex];

        if (!selected.IsImplemented)
        {
            Debug.LogWarning("NDL: MenuScene - Selected game has no scene");
            return;
        }

        // Lưu tên game (variant) để game scene biết load CSV nào
        // VD: "ChuCai", "SoDem" → QuestionPool load Resources/TongHop/{name}/
        GameSessionManager.Instance.SelectedGameName = selected.name;
        GameSessionManager.Instance.SelectedEntry    = selected;
        GameSessionManager.Instance.LastPlayedGame = selected.sceneName;

        Debug.Log($"NDL: MenuScene - Starting game: {selected.sceneName} (variant: {selected.name})");
        SceneManager.LoadScene(selected.sceneName);
    }
}
