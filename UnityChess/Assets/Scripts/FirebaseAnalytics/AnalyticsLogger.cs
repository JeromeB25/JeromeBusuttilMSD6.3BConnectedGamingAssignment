using UnityEngine;
using Firebase.Analytics;
using System;

public static class AnalyticsLogger
{
    public static void LogMatchStart(string matchId)
    {
        FirebaseAnalytics.LogEvent("match_started",
            new Parameter("match_id", matchId),
            new Parameter("timestamp", DateTime.UtcNow.ToString("o")));

        Debug.Log($"[Analytics] Match started: {matchId}");
    }

    public static void LogMatchEnd(string matchId, string result)
    {
        FirebaseAnalytics.LogEvent("match_ended",
            new Parameter("match_id", matchId),
            new Parameter("result", result),
            new Parameter("timestamp", DateTime.UtcNow.ToString("o")));

        Debug.Log($"[Analytics] Match ended: {matchId}, result: {result}");
    }

    public static void LogDLCPurchase(string dlcName)
    {
        FirebaseAnalytics.LogEvent("dlc_purchased",
            new Parameter("dlc_name", dlcName),
            new Parameter("timestamp", DateTime.UtcNow.ToString("o")));

        Debug.Log($"[Analytics] DLC purchased: {dlcName}");
    }

    public static void LogProfilePictureChanged(string profileId)
    {
        FirebaseAnalytics.LogEvent("profile_picture_changed",
            new Parameter("profile_id", profileId),
            new Parameter("timestamp", DateTime.UtcNow.ToString("o")));

        Debug.Log($"[Analytics] Profile picture changed to: {profileId}");
    }

    public static string GenerateMatchId()
    {
        return Guid.NewGuid().ToString();
    }
}
