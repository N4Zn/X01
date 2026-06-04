using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CharacterSelectController : MonoBehaviour
{
    [SerializeField] private CharacterSelectView characterSelectView;
    protected CustomFSMManager _customFSMManager;

    private GameMode _selectedMode = GameMode.OneVsOne;
    private int _selectingPlayer = 1; // 1 or 2
    private int _p1CharIndex = -1;
    private int _p2CharIndex = -1;

    void Start()
    {
        _customFSMManager = gameObject.AddComponent<CustomFSMManager>();
        _customFSMManager.fsmName = this.GetType().Name + "FSM";
        _customFSMManager.Initialize(typeof(CharacterSelectState), this.GetType(), false);
        _customFSMManager.StateMachineChange(CharacterSelectState.Initialize);

        characterSelectView.InitView();
        characterSelectView.onClickOneVsOne += OnClickOneVsOne;
        characterSelectView.onClickTeamMode += OnClickTeamMode;
        characterSelectView.onClickBack += OnClickBack;
        characterSelectView.onClickNext += OnClickNext;
        characterSelectView.onCharacterSelected += OnCharacterSelected;
    }

    // ===== State Machine Handlers =====

    protected void StateMachineEnter_Initialize(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: CharacterSelect - StateMachineEnter_Initialize");
        _customFSMManager.StateMachineChange(CharacterSelectState.ModeSelect);
    }

    protected void StateMachineExit_Initialize(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: CharacterSelect - StateMachineExit_Initialize");
    }

    protected void StateMachineEnter_ModeSelect(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: CharacterSelect - StateMachineEnter_ModeSelect");
        characterSelectView.ShowModeSelectPanel();
    }

    protected void StateMachineExit_ModeSelect(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: CharacterSelect - StateMachineExit_ModeSelect");
    }

    protected void StateMachineEnter_TeamSetup(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: CharacterSelect - StateMachineEnter_TeamSetup");
        _selectingPlayer = 1;
        _p1CharIndex = -1;
        _p2CharIndex = -1;
        characterSelectView.ShowCharacterSelect(_selectedMode == GameMode.Team);
    }

    protected void StateMachineExit_TeamSetup(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: CharacterSelect - StateMachineExit_TeamSetup");
    }

    protected void StateMachineEnter_Ready(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: CharacterSelect - StateMachineEnter_Ready");
        characterSelectView.ShowNextButton();
    }

    protected void StateMachineExit_Ready(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: CharacterSelect - StateMachineExit_Ready");
    }

    // ===== Event Handlers =====

    private void OnClickOneVsOne()
    {
        Debug.Log("NDL: CharacterSelect - Selected 1 vs 1 mode");
        _selectedMode = GameMode.OneVsOne;
        _customFSMManager.StateMachineChange(CharacterSelectState.TeamSetup);
    }

    private void OnClickTeamMode()
    {
        Debug.Log("NDL: CharacterSelect - Selected Team mode");
        _selectedMode = GameMode.Team;
        _customFSMManager.StateMachineChange(CharacterSelectState.TeamSetup);
    }

    private void OnCharacterSelected(int charIndex)
    {
        Debug.Log($"NDL: CharacterSelect - Character {charIndex} selected by Player {_selectingPlayer}");

        if (_selectingPlayer == 1)
        {
            _p1CharIndex = charIndex;
            characterSelectView.SetPlayer1Character(charIndex);
            characterSelectView.DisableCharacter(charIndex);

            // Move to Player 2 selection
            _selectingPlayer = 2;
            characterSelectView.UpdateSelectingPlayer(2);
        }
        else if (_selectingPlayer == 2)
        {
            _p2CharIndex = charIndex;
            characterSelectView.SetPlayer2Character(charIndex);

            // Both players selected, transition to Ready
            _customFSMManager.StateMachineChange(CharacterSelectState.Ready);
        }
    }

    private void OnClickBack()
    {
        Debug.Log("NDL: CharacterSelect - Back to HomeScene");
        SceneManager.LoadScene("MenuScene");
    }

    private void OnClickNext()
    {
        Debug.Log("NDL: CharacterSelect - Next to MenuScene (Game Select)");

        string p1Name = CharacterDatabase.CharacterNames[_p1CharIndex];
        string p2Name = CharacterDatabase.CharacterNames[_p2CharIndex];

        if (_selectedMode == GameMode.OneVsOne)
        {
            GameSessionManager.Instance.SetupOneVsOne(p1Name, p2Name);
        }
        else
        {
            GameSessionManager.Instance.SetupTeams("Team A", "Team B", p1Name, p2Name);
        }

        GameSessionManager.Instance.Players[0].CharacterIndex = _p1CharIndex;
        GameSessionManager.Instance.Players[1].CharacterIndex = _p2CharIndex;

        SceneManager.LoadScene("MenuScene");
    }
}
