using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System.Threading.Tasks;
using System.Collections.Generic;

/// <summary>
/// Handles saving and loading game state and equipped profile picture to/from Firebase.
/// </summary>
public class GameStateSaver : MonoBehaviour
{
    public static GameStateSaver Instance;

    private bool firebaseReady = false;
    private const string databaseUrl = "https://jeromecgdlc-default-rtdb.europe-west1.firebasedatabase.app/";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeFirebase();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            var status = task.Result;
            if (status == DependencyStatus.Available)
            {
                firebaseReady = true;
                Debug.Log("[GameStateSaver] Firebase initialized.");
            }
            else
            {
                Debug.LogError($"[GameStateSaver] Firebase initialization failed: {status}");
            }
        });
    }

    public async Task<bool> SaveGameStateToFirebase(string userId)
    {
        if (!firebaseReady)
        {
            Debug.LogError("[GameStateSaver] Firebase not ready. Save aborted.");
            return false;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogError("[GameStateSaver] GameManager.Instance is null.");
            return false;
        }

        string gameState = GameManager.Instance.SerializeGame();
        string profilePicId = PlayerProfile.Instance?.CurrentProfilePictureId ?? "unknown";
        string timestamp = System.DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        Debug.Log($"[GameStateSaver] Saving game for {userId} at {timestamp}");
        Debug.Log($"[GameStateSaver] Game state: {gameState}");
        Debug.Log($"[GameStateSaver] Profile picture ID: {profilePicId}");

        try
        {
            var db = FirebaseDatabase.GetInstance(databaseUrl);

            var saveData = new Dictionary<string, object>
            {
                { "game_state", gameState },
                { "profile_picture", profilePicId }
            };

            await db.RootReference
                .Child("saved_games")
                .Child(userId)
                .Child(timestamp)
                .SetValueAsync(saveData);

            Debug.Log("[Firebase] Game and profile picture saved successfully.");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameStateSaver] Save failed: {e.Message}");
            return false;
        }
    }

    public async Task<string> LoadMostRecentGameState(string userId)
    {
        if (!firebaseReady)
        {
            Debug.LogError("[GameStateSaver] Firebase not ready. Load aborted.");
            return null;
        }

        Debug.Log($"[GameStateSaver] Attempting to load game state for user: {userId}");

        try
        {
            var db = FirebaseDatabase.GetInstance(databaseUrl);

            var snapshot = await db.RootReference
                .Child("saved_games")
                .Child(userId)
                .GetValueAsync();

            string lastState = null;
            string latestKey = null;

            foreach (var child in snapshot.Children)
            {
                latestKey = child.Key;

                if (child.Child("game_state") != null)
                {
                    lastState = child.Child("game_state").Value?.ToString();
                }

                if (child.Child("profile_picture") != null)
                {
                    string loadedProfileId = child.Child("profile_picture").Value?.ToString();
                    if (!string.IsNullOrEmpty(loadedProfileId))
                    {
                        PlayerProfile.Instance?.SetProfilePicture(loadedProfileId);
                        Debug.Log($"[GameStateSaver] Restored profile picture: {loadedProfileId}");
                    }
                }
            }

            if (string.IsNullOrEmpty(lastState))
            {
                Debug.LogWarning("[GameStateSaver] No game state found.");
                return null;
            }

            Debug.Log($"[GameStateSaver] Loaded state from timestamp {latestKey}: {lastState}");
            return lastState;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameStateSaver] Load failed: {e.Message}");
            return null;
        }
    }
}
