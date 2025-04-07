using UnityEngine;
using UnityEngine.UI;

public class SimpleAnalyticsDisplay : MonoBehaviour
{
    public Text analyticsText;

    void Start()
    {
        string topDLC = PlayerPrefs.GetString("top_dlc", "None");
        int gamesPlayed = PlayerPrefs.GetInt("games_played", 0);

        analyticsText.text = $"📊 Stats:\nGames Played: {gamesPlayed}\nTop DLC: {topDLC}";
    }
}
