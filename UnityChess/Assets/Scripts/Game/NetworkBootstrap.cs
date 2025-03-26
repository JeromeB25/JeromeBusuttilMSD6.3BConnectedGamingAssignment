using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

/// <summary>
/// Bootstraps the networking components for the chess game.
/// This script ensures all required network components are properly initialized.
/// </summary>
public class NetworkBootstrap : MonoBehaviour
{
    // Singleton reference
    public static NetworkBootstrap Instance { get; private set; }
    
    // Event that other systems can subscribe to when network initialization is complete
    public delegate void NetworkInitializedEvent();
    public static event NetworkInitializedEvent OnNetworkInitialized;

    [Header("Network Configuration")]
    [SerializeField] private ushort port = 7777;
    [SerializeField] private GameObject chessNetworkManagerPrefab;
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InitializeNetworking();
    }

    /// <summary>
    /// Initialize all required networking components
    /// </summary>
    public void InitializeNetworking()
    {
        // Check if we already have NetworkManager
        if (NetworkManager.Singleton == null)
        {
            Debug.Log("Creating NetworkManager");
            
            // Create NetworkManager
            GameObject networkManagerObj = new GameObject("NetworkManager");
            NetworkManager networkManager = networkManagerObj.AddComponent<NetworkManager>();
            
            // Add UnityTransport
            UnityTransport transport = networkManagerObj.AddComponent<UnityTransport>();
            transport.ConnectionData.Port = port;
            
            DontDestroyOnLoad(networkManagerObj);
        }
        else
        {
            Debug.Log("NetworkManager already exists");
        }

        // Check if we have ChessNetworkManager
        if (ChessNetworkManager.Instance == null)
        {
            Debug.Log("Creating ChessNetworkManager");
            
            if (chessNetworkManagerPrefab != null)
            {
                // Instantiate from prefab if available
                Instantiate(chessNetworkManagerPrefab);
            }
            else
            {
                // Create a new GameObject with required components
                GameObject chessNetManagerObj = new GameObject("ChessNetworkManager");
                ChessNetworkManager chessNetManager = chessNetManagerObj.AddComponent<ChessNetworkManager>();
                NetworkObject netObj = chessNetManagerObj.AddComponent<NetworkObject>();
                
                DontDestroyOnLoad(chessNetManagerObj);
            }
        }
        else
        {
            Debug.Log("ChessNetworkManager already exists");
        }

        // Notify that network initialization is complete
        OnNetworkInitialized?.Invoke();
    }
}