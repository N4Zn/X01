using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controller for TeamSelect scene — manages class/player/team selection flow.
/// </summary>
public class TeamSelectController : MonoBehaviour
{
    [SerializeField] private TeamSelectView teamSelectView;

    private CustomFSMManager _customFSMManager;
    private TeamSelectModel _model;

    // Track previous state for popup return
    private TeamSelectState _stateBeforePanel;

    // Track which player is being edited in PanelPlayer
    private PlayerInfo _editingPlayer;
    private bool _isNewPlayer;
    private bool _addingStudentFromClassPanel;

    // Track which team color is being edited in PanelTeam
    private bool _editingRedTeam;

    void Start()
    {
        _model = new TeamSelectModel();

        // Ensure DataManager is loaded
        DataManager.Instance.Load();

        // Init view and subscribe events
        teamSelectView.InitView();
        SubscribeMainEvents();
        SubscribePanelEvents();

        // Init FSM
        _customFSMManager = gameObject.AddComponent<CustomFSMManager>();
        _customFSMManager.fsmName = this.GetType().Name + "FSM";
        _customFSMManager.Initialize(typeof(TeamSelectState), this.GetType(), false);
        _customFSMManager.StateMachineChange(TeamSelectState.Initialize);
    }

    void OnDestroy()
    {
        UnsubscribeMainEvents();
        UnsubscribePanelEvents();
    }

    private TeamSelectState GetCurrentState()
    {
        if (_customFSMManager == null) return TeamSelectState.Initialize;
        return (TeamSelectState)_customFSMManager.GetCurrentState();
    }

    // ===== Event Subscriptions =====

    private void SubscribeMainEvents()
    {
        teamSelectView.OnClickBack += OnClickBack;
        teamSelectView.OnClickStart += OnClickStart;
        teamSelectView.OnClickSetting += OnClickSetting;
        teamSelectView.OnClickSavedTeams += OnClickSavedTeams;
        teamSelectView.OnClickAddPlayer += OnClickAddPlayer;
        teamSelectView.OnClassSelected += OnClassSelected;
        teamSelectView.OnClickNewClass += OnClickNewClass;
        teamSelectView.OnModeChanged += OnModeChanged;
        teamSelectView.OnPlayerTapped += OnPlayerTapped;
        teamSelectView.OnPlayerLongPressed += OnPlayerLongPressed;
        teamSelectView.OnBlueTeamTapped += OnBlueTeamTapped;
        teamSelectView.OnRedTeamTapped += OnRedTeamTapped;
        teamSelectView.OnBlueTeamLongPressed += OnBlueTeamLongPressed;
        teamSelectView.OnRedTeamLongPressed += OnRedTeamLongPressed;
        teamSelectView.OnClassLongPressed += OnClassLongPressed;
    }

    private void UnsubscribeMainEvents()
    {
        if (teamSelectView == null) return;
        teamSelectView.OnClickBack -= OnClickBack;
        teamSelectView.OnClickStart -= OnClickStart;
        teamSelectView.OnClickSetting -= OnClickSetting;
        teamSelectView.OnClickSavedTeams -= OnClickSavedTeams;
        teamSelectView.OnClickAddPlayer -= OnClickAddPlayer;
        teamSelectView.OnClassSelected -= OnClassSelected;
        teamSelectView.OnClickNewClass -= OnClickNewClass;
        teamSelectView.OnModeChanged -= OnModeChanged;
        teamSelectView.OnPlayerTapped -= OnPlayerTapped;
        teamSelectView.OnPlayerLongPressed -= OnPlayerLongPressed;
        teamSelectView.OnBlueTeamTapped -= OnBlueTeamTapped;
        teamSelectView.OnRedTeamTapped -= OnRedTeamTapped;
        teamSelectView.OnBlueTeamLongPressed -= OnBlueTeamLongPressed;
        teamSelectView.OnRedTeamLongPressed -= OnRedTeamLongPressed;
        teamSelectView.OnClassLongPressed -= OnClassLongPressed;
    }

