using UnityEngine;

public class MatchAnalyticsTracker : MonoBehaviour
{
    private string currentMatchId;
    private bool matchStarted = false;

    private void OnEnable()
    {
        GameManager.MoveExecutedEvent += OnFirstMove;
        GameManager.GameEndedEvent += OnGameEnd;
    }

    private void OnDisable()
    {
        GameManager.MoveExecutedEvent -= OnFirstMove;
        GameManager.GameEndedEvent -= OnGameEnd;
    }

    private void OnFirstMove()
    {
        if (matchStarted) return;

        currentMatchId = AnalyticsLogger.GenerateMatchId();
        AnalyticsLogger.LogMatchStart(currentMatchId);
        matchStarted = true;
    }

    private void OnGameEnd()
    {
        string result = "Draw";

        // We try to get the real result from the GameManager's HalfMoveTimeline
        var timeline = GameManager.Instance.HalfMoveTimeline;
        if (timeline != null && timeline.TryGetCurrent(out var latest))
        {
            result = latest.CausedCheckmate ? $"{latest.Piece.Owner} Wins" : "Draw";
        }

        AnalyticsLogger.LogMatchEnd(currentMatchId, result);
        matchStarted = false;
    }
}
