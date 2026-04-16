using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuSceneController : MonoBehaviour
{
    [SerializeField] private MenuSceneView menuSceneView;
    protected CustomFSMManager _customFSMManager;

    private int _currentCategory = 0;
    private int _selectedGameIndex = -1; // single selected game in grid (0-9)
    private HashSet<int> _selectedGames = new HashSet<int>();

    void Start()
    {
        _customFSMManager = gameObject.AddComponent<CustomFSMManager>();
        _customFSMManager.fsmName = this.GetType().Name + "FSM";
        _customFSMManager.Initialize(typeof(MenuSceneState), this.GetType(), false);
        _customFSMManager.StateMachineChange(MenuSceneState.Initialize);

        menuSceneView.InitView();

        // Navigation
        menuSceneView.onClickBack += OnClickBack;
        menuSceneView.onClickHome += OnClickHome;
        menuSceneView.onClickSettings += OnClickSettings;
        menuSceneView.onClickStart += OnClickStart;

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
        menuSceneView.UpdateGameGridInteractable();
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
        SceneManager.LoadScene("HomeScene");
    }

    private void OnClickSettings()
    {
        Debug.Log("NDL: MenuScene - OnClickSettings");
    }

    // ===== Category =====

    private void OnCategorySelected(int categoryIndex)
    {
        Debug.Log("NDL: MenuScene - OnCategorySelected: " + categoryIndex);
        _currentCategory = categoryIndex;
        menuSceneView.SetActiveCategory(categoryIndex);
    }

    // ===== Game Selection =====

    private void OnGameSelected(int gridIndex)
    {
        // Grid index: row * 5 + col
        int col = gridIndex % MenuSceneView.COLS;
        int row = gridIndex / MenuSceneView.COLS;

        string sceneName = MenuSceneView.GameSceneNames[col, row];
        if (sceneName == null)
        {
            Debug.Log("NDL: MenuScene - Game not implemented: col=" + col + " row=" + row);
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

        Debug.Log("NDL: MenuScene - GameSelected: " + gridIndex + " (" + MenuSceneView.GameNames[col, row] + ")");
    }

    // ===== Random Selection =====

    private void OnClickRandomSelect()
    {
        Debug.Log("NDL: MenuScene - RandomSelect clicked");

        _selectedGames.Clear();
        List<int> implementedGames = new List<int>();

        // Find all implemented games
        for (int col = 0; col < MenuSceneView.COLS; col++)
        {
            for (int row = 0; row < MenuSceneView.ROWS; row++)
            {
                if (MenuSceneView.GameSceneNames[col, row] != null)
                {
                    implementedGames.Add(row * MenuSceneView.COLS + col);
                }
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

        int col2 = picked % MenuSceneView.COLS;
        int row2 = picked / MenuSceneView.COLS;
        Debug.Log("NDL: MenuScene - RandomSelected: " + MenuSceneView.GameNames[col2, row2]);
    }

    // ===== Start =====

    private void OnClickStart()
    {
        if (_selectedGames.Count == 0) return;

        // Load the selected game
        int gameIdx = -1;
        foreach (int idx in _selectedGames)
        {
            gameIdx = idx;
            break;
        }

        int col = gameIdx % MenuSceneView.COLS;
        int row = gameIdx / MenuSceneView.COLS;
        string sceneName = MenuSceneView.GameSceneNames[col, row];

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