    private void SubscribePanelEvents()
    {
        // Panel Class
        var pc = teamSelectView.PanelClass;
        if (pc != null)
        {
            pc.OnClose += OnPanelClassClose;
            pc.OnSave += OnPanelClassSave;
            pc.OnCopy += OnPanelClassCopy;
            pc.OnDeleteClass += OnPanelClassDelete;
            pc.OnAddStudent += OnPanelClassAddStudent;
            pc.OnDeleteStudent += OnPanelClassDeleteStudent;
        }

        // Panel Player
        var pp = teamSelectView.PanelPlayer;
        if (pp != null)
        {
            pp.OnClose += OnPanelPlayerClose;
            pp.OnSave += OnPanelPlayerSave;
            pp.OnDelete += OnPanelPlayerDelete;
        }

        // Panel Team
        var pt = teamSelectView.PanelTeam;
        if (pt != null)
        {
            pt.OnClose += OnPanelTeamClose;
            pt.OnSave += OnPanelTeamSave;
            pt.OnDeleteTeam += OnPanelTeamDelete;
            pt.OnDeleteMember += OnPanelTeamDeleteMember;
            pt.OnTeamNameChanged += OnPanelTeamNameChanged;
        }

        // Panel Saved Team
        var pst = teamSelectView.PanelSavedTeam;
        if (pst != null)
        {
            pst.OnClose += OnPanelSavedTeamClose;
            pst.OnConfirm += OnPanelSavedTeamConfirm;
            pst.OnDeleteTeam += OnPanelSavedTeamDeleteTeam;
        }
    }

    private void UnsubscribePanelEvents()
    {
        if (teamSelectView == null) return;

        var pc = teamSelectView.PanelClass;
        if (pc != null)
        {
            pc.OnClose -= OnPanelClassClose;
            pc.OnSave -= OnPanelClassSave;
            pc.OnCopy -= OnPanelClassCopy;
            pc.OnDeleteClass -= OnPanelClassDelete;
            pc.OnAddStudent -= OnPanelClassAddStudent;
            pc.OnDeleteStudent -= OnPanelClassDeleteStudent;
        }

        var pp = teamSelectView.PanelPlayer;
        if (pp != null)
        {
            pp.OnClose -= OnPanelPlayerClose;
            pp.OnSave -= OnPanelPlayerSave;
            pp.OnDelete -= OnPanelPlayerDelete;
        }

        var pt = teamSelectView.PanelTeam;
        if (pt != null)
        {
            pt.OnClose -= OnPanelTeamClose;
            pt.OnSave -= OnPanelTeamSave;
            pt.OnDeleteTeam -= OnPanelTeamDelete;
            pt.OnDeleteMember -= OnPanelTeamDeleteMember;
            pt.OnTeamNameChanged -= OnPanelTeamNameChanged;
        }

        var pst = teamSelectView.PanelSavedTeam;
        if (pst != null)
        {
            pst.OnClose -= OnPanelSavedTeamClose;
            pst.OnConfirm -= OnPanelSavedTeamConfirm;
            pst.OnDeleteTeam -= OnPanelSavedTeamDeleteTeam;
        }
    }

    // ===== FSM State Handlers =====

    protected void StateMachineEnter_Initialize(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TeamSelect - Initialize");

        List<string> classNames = DataManager.Instance.GetAllClassNames();
        if (classNames.Count > 0)
        {
            _model.SelectedClassName = classNames[0];
            LoadClassStudents(_model.SelectedClassName);
        }

        teamSelectView.PopulateClassDropdown(classNames, _model.SelectedClassName);
        RefreshPlayerGrid();
        RefreshTeamDisplay();
        RefreshTeamLabels();

        _customFSMManager.StateMachineChange(TeamSelectState.SelectingBlue);
    }

    protected void StateMachineExit_Initialize(Enum previousState, Dictionary<string, object> options) { }

    protected void StateMachineEnter_Idle(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TeamSelect - Idle");
        teamSelectView.SetBlueSlotBlinking(false);
        teamSelectView.SetRedSlotBlinking(false);
        teamSelectView.SetTeamPanelHighlight(false, false); // both dim
        UpdateStartButton();
    }

