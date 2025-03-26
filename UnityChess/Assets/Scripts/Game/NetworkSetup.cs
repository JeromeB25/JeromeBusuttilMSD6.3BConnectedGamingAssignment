using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Sets up networking components for the chess game.
/// This script avoids adding NetworkObjects to individual squares to prevent conflicts.
/// </summary>
public class NetworkSetup : MonoBehaviour
{
    [SerializeField] private GameObject chessBoard;

    private void Start()
    {
        // Only set up minimal networking components
        SetupMinimalNetworking();
    }

    private void SetupMinimalNetworking()
    {
        Debug.Log("Setting up minimal networking for the chess game");

        // Find chessBoard if not assigned
        if (chessBoard == null)
        {
            chessBoard = GameObject.FindGameObjectWithTag("Board");
            if (chessBoard == null)
            {
                chessBoard = GameObject.Find("Board");
            }
        }

        if (chessBoard != null)
        {
            Debug.Log("Found chess board, configuring for networking");

            // Ensure the board itself has a NetworkObject
            if (chessBoard.GetComponent<NetworkObject>() == null)
            {
                NetworkObject boardNetObj = chessBoard.AddComponent<NetworkObject>();
                boardNetObj.DontDestroyWithOwner = true;
            }

            // Setup ChessMoveRelay to handle move synchronization
            SetupChessMoveRelay();
        }
        else
        {
            Debug.LogWarning("Chess board reference not assigned or found in NetworkSetup");
        }
    }

    /// <summary>
    /// Sets up the ChessMoveRelay component to synchronize moves
    /// </summary>
    private void SetupChessMoveRelay()
    {
        // Find or create ChessMoveRelay
        ChessMoveRelay relay = FindObjectOfType<ChessMoveRelay>();
        if (relay == null)
        {
            GameObject relayObj = new GameObject("ChessMoveRelay");
            relay = relayObj.AddComponent<ChessMoveRelay>();
            
            // Set board reference
            relay.chessBoard = chessBoard;
            
            // Add NetworkObject
            NetworkObject netObj = relayObj.AddComponent<NetworkObject>();
            
            // If we're already a host, spawn the relay
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                netObj.Spawn();
            }
        }
    }
}