using UnityChess;
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

/// <summary>
/// Extension to the GameManager to handle network functionality.
/// This script should be added alongside the existing GameManager.
/// </summary>
public class GameManagerNetworkExtension : NetworkBehaviour
{
    // Reference to the main GameManager
    private GameManager gameManager;
    
    // Store local player's side
    private Side localPlayerSide = Side.White;
    
    // Network variables to synchronize game state
    private NetworkVariable<bool> isGameStarted = new NetworkVariable<bool>(false);
    private NetworkVariable<int> currentHalfMoveIndex = new NetworkVariable<int>(0);
    
    // Flag to prevent move processing during synchronization
    private bool isSynchronizing = false;
    
    private void Start()
    {
        // Get the reference to the GameManager
        gameManager = GameManager.Instance;
        
        Debug.Log("GameManagerNetworkExtension starting...");
        
        // Subscribe to GameManager events to handle network synchronization
        VisualPiece.VisualPieceMoved += OnVisualPieceMovedLocal;
        GameManager.NewGameStartedEvent += OnNewGameStartedLocal;
        
        // Subscribe to network events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
        else
        {
            Debug.LogWarning("NetworkManager singleton is not available yet in GameManagerNetworkExtension");
        }
        
        // Subscribe to NetworkVariable changes
        isGameStarted.OnValueChanged += OnGameStartedValueChanged;
        currentHalfMoveIndex.OnValueChanged += OnCurrentHalfMoveIndexChanged;
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log($"GameManagerNetworkExtension spawned. IsServer: {IsServer}, IsClient: {IsClient}, IsOwner: {IsOwner}");
        
        // If this is a client connecting to an existing game, request the current state
        if (IsClient && !IsServer)
        {
            Debug.Log("Client requesting game state from server");
            RequestGameStateServerRpc();
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        VisualPiece.VisualPieceMoved -= OnVisualPieceMovedLocal;
        GameManager.NewGameStartedEvent -= OnNewGameStartedLocal;
        
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
        
        // Unsubscribe from NetworkVariable changes
        isGameStarted.OnValueChanged -= OnGameStartedValueChanged;
        currentHalfMoveIndex.OnValueChanged -= OnCurrentHalfMoveIndexChanged;
    }
    
    private void OnClientConnected(ulong clientId)
    {
        // When a client connects, get the side from the ChessNetworkManager
        if (ChessNetworkManager.Instance != null)
        {
            localPlayerSide = ChessNetworkManager.Instance.GetLocalPlayerSide();
            Debug.Log($"Network client connected. Assigned side: {localPlayerSide}");
        }
        
        // If this is the server, synchronize the current game state to the new client
        if (IsServer && clientId != NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log($"Server sending game state to newly connected client {clientId}");
            SynchronizeGameStateForClientRpc(gameManager.SerializeGame(), 
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
        }
    }
    
    // Called when a piece is moved locally
    private void OnVisualPieceMovedLocal(Square movedPieceInitialSquare, Transform movedPieceTransform, Transform closestBoardSquareTransform, Piece promotionPiece = null)
    {
        // Skip if we're in synchronization mode or not connected
        if (isSynchronizing || !NetworkManager.Singleton.IsConnectedClient)
            return;
            
        // Check if it's the local player's turn
        Side currentSideToMove = gameManager.SideToMove;
        if (currentSideToMove != localPlayerSide)
        {
            Debug.Log($"Not your turn. Current turn: {currentSideToMove}, Your side: {localPlayerSide}");
            return;
        }
        
        // Get the destination square
        Square endSquare = new Square(closestBoardSquareTransform.name);
        
        // Send the move over the network
        MoveDataStruct moveData = new MoveDataStruct
        {
            startFile = (byte)movedPieceInitialSquare.File,
            startRank = (byte)movedPieceInitialSquare.Rank,
            endFile = (byte)endSquare.File,
            endRank = (byte)endSquare.Rank,
            hasPromotionPiece = promotionPiece != null,
            promotionPieceType = promotionPiece != null ? (byte)GetPromotionPieceType(promotionPiece) : (byte)0
        };
        
        Debug.Log($"Sending move: {movedPieceInitialSquare} -> {endSquare}");
        
        // Send the move to the server
        SendMoveServerRpc(moveData);
    }
    
    // Called when a new game is started locally
    private void OnNewGameStartedLocal()
    {
        // Only if we're the server, notify all clients
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            Debug.Log("Server starting a new game, notifying clients");
            isGameStarted.Value = true;
            StartNewGameClientRpc();
        }
    }
    
    // Helper method to determine the type of promotion piece
    private ElectedPiece GetPromotionPieceType(Piece piece)
    {
        if (piece is Queen) return ElectedPiece.Queen;
        if (piece is Rook) return ElectedPiece.Rook;
        if (piece is Bishop) return ElectedPiece.Bishop;
        if (piece is Knight) return ElectedPiece.Knight;
        
        return ElectedPiece.Queen; // Default
    }
    
    #region Server RPCs
    
    [ServerRpc(RequireOwnership = false)]
    private void RequestGameStateServerRpc(ServerRpcParams serverRpcParams = default)
    {
        if (!IsServer) return;
        
        ulong clientId = serverRpcParams.Receive.SenderClientId;
        Debug.Log($"Server received request for game state from client {clientId}");
        
        // Send the current game state to the requesting client
        string gameState = gameManager.SerializeGame();
        int currentIndex = gameManager.LatestHalfMoveIndex;
        
        SynchronizeGameStateForClientRpc(gameState, 
            new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
            
        // Update the client with the current move index
        SynchronizeHalfMoveIndexClientRpc(currentIndex);
    }
    
    // RPC to send a move to the server
    [ServerRpc(RequireOwnership = false)]
    private void SendMoveServerRpc(MoveDataStruct moveData, ServerRpcParams serverRpcParams = default)
    {
        // Get sender info
        ulong senderId = serverRpcParams.Receive.SenderClientId;
        Side senderSide = ChessNetworkManager.Instance.GetPlayerSide(senderId);
        
        Debug.Log($"Server received move from client {senderId} with side {senderSide}");
        
        // Validate that the move came from a player whose turn it is
        if (gameManager.SideToMove != senderSide)
        {
            Debug.LogWarning($"Received move from client {senderId} but it's not their turn!");
            return;
        }
        
        // Forward the move to all clients
        BroadcastMoveClientRpc(moveData);
        
        // Update the current half move index after the move is processed
        currentHalfMoveIndex.Value = gameManager.LatestHalfMoveIndex;
    }
    
    #endregion
    
    #region Client RPCs
    
    // RPC to broadcast a move to all clients
    [ClientRpc]
    private void BroadcastMoveClientRpc(MoveDataStruct moveData)
    {
        // Skip processing if we're synchronizing or if this is the server broadcasting to itself
        if (isSynchronizing || (IsServer && IsOwner && NetworkManager.Singleton.IsHost))
            return;
            
        Debug.Log($"Client received broadcasted move: {moveData.startFile},{moveData.startRank} -> {moveData.endFile},{moveData.endRank}");
            
        // Create the squares from the move data
        Square startSquare = new Square(moveData.startFile, moveData.startRank);
        Square endSquare = new Square(moveData.endFile, moveData.endRank);
        
        // Find the piece GameObject at the start position
        GameObject pieceGO = BoardManager.Instance.GetPieceGOAtPosition(startSquare);
        if (pieceGO == null)
        {
            Debug.LogError($"No piece found at {startSquare}");
            return;
        }
        
        // Get the piece's transform
        Transform pieceTransform = pieceGO.transform;
        
        // Get the destination square's transform
        Transform destSquareTransform = BoardManager.Instance.GetSquareGOByPosition(endSquare).transform;
        
        // Handle promotion if needed
        Piece promotionPiece = null;
        if (moveData.hasPromotionPiece)
        {
            // Create the appropriate promotion piece based on the type
            ElectedPiece electedPiece = (ElectedPiece)moveData.promotionPieceType;
            promotionPiece = PromotionUtil.GeneratePromotionPiece(electedPiece, gameManager.SideToMove);
        }
        
        // Important: On clients, DON'T change parent directly - this causes errors
        // Only update position visually, let the game logic handle the rest
        if (IsServer)
        {
            // On server, we can still set the parent-child relationship
            pieceTransform.parent = destSquareTransform;
            pieceTransform.position = destSquareTransform.position;
        }
        else
        {
            // On client, just update position
            pieceTransform.position = destSquareTransform.position;
            
            // Let the game know about the move via Unity event system
            // This will update the game state correctly without changing parents directly
            if (pieceGO.TryGetComponent<VisualPiece>(out var visualPiece))
            {
                visualPiece.OnMouseUp();
            }
        }
        
        // If there's a promotion piece, handle it
        if (moveData.hasPromotionPiece)
        {
            // Elect the piece
            gameManager.ElectPiece((ElectedPiece)moveData.promotionPieceType);
        }
    }
    
    // RPC to start a new game on all clients
    [ClientRpc]
    private void StartNewGameClientRpc()
    {
        // Skip if this is the server (host) as it already started the game
        if (IsServer && IsOwner)
            return;
            
        Debug.Log("Client received new game notification");
        
        // Start a new game
        isSynchronizing = true;
        gameManager.StartNewGame();
        isSynchronizing = false;
    }
    
    // RPC to synchronize the full game state to a specific client
    [ClientRpc]
    private void SynchronizeGameStateForClientRpc(string gameState, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"Client received game state: {gameState.Substring(0, Mathf.Min(30, gameState.Length))}...");
        
        // Skip if this is the server
        if (IsServer && IsOwner)
            return;
            
        // Load the game from the serialized state
        isSynchronizing = true;
        gameManager.LoadGame(gameState);
        isSynchronizing = false;
    }
    
    // RPC to synchronize the half-move index to clients
    [ClientRpc]
    private void SynchronizeHalfMoveIndexClientRpc(int halfMoveIndex)
    {
        // Skip if this is the server
        if (IsServer && IsOwner)
            return;
            
        Debug.Log($"Client received half-move index: {halfMoveIndex}");
        
        // Reset the game to the specified half-move index
        isSynchronizing = true;
        gameManager.ResetGameToHalfMoveIndex(halfMoveIndex);
        isSynchronizing = false;
    }
    
    #endregion
    
    #region NetworkVariable Callbacks
    
    // Called when the isGameStarted NetworkVariable changes
    private void OnGameStartedValueChanged(bool oldValue, bool newValue)
    {
        if (newValue && !IsServer)
        {
            Debug.Log("Client detected game started from network variable change");
            
            // A new game was started by the server
            isSynchronizing = true;
            gameManager.StartNewGame();
            isSynchronizing = false;
        }
    }
    
    // Called when the currentHalfMoveIndex NetworkVariable changes
    private void OnCurrentHalfMoveIndexChanged(int oldValue, int newValue)
    {
        // Skip if this is the server
        if (IsServer)
            return;
            
        Debug.Log($"Half move index changed from {oldValue} to {newValue}");
        
        // Reset the game to the specified half-move index
        isSynchronizing = true;
        gameManager.ResetGameToHalfMoveIndex(newValue);
        isSynchronizing = false;
    }
    
    #endregion
}

// Struct to hold move data for network transmission
public struct MoveDataStruct : INetworkSerializable
{
    public byte startFile;
    public byte startRank;
    public byte endFile;
    public byte endRank;
    public bool hasPromotionPiece;
    public byte promotionPieceType;
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref startFile);
        serializer.SerializeValue(ref startRank);
        serializer.SerializeValue(ref endFile);
        serializer.SerializeValue(ref endRank);
        serializer.SerializeValue(ref hasPromotionPiece);
        serializer.SerializeValue(ref promotionPieceType);
    }
}