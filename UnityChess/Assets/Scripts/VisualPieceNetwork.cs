using UnityChess;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Extends VisualPiece functionality to work with networking.
/// Should be attached to the same GameObject as VisualPiece.
/// </summary>
public class VisualPieceNetwork : NetworkBehaviour
{
    private VisualPiece visualPiece;
    
    private void Awake()
    {
        visualPiece = GetComponent<VisualPiece>();
    }
    
    private void Start()
    {
        // Subscribe to turn change events to update piece interactivity
        GameManager.MoveExecutedEvent += UpdatePieceInteractivity;
        
        // Initial update
        UpdatePieceInteractivity();
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        GameManager.MoveExecutedEvent -= UpdatePieceInteractivity;
    }
    
    private void UpdatePieceInteractivity()
    {
        if (visualPiece == null || NetworkManager.Singleton == null)
            return;
            
        // Only enable pieces if:
        // 1. We are connected to a network
        // 2. The piece belongs to the local player
        // 3. It's the local player's turn
        
        if (!NetworkManager.Singleton.IsConnectedClient)
        {
            // Not connected, use default behavior (allow local play)
            return;
        }
        
        // Get local player side
        Side localPlayerSide = Side.White;
        if (ChessNetworkManager.Instance != null)
        {
            localPlayerSide = ChessNetworkManager.Instance.GetLocalPlayerSide();
        }
        
        Side currentTurn = GameManager.Instance.SideToMove;
        
        // Determine if this piece should be interactive
        bool belongsToLocalPlayer = visualPiece.PieceColor == localPlayerSide;
        bool isLocalPlayerTurn = currentTurn == localPlayerSide;
        bool hasLegalMoves = GameManager.Instance.HasLegalMoves(GameManager.Instance.CurrentBoard[visualPiece.CurrentSquare]);
        
        // Enable/disable the piece accordingly
        bool shouldBeEnabled = belongsToLocalPlayer && isLocalPlayerTurn && hasLegalMoves;
        
        // Only update if needed
        if (visualPiece.enabled != shouldBeEnabled)
        {
            visualPiece.enabled = shouldBeEnabled;
            //Debug.Log($"Set {gameObject.name} interactive: {shouldBeEnabled} (side: {visualPiece.PieceColor}, turn: {currentTurn})");
        }
    }
}