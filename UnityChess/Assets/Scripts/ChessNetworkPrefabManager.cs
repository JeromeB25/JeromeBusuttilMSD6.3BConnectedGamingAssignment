using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityChess;

/// <summary>
/// Manages the spawning and despawning of networked chess pieces.
/// </summary>
public class ChessNetworkPrefabManager : NetworkBehaviour
{
    [SerializeField] private List<GameObject> whitePiecePrefabs = new List<GameObject>();
    [SerializeField] private List<GameObject> blackPiecePrefabs = new List<GameObject>();
    
    // Dictionary to map piece types to prefabs
    private Dictionary<string, GameObject> whitePieceMap = new Dictionary<string, GameObject>();
    private Dictionary<string, GameObject> blackPieceMap = new Dictionary<string, GameObject>();
    
    // Reference to instances for tracking
    private Dictionary<ulong, NetworkObject> spawnedPieces = new Dictionary<ulong, NetworkObject>();
    
    private void Awake()
    {
        // Initialize the piece type to prefab mappings
        foreach (GameObject prefab in whitePiecePrefabs)
        {
            VisualPiece visualPiece = prefab.GetComponent<VisualPiece>();
            if (visualPiece != null)
            {
                string pieceType = prefab.name.Replace("White ", "").Replace("(Clone)", "").Trim();
                whitePieceMap[pieceType] = prefab;
            }
        }
        
        foreach (GameObject prefab in blackPiecePrefabs)
        {
            VisualPiece visualPiece = prefab.GetComponent<VisualPiece>();
            if (visualPiece != null)
            {
                string pieceType = prefab.name.Replace("Black ", "").Replace("(Clone)", "").Trim();
                blackPieceMap[pieceType] = prefab;
            }
        }
    }
    
    private void Start()
    {
        // Subscribe to game events
        GameManager.NewGameStartedEvent += OnNewGameStarted;
        GameManager.GameResetToHalfMoveEvent += OnGameReset;
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        GameManager.NewGameStartedEvent -= OnNewGameStarted;
        GameManager.GameResetToHalfMoveEvent -= OnGameReset;
    }
    
    // Called when a new game is started
    private void OnNewGameStarted()
    {
        if (!IsServer) return;
        
        // Despawn all existing pieces
        DespawnAllPieces();
        
        // Spawn new pieces for the new game
        SpawnAllPieces();
    }
    
    // Called when the game is reset to a specific half-move
    private void OnGameReset()
    {
        if (!IsServer) return;
        
        // Despawn all existing pieces
        DespawnAllPieces();
        
        // Spawn pieces based on the current board state
        SpawnAllPieces();
    }
    
    // Spawns all pieces based on the current game state
    private void SpawnAllPieces()
    {
        if (!IsServer) return;
        
        foreach ((Square square, Piece piece) in GameManager.Instance.CurrentPieces)
        {
            SpawnPieceServerRpc(
                (byte)square.File, 
                (byte)square.Rank, 
                piece.GetType().Name, 
                piece.Owner == Side.White
            );
        }
    }
    
    // Despawns all currently spawned pieces
    private void DespawnAllPieces()
    {
        if (!IsServer) return;
        
        foreach (NetworkObject networkObject in spawnedPieces.Values)
        {
            if (networkObject != null && networkObject.IsSpawned)
            {
                networkObject.Despawn();
            }
        }
        
        spawnedPieces.Clear();
    }
    
    [ServerRpc]
    private void SpawnPieceServerRpc(byte file, byte rank, string pieceTypeName, bool isWhite)
    {
        // Get the appropriate prefab based on piece type and color
        GameObject prefab = isWhite ? 
            whitePieceMap.GetValueOrDefault(pieceTypeName) : 
            blackPieceMap.GetValueOrDefault(pieceTypeName);
            
        if (prefab == null)
        {
            Debug.LogError($"No prefab found for {(isWhite ? "White" : "Black")} {pieceTypeName}");
            return;
        }
        
        // Get the position on the board
        Square square = new Square(file, rank);
        Transform squareTransform = BoardManager.Instance.GetSquareGOByPosition(square).transform;
        
        // Instantiate and spawn the piece
        GameObject pieceInstance = Instantiate(prefab, squareTransform.position, Quaternion.identity);
        NetworkObject networkObject = pieceInstance.GetComponent<NetworkObject>();
        
        if (networkObject != null)
        {
            // Set parent before spawning
            pieceInstance.transform.SetParent(squareTransform);
            pieceInstance.transform.localPosition = Vector3.zero;
            
            // Configure the visual piece
            VisualPiece visualPiece = pieceInstance.GetComponent<VisualPiece>();
            if (visualPiece != null)
            {
                visualPiece.PieceColor = isWhite ? Side.White : Side.Black;
            }
            
            // Add network component if needed
            if (pieceInstance.GetComponent<VisualPieceNetwork>() == null)
            {
                pieceInstance.AddComponent<VisualPieceNetwork>();
            }
            
            // Spawn the piece
            networkObject.Spawn();
            
            // Keep track of spawned pieces
            ulong networkId = networkObject.NetworkObjectId;
            spawnedPieces[networkId] = networkObject;
        }
    }
}