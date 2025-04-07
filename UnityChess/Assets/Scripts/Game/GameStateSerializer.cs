using System;
using UnityChess;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Handles serialization and deserialization of game state for rejoin functionality.
/// </summary>
public class GameStateSerializer : MonoBehaviour
{
    // Singleton pattern
    public static GameStateSerializer Instance { get; private set; }

    // Keys for saving different aspects of the game state
    private const string GAME_STATE_KEY = "SavedChessGameState";
    private const string PLAYER_SIDE_KEY = "SavedPlayerSide";
    private const string LAST_IP_KEY = "SavedLastIP";

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Saves the current game state for later restoration during rejoin
    /// </summary>
    public void SaveGameState()
    {
        if (!GameManager.Instance)
        {
            Debug.LogWarning("Cannot save game state: GameManager instance not found");
            return;
        }

        try
        {
            // Get the serialized game state using the game manager's existing serialization
            string gameState = GameManager.Instance.SerializeGame();
            
            // Save the game state to PlayerPrefs
            PlayerPrefs.SetString(GAME_STATE_KEY, gameState);
            
            // Save the player's side if in a network game
            if (ChessNetworkManager.Instance && NetworkManager.Singleton.IsConnectedClient)
            {
                // Get the player's side using the method you have in your implementation
                Side playerSide = ChessNetworkManager.Instance.GetPlayerSide(NetworkManager.Singleton.LocalClientId);
                PlayerPrefs.SetInt(PLAYER_SIDE_KEY, (int)playerSide);
            }
            
            // Commit changes to ensure they're saved
            PlayerPrefs.Save();
            
            Debug.Log("Game state saved successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save game state: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// Saves the IP address for reconnection
    /// </summary>
    public void SaveIPAddress(string ipAddress)
    {
        PlayerPrefs.SetString(LAST_IP_KEY, ipAddress);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Gets the saved IP address
    /// </summary>
    public string GetSavedIPAddress()
    {
        return PlayerPrefs.GetString(LAST_IP_KEY, "127.0.0.1");
    }

    /// <summary>
    /// Checks if there is a saved game state
    /// </summary>
    public bool HasSavedGameState()
    {
        return PlayerPrefs.HasKey(GAME_STATE_KEY);
    }

    /// <summary>
    /// Restores a saved game state after reconnection
    /// </summary>
    public void RestoreSavedGameState()
    {
        if (!GameManager.Instance)
        {
            Debug.LogWarning("Cannot restore game state: GameManager instance not found");
            return;
        }

        try
        {
            if (HasSavedGameState())
            {
                string gameState = PlayerPrefs.GetString(GAME_STATE_KEY);
                
                // Use the game manager's existing load game functionality
                GameManager.Instance.LoadGame(gameState);
                
                Debug.Log("Game state restored successfully");
            }
            else
            {
                Debug.Log("No saved game state found to restore");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to restore game state: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// Gets the saved player side
    /// </summary>
    public Side GetSavedPlayerSide()
    {
        return (Side)PlayerPrefs.GetInt(PLAYER_SIDE_KEY, (int)Side.None);
    }

    /// <summary>
    /// Clears saved game state after a normal game end or new game start
    /// </summary>
    public void ClearSavedGameState()
    {
        PlayerPrefs.DeleteKey(GAME_STATE_KEY);
        PlayerPrefs.DeleteKey(PLAYER_SIDE_KEY);
        // Don't delete IP address as it's needed for rejoin
        PlayerPrefs.Save();
        Debug.Log("Saved game state cleared");
    }
}