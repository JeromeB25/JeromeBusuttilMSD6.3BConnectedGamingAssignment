using UnityEngine;
using Firebase.Analytics;

/// <summary>
/// Manages the player's selected profile picture.
/// </summary>
public class PlayerProfile : MonoBehaviour
{
    public static PlayerProfile Instance;

    public string CurrentProfilePictureId { get; private set; } = "default";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetProfilePicture(string picId)
    {
        CurrentProfilePictureId = picId;

        FirebaseAnalytics.LogEvent("profile_picture_changed",
            new Parameter("profile_pic_id", picId));

        Debug.Log($"[PlayerProfile] Profile picture changed to: {picId}");
    }
}