    protected void StateMachineExit_Idle(Enum previousState, Dictionary<string, object> options) { }

    protected void StateMachineEnter_SelectingBlue(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TeamSelect - SelectingBlue");
        teamSelectView.SetBlueSlotBlinking(true);
        teamSelectView.SetRedSlotBlinking(false);
        teamSelectView.SetTeamPanelHighlight(true, false); // blue bright, red dim
        UpdateStartButton();
    }

    protected void StateMachineExit_SelectingBlue(Enum previousState, Dictionary<string, object> options) { }

    protected void StateMachineEnter_SelectingRed(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TeamSelect - SelectingRed");
        teamSelectView.SetBlueSlotBlinking(false);
        teamSelectView.SetRedSlotBlinking(true);
        teamSelectView.SetTeamPanelHighlight(false, true); // blue dim, red bright
        UpdateStartButton();
    }

    protected void StateMachineExit_SelectingRed(Enum previousState, Dictionary<string, object> options) { }

    protected void StateMachineEnter_Ready(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TeamSelect - Ready");
        teamSelectView.SetBlueSlotBlinking(false);
        teamSelectView.SetRedSlotBlinking(false);
        teamSelectView.SetTeamPanelHighlight(true, true); // both bright
        teamSelectView.SetStartButtonInteractable(true);
    }

    protected void StateMachineExit_Ready(Enum previousState, Dictionary<string, object> options) { }

    protected void StateMachineEnter_PanelClass(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TeamSelect - PanelClass");
    }

    protected void StateMachineExit_PanelClass(Enum previousState, Dictionary<string, object> options) { }

    protected void StateMachineEnter_PanelPlayer(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TeamSelect - PanelPlayer");
    }

    protected void StateMachineExit_PanelPlayer(Enum previousState, Dictionary<string, object> options) { }

    protected void StateMachineEnter_PanelTeam(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TeamSelect - PanelTeam");
    }

    protected void StateMachineExit_PanelTeam(Enum previousState, Dictionary<string, object> options) { }

    protected void StateMachineEnter_PanelSavedTeam(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TeamSelect - PanelSavedTeam");
    }

    protected void StateMachineExit_PanelSavedTeam(Enum previousState, Dictionary<string, object> options) { }

    protected void StateMachineEnter_PanelSetting(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TeamSelect - PanelSetting");
    }

    protected void StateMachineExit_PanelSetting(Enum previousState, Dictionary<string, object> options) { }

    // ===== Main Screen Event Handlers =====

    private void OnClickBack()
    {
        SceneManager.LoadScene("HomeScene");
    }

    private void OnClickStart()
    {
        if (!_model.BothTeamsReady) return;

        // Setup GameSessionManager
        GameSessionManager.Instance.SetupFromTeamSelect(
            _model.SelectedMode,
            _model.BlueTeamName, _model.BlueTeamMembers,
            _model.RedTeamName, _model.RedTeamMembers
        );

        SceneManager.LoadScene("MenuScene");
    }

    private void OnClickSetting()
    {
        _stateBeforePanel = GetCurrentState();
        // TODO: show setting panel when implemented
        Debug.Log("NDL: Setting panel not yet implemented");
    }

    private void OnClickSavedTeams()
    {
        _stateBeforePanel = GetCurrentState();
        List<TeamData> allTeams = DataManager.Instance.GetAllTeams();

        // Filter teams by currently selected class
        List<TeamData> filteredTeams = new List<TeamData>();
        string currentClass = _model.SelectedClassName;
        foreach (TeamData t in allTeams)
        {
            if (t.ClassName == currentClass)
                filteredTeams.Add(t);
        }

        teamSelectView.PanelSavedTeam.Show(filteredTeams);
        _customFSMManager.StateMachineChange(TeamSelectState.PanelSavedTeam);
    }

