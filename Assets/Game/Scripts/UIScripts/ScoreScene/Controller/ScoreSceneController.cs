using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScoreSceneController : MonoBehaviour
{
    [SerializeField] private ScoreSceneView scoreSceneView;
    protected CustomFSMManager _customFSMManager;

    void Start()
    {
        _customFSMManager = gameObject.AddComponent<CustomFSMManager>();
        _customFSMManager.fsmName = this.GetType().Name + "FSM";
        _customFSMManager.Initialize(typeof(ScoreSceneState), this.GetType(), false);
        _customFSMManager.StateMachineChange(ScoreSceneState.Initialize);

        scoreSceneView.InitView();
        scoreSceneView.onClickReplay += OnClickReplay;
        scoreSceneView.onClickChangeTeam += OnClickChangeTeam;
    }

    protected void StateMachineEnter_Initialize(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: ScoreScene - StateMachineEnter_Initialize");

        var session = GameSessionManager.Instance;

        if (session.CurrentGameMode == GameMode.Team)
        {
            scoreSceneView.ShowTeamMode();
            scoreSceneView.DisplayTeamResults(
                session.GetDisplayName1(),
                session.Player1FinalScore,
                session.GetDisplayName2(),
                session.Player2FinalScore,
                session.BlueTeamPlayers,
                session.RedTeamPlayers
            );
        }
        else
        {
            scoreSceneView.ShowOneVsOneMode();
            scoreSceneView.DisplayOneVsOneResults(
                session.GetDisplayName1(),
                session.Player1FinalScore,
                session.GetDisplayName2(),
                session.Player2FinalScore
            );
        }

        _customFSMManager.StateMachineChange(ScoreSceneState.DisplayResults);
    }

    protected void StateMachineExit_Initialize(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: ScoreScene - StateMachineExit_Initialize");
    }

    protected void StateMachineEnter_DisplayResults(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: ScoreScene - StateMachineEnter_DisplayResults");
    }

    protected void StateMachineExit_DisplayResults(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: ScoreScene - StateMachineExit_DisplayResults");
    }

    private void OnClickReplay()
    {
        Debug.Log("NDL: ScoreScene - OnClickReplay");
        string lastGame = GameSessionManager.Instance.LastPlayedGame;
        if (!string.IsNullOrEmpty(lastGame))
            SceneManager.LoadScene(lastGame);
        else
            SceneManager.LoadScene("MenuScene");
    }

    private void OnClickChangeTeam()
    {
        Debug.Log("NDL: ScoreScene - OnClickChangeTeam - Loading MenuScene");
        SceneManager.LoadScene("MenuScene");
    }
}
