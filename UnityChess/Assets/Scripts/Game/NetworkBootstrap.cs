using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Firebase;
using Firebase.Extensions;

/// <summary>
/// Bootstraps the networking and Firebase components for the chess game.
/// Ensures all required systems are properly initialized on startup.
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
        InitializeFirebase(); // Firebase setup 
    }

    /// <summary>
    /// Initialize networking components like NetworkManager, ChessNetworkManager, and GameStateSerializer.
    /// </summary>
    public void InitializeNetworking()
    {
        // Ensure NetworkManager exists
        if (NetworkManager.Singleton == null)
        {
            Debug.Log("[NetworkBootstrap] Creating NetworkManager");
            
            GameObject networkManagerObj = new GameObject("NetworkManager");
            NetworkManager networkManager = networkManagerObj.AddComponent<NetworkManager>();

            UnityTransport transport = networkManagerObj.AddComponent<UnityTransport>();
            transport.ConnectionData.Port = port;

            DontDestroyOnLoad(networkManagerObj);
        }
        else
        {
            Debug.Log("[NetworkBootstrap] NetworkManager already exists");
        }

        // Ensure ChessNetworkManager exists
        if (ChessNetworkManager.Instance == null)
        {
            Debug.Log("[NetworkBootstrap] Creating ChessNetworkManager");

            if (chessNetworkManagerPrefab != null)
            {
                Instantiate(chessNetworkManagerPrefab);
            }
            else
            {
                GameObject chessNetManagerObj = new GameObject("ChessNetworkManager");
                chessNetManagerObj.AddComponent<ChessNetworkManager>();
                chessNetManagerObj.AddComponent<NetworkObject>();

                DontDestroyOnLoad(chessNetManagerObj);
            }
        }
        else
        {
            Debug.Log("[NetworkBootstrap] ChessNetworkManager already exists");
        }

        // Ensure GameStateSerializer exists
        if (GameStateSerializer.Instance == null)
        {
            Debug.Log("[NetworkBootstrap] Creating GameStateSerializer");

            GameObject serializerObj = new GameObject("GameStateSerializer");
            serializerObj.AddComponent<GameStateSerializer>();

            DontDestroyOnLoad(serializerObj);
        }
        else
        {
            Debug.Log("[NetworkBootstrap] GameStateSerializer already exists");
        }

        OnNetworkInitialized?.Invoke();
    }

    /// <summary>
    /// Initializes Firebase and checks dependencies.
    /// </summary>
    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            var status = task.Result;
            if (status == DependencyStatus.Available)
            {
                Debug.Log("[Firebase] Initialized successfully.");
            }
            else
            {
                Debug.LogError($"[Firebase] Initialization failed: {status}");
            }
        });
    }
}