    private void OnClickAddPlayer()
    {
        if (string.IsNullOrEmpty(_model.SelectedClassName))
        {
            // Create a default class first
            DataManager.Instance.CreateClass("Lớp 1");
            _model.SelectedClassName = "Lớp 1";
            RefreshClassDropdown();
        }

        _stateBeforePanel = GetCurrentState();
        _editingPlayer = new PlayerInfo("", _model.SelectedClassName);
        _isNewPlayer = true;

        List<string> classNames = DataManager.Instance.GetAllClassNames();
        teamSelectView.PanelPlayer.Show(_editingPlayer, classNames, true);
        _customFSMManager.StateMachineChange(TeamSelectState.PanelPlayer);
    }

    private void OnClassSelected(string className)
    {
        _model.SelectedClassName = className;
        _model.ClearTeams();
        LoadClassStudents(className);
        RefreshPlayerGrid();
        RefreshTeamDisplay();
        UpdateStartButton();
        _customFSMManager.StateMachineChange(TeamSelectState.SelectingBlue);
    }

    private void OnClickNewClass()
    {
        _stateBeforePanel = GetCurrentState();

        string newName = "Lớp " + (DataManager.Instance.GetAllClassNames().Count + 1);
        ClassData newClass = DataManager.Instance.CreateClass(newName);
        _model.SelectedClassName = newName;

        RefreshClassDropdown();
        LoadClassStudents(newName);
        RefreshPlayerGrid();

        teamSelectView.PanelClass.Show(newClass);
        _customFSMManager.StateMachineChange(TeamSelectState.PanelClass);
    }

    private void OnClassLongPressed()
    {
        if (string.IsNullOrEmpty(_model.SelectedClassName)) return;

        ClassData classData = DataManager.Instance.GetClass(_model.SelectedClassName);
        if (classData == null) return;

        _stateBeforePanel = GetCurrentState();
        teamSelectView.PanelClass.Show(classData);
        _customFSMManager.StateMachineChange(TeamSelectState.PanelClass);
    }

    private void OnModeChanged(GameMode mode)
    {
        _model.SelectedMode = mode;
        _model.EnforceTeamSizeLimits();
        teamSelectView.SetMode(mode);
        RefreshTeamDisplay();
        RefreshPlayerGrid();
        UpdateStartButton();

        // Return to SelectingBlue if teams were cleared
        if (!_model.BothTeamsReady)
        {
            if (_model.BlueTeamMembers.Count == 0)
                _customFSMManager.StateMachineChange(TeamSelectState.SelectingBlue);
            else
                _customFSMManager.StateMachineChange(TeamSelectState.SelectingRed);
        }
    }

    private void OnPlayerTapped(int index)
    {
        if (index < 0 || index >= _model.CurrentClassStudents.Count) return;

        PlayerInfo player = _model.CurrentClassStudents[index];
        TeamSelectState state = GetCurrentState();

        // In Ready state, allow removing from either team
        if (state == TeamSelectState.Ready)
        {
            if (_model.IsInBlue(player.PlayerId))
            {
                _model.RemoveFromBlue(player.PlayerId);
                RefreshTeamDisplay();
                RefreshPlayerGrid();
                UpdateStartButton();
                if (_model.BlueTeamMembers.Count == 0)
                    _customFSMManager.StateMachineChange(TeamSelectState.SelectingBlue);
                else
                    _customFSMManager.StateMachineChange(TeamSelectState.SelectingBlue);
                return;
            }
            if (_model.IsInRed(player.PlayerId))
            {
                _model.RemoveFromRed(player.PlayerId);
                RefreshTeamDisplay();
                RefreshPlayerGrid();
                UpdateStartButton();
                _customFSMManager.StateMachineChange(TeamSelectState.SelectingRed);
                return;
            }
        }

        if (state == TeamSelectState.SelectingBlue || state == TeamSelectState.Ready)
        {
            if (_model.IsInBlue(player.PlayerId))
            {
                _model.RemoveFromBlue(player.PlayerId);
                RefreshTeamDisplay();
                RefreshPlayerGrid();
                UpdateStartButton();
                if (_model.BlueTeamMembers.Count == 0)
                    _customFSMManager.StateMachineChange(TeamSelectState.SelectingBlue);
                return;
            }

            if (_model.IsInRed(player.PlayerId)) return; // Already in other team

            if (_model.AddToBlue(player))
            {
                RefreshTeamDisplay();
                RefreshPlayerGrid();

                // In 1vs1 mode, auto-switch to red after selecting 1 blue
                if (_model.SelectedMode == GameMode.OneVsOne && _model.BlueTeamMembers.Count >= 1)
                {
                    _customFSMManager.StateMachineChange(TeamSelectState.SelectingRed);
                }
                else if (!_model.CanAddToBlue)
                {
                    _customFSMManager.StateMachineChange(TeamSelectState.SelectingRed);
                }
                UpdateStartButton();
            }
        }
        else if (state == TeamSelectState.SelectingRed)
        {
            if (_model.IsInRed(player.PlayerId))
            {
                _model.RemoveFromRed(player.PlayerId);
                RefreshTeamDisplay();
                RefreshPlayerGrid();
                UpdateStartButton();
                return;
            }

            if (_model.IsInBlue(player.PlayerId)) return;

            if (_model.AddToRed(player))
            {
                RefreshTeamDisplay();
                RefreshPlayerGrid();
                UpdateStartButton();

                if (!_model.CanAddToRed)
                {
                    _customFSMManager.StateMachineChange(TeamSelectState.Ready);
                }
            }
        }
    }

    private void OnPlayerLongPressed(int index)
    {
        if (index < 0 || index >= _model.CurrentClassStudents.Count) return;

        _stateBeforePanel = GetCurrentState();
        _editingPlayer = _model.CurrentClassStudents[index];
        _isNewPlayer = false;

        List<string> classNames = DataManager.Instance.GetAllClassNames();
        teamSelectView.PanelPlayer.Show(_editingPlayer, classNames, false);
        _customFSMManager.StateMachineChange(TeamSelectState.PanelPlayer);
    }

    private void OnBlueTeamTapped()
    {
        TeamSelectState state = GetCurrentState();
        if (state == TeamSelectState.SelectingRed || state == TeamSelectState.Ready || state == TeamSelectState.Idle)
        {
            _customFSMManager.StateMachineChange(TeamSelectState.SelectingBlue);
        }
    }

    private void OnRedTeamTapped()
    {
        TeamSelectState state = GetCurrentState();
        if (state == TeamSelectState.SelectingBlue || state == TeamSelectState.Ready || state == TeamSelectState.Idle)
        {
            _customFSMManager.StateMachineChange(TeamSelectState.SelectingRed);
        }
    }

    private void OnBlueTeamLongPressed()
    {
        _stateBeforePanel = GetCurrentState();
        _editingRedTeam = false;
        teamSelectView.PanelTeam.Show(_model.BlueTeamName, _model.BlueTeamMembers, true);
        _customFSMManager.StateMachineChange(TeamSelectState.PanelTeam);
    }

    private void OnRedTeamLongPressed()
    {
        _stateBeforePanel = GetCurrentState();
        _editingRedTeam = true;
        teamSelectView.PanelTeam.Show(_model.RedTeamName, _model.RedTeamMembers, false);
        _customFSMManager.StateMachineChange(TeamSelectState.PanelTeam);
    }

    // ===== Panel Class Handlers =====

    private void OnPanelClassClose()
    {
        teamSelectView.PanelClass.Hide();
        ReturnFromPanel();
    }

    private void OnPanelClassSave()
    {
        string newName = teamSelectView.PanelClass.GetClassName();
        if (!string.IsNullOrEmpty(newName) && newName != _model.SelectedClassName)
        {
            DataManager.Instance.RenameClass(_model.SelectedClassName, newName);
            _model.SelectedClassName = newName;
        }
        RefreshClassDropdown();
        LoadClassStudents(_model.SelectedClassName);
        RefreshPlayerGrid();
        teamSelectView.PanelClass.Hide();
        ReturnFromPanel();
    }

    private void OnPanelClassCopy()
    {
        string copyName = _model.SelectedClassName + " (Copy)";
        DataManager.Instance.CopyClass(_model.SelectedClassName, copyName);
        RefreshClassDropdown();
    }

    private void OnPanelClassDelete()
    {
        DataManager.Instance.DeleteClass(_model.SelectedClassName);
        List<string> classNames = DataManager.Instance.GetAllClassNames();
        _model.SelectedClassName = classNames.Count > 0 ? classNames[0] : "";
        RefreshClassDropdown();
        LoadClassStudents(_model.SelectedClassName);
        RefreshPlayerGrid();
        teamSelectView.PanelClass.Hide();
        ReturnFromPanel();
    }

    private void OnPanelClassAddStudent()
    {
        if (string.IsNullOrEmpty(_model.SelectedClassName)) return;

        // Open PanelPlayer for adding a new student (instead of auto-creating)
        _editingPlayer = new PlayerInfo("", _model.SelectedClassName);
        _isNewPlayer = true;
        _addingStudentFromClassPanel = true;

        List<string> classNames = DataManager.Instance.GetAllClassNames();
        teamSelectView.PanelPlayer.Show(_editingPlayer, classNames, true);
        _customFSMManager.StateMachineChange(TeamSelectState.PanelPlayer);
    }

    private void OnPanelClassDeleteStudent(int index)
    {
        ClassData c = DataManager.Instance.GetClass(_model.SelectedClassName);
        if (c == null || index < 0 || index >= c.Students.Count) return;

        DataManager.Instance.DeletePlayer(_model.SelectedClassName, c.Students[index].PlayerId);

        c = DataManager.Instance.GetClass(_model.SelectedClassName);
        if (c != null)
        {
            teamSelectView.PanelClass.UpdateStudentList(c.Students);
        }
    }

    // ===== Panel Player Handlers =====

    private void OnPanelPlayerClose()
    {
        teamSelectView.PanelPlayer.Hide();

        // If was adding from PanelClass, return to PanelClass
        if (_addingStudentFromClassPanel)
        {
            _addingStudentFromClassPanel = false;
            ClassData c = DataManager.Instance.GetClass(_model.SelectedClassName);
            if (c != null)
            {
                teamSelectView.PanelClass.Show(c);
            }
            _customFSMManager.StateMachineChange(TeamSelectState.PanelClass);
        }
        else
        {
            ReturnFromPanel();
        }
    }

    private void OnPanelPlayerSave()
    {
        if (_editingPlayer == null) return;

        string playerName = teamSelectView.PanelPlayer.GetPlayerName();

        // Validate: name is required
        if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(playerName.Trim()))
        {
            teamSelectView.PanelPlayer.ShowNameError("Vui lòng nhập tên học sinh");
            return;
        }

        _editingPlayer.PlayerName = playerName.Trim();
        _editingPlayer.AvatarIndex = teamSelectView.PanelPlayer.GetSelectedAvatar();
        _editingPlayer.BackgroundColorIndex = teamSelectView.PanelPlayer.GetSelectedBackgroundColor();
        _editingPlayer.HairIndex = teamSelectView.PanelPlayer.GetSelectedHair();
        _editingPlayer.GlassesIndex = teamSelectView.PanelPlayer.GetSelectedGlasses();
        _editingPlayer.Gender = teamSelectView.PanelPlayer.GetSelectedGender();
        string newClass = teamSelectView.PanelPlayer.GetSelectedClass();
        if (!string.IsNullOrEmpty(newClass)) _editingPlayer.ClassName = newClass;

        if (_isNewPlayer)
        {
            string targetClass = _editingPlayer.ClassName;
            if (string.IsNullOrEmpty(targetClass)) targetClass = _model.SelectedClassName;

            PlayerInfo added = DataManager.Instance.AddPlayer(targetClass, _editingPlayer.PlayerName);
            added.AvatarIndex = _editingPlayer.AvatarIndex;
            added.BackgroundColorIndex = _editingPlayer.BackgroundColorIndex;
            added.HairIndex = _editingPlayer.HairIndex;
            added.GlassesIndex = _editingPlayer.GlassesIndex;
            added.Gender = _editingPlayer.Gender;
            DataManager.Instance.UpdatePlayer(added);
        }
        else
        {
            DataManager.Instance.UpdatePlayer(_editingPlayer);
        }

        LoadClassStudents(_model.SelectedClassName);
        RefreshPlayerGrid();
        teamSelectView.PanelPlayer.Hide();

        // If adding student from PanelClass, return to PanelClass with updated list
        if (_addingStudentFromClassPanel)
        {
            _addingStudentFromClassPanel = false;
            ClassData c = DataManager.Instance.GetClass(_model.SelectedClassName);
            if (c != null)
            {
                teamSelectView.PanelClass.Show(c);
            }
            _customFSMManager.StateMachineChange(TeamSelectState.PanelClass);
        }
        else
        {
            ReturnFromPanel();
        }
    }

    private void OnPanelPlayerDelete()
    {
        if (_editingPlayer == null || _isNewPlayer) return;

        // Remove from teams if present
        _model.RemoveFromBlue(_editingPlayer.PlayerId);
        _model.RemoveFromRed(_editingPlayer.PlayerId);

        DataManager.Instance.DeletePlayer(_editingPlayer.ClassName, _editingPlayer.PlayerId);

        LoadClassStudents(_model.SelectedClassName);
        RefreshPlayerGrid();
        RefreshTeamDisplay();
        teamSelectView.PanelPlayer.Hide();
        ReturnFromPanel();
    }

    // ===== Panel Team Handlers =====

    private void OnPanelTeamClose()
    {
        teamSelectView.PanelTeam.Hide();
        RefreshTeamDisplay();
        RefreshPlayerGrid();
        RefreshTeamLabels();
        UpdateStartButton();
        ReturnFromPanel();
    }

    private void OnPanelTeamSave()
    {
        // Save current team composition as a named team
        string teamName = teamSelectView.PanelTeam.GetTeamName();
        if (string.IsNullOrEmpty(teamName)) teamName = "Saved Team";

        TeamData team = DataManager.Instance.CreateTeam(teamName);
        team.ClassName = _model.SelectedClassName;

        // Use tracked team color to get the correct members
        List<PlayerInfo> members = _editingRedTeam ? _model.RedTeamMembers : _model.BlueTeamMembers;
        team.MemberPlayerIds.Clear();
        foreach (PlayerInfo p in members)
        {
            team.MemberPlayerIds.Add(p.PlayerId);
        }
        DataManager.Instance.SaveTeam(team);

        // Update team name from input
        string editedName = teamSelectView.PanelTeam.GetTeamName();
        if (!string.IsNullOrEmpty(editedName))
        {
            if (_editingRedTeam)
                _model.RedTeamName = editedName;
            else
                _model.BlueTeamName = editedName;
        }

        teamSelectView.PanelTeam.Hide();
        RefreshTeamDisplay();
        RefreshPlayerGrid();
        RefreshTeamLabels();
        UpdateStartButton();
        ReturnFromPanel();
    }



    private void OnPanelTeamDelete()
    {
        teamSelectView.PanelTeam.Hide();
        ReturnFromPanel();
    }

    private void OnPanelTeamDeleteMember(int index)
    {
        List<PlayerInfo> members = _editingRedTeam ? _model.RedTeamMembers : _model.BlueTeamMembers;
        if (index < 0 || index >= members.Count) return;

        string playerId = members[index].PlayerId;
        if (_editingRedTeam)
            _model.RemoveFromRed(playerId);
        else
            _model.RemoveFromBlue(playerId);

        // Refresh the member list in the panel
        List<PlayerInfo> updatedMembers = _editingRedTeam ? _model.RedTeamMembers : _model.BlueTeamMembers;
        teamSelectView.PanelTeam.UpdateMemberList(updatedMembers);
    }

    private void OnPanelTeamNameChanged(string newName)
    {
        if (_editingRedTeam)
            _model.RedTeamName = newName;
        else
            _model.BlueTeamName = newName;
    }

    // ===== Panel Saved Team Handlers =====

    private void OnPanelSavedTeamClose()
    {
        teamSelectView.PanelSavedTeam.Hide();
        ReturnFromPanel();
    }

    private void OnPanelSavedTeamDeleteTeam(string teamId)
    {
        DataManager.Instance.DeleteTeam(teamId);

        // Clear selection if deleted team was selected
        if (teamSelectView.PanelSavedTeam.GetSelectedBlueTeamId() == teamId ||
            teamSelectView.PanelSavedTeam.GetSelectedRedTeamId() == teamId)
        {
            // Re-show panel with updated filtered list
        }

        // Refresh the panel with updated filtered list
        List<TeamData> allTeams = DataManager.Instance.GetAllTeams();
        List<TeamData> filteredTeams = new List<TeamData>();
        string currentClass = _model.SelectedClassName;
        foreach (TeamData t in allTeams)
        {
            if (t.ClassName == currentClass)
                filteredTeams.Add(t);
        }
        teamSelectView.PanelSavedTeam.Show(filteredTeams);
    }

    private void OnPanelSavedTeamConfirm()
    {
        string blueTeamId = teamSelectView.PanelSavedTeam.GetSelectedBlueTeamId();
        string redTeamId = teamSelectView.PanelSavedTeam.GetSelectedRedTeamId();

        if (!string.IsNullOrEmpty(blueTeamId) && !string.IsNullOrEmpty(redTeamId))
        {
            _model.ClearTeams();

            TeamData blueTeam = DataManager.Instance.GetTeam(blueTeamId);
            TeamData redTeam = DataManager.Instance.GetTeam(redTeamId);

            // Load saved teams directly, bypassing duplicate/size checks
            if (blueTeam != null)
            {
                foreach (string pid in blueTeam.MemberPlayerIds)
                {
                    PlayerInfo p = DataManager.Instance.GetPlayer(pid);
                    if (p != null) _model.ForceAddToBlue(p);
                }
            }

            if (redTeam != null)
            {
                foreach (string pid in redTeam.MemberPlayerIds)
                {
                    PlayerInfo p = DataManager.Instance.GetPlayer(pid);
                    if (p != null) _model.ForceAddToRed(p);
                }
            }

            RefreshTeamDisplay();
            RefreshPlayerGrid();
        }

        teamSelectView.PanelSavedTeam.Hide();

        if (_model.BothTeamsReady)
            _customFSMManager.StateMachineChange(TeamSelectState.Ready);
        else
            _customFSMManager.StateMachineChange(TeamSelectState.SelectingBlue);
    }

    // ===== Helper Methods =====

    private void ReturnFromPanel()
    {
        _customFSMManager.StateMachineChange(_stateBeforePanel);
    }

    private void LoadClassStudents(string className)
    {
        ClassData c = DataManager.Instance.GetClass(className);
        _model.CurrentClassStudents = c != null ? c.Students : new List<PlayerInfo>();
    }

    private void RefreshPlayerGrid()
    {
        List<string> blueIds = new List<string>();
        foreach (PlayerInfo p in _model.BlueTeamMembers) blueIds.Add(p.PlayerId);

        List<string> redIds = new List<string>();
        foreach (PlayerInfo p in _model.RedTeamMembers) redIds.Add(p.PlayerId);

        teamSelectView.PopulatePlayerGrid(_model.CurrentClassStudents, blueIds, redIds);
    }

    private void RefreshTeamDisplay()
    {
        teamSelectView.UpdateBlueTeam(_model.BlueTeamMembers);
        teamSelectView.UpdateRedTeam(_model.RedTeamMembers);
    }

    private void RefreshClassDropdown()
    {
        List<string> classNames = DataManager.Instance.GetAllClassNames();
        teamSelectView.PopulateClassDropdown(classNames, _model.SelectedClassName);
    }

    private void RefreshTeamLabels()
    {
        teamSelectView.SetTeamLabels(_model.BlueTeamName, _model.RedTeamName);
    }

    private void UpdateStartButton()
    {
        teamSelectView.SetStartButtonInteractable(_model.BothTeamsReady);
    }
}
