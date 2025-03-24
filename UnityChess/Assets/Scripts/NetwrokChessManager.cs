using UnityEngine;
using Unity.Netcode;
using UnityChess; // So we can access Square, GameManager, BoardManager, etc.

public class NetworkChessManager : NetworkBehaviour
{
    private void OnEnable()
    {
        // Subscribe to the VisualPieceMoved event
        VisualPiece.VisualPieceMoved += OnVisualPieceMoved;
    }

    private void OnDisable()
    {
        VisualPiece.VisualPieceMoved -= OnVisualPieceMoved;
    }

    /// <summary>
    /// Called whenever a piece is dropped in the scene (from VisualPiece.cs).
    /// We intercept the event and route it through server/client logic.
    /// </summary>
    private void OnVisualPieceMoved(Square startSquare, Transform pieceTransform, Transform closestSquareTransform, Piece promotionPiece)
    {
        // If I'm the server (Host), apply the move immediately, then tell clients.
        if (IsServer)
        {
            ApplyMoveServer(startSquare, pieceTransform, closestSquareTransform, promotionPiece);
        }
        else
        {
            // If I'm a client, request the server to apply the move.
            // We can’t directly send Transform in a ServerRpc, so we pass square data.
            RequestMoveServerRpc(startSquare.File, startSquare.Rank, closestSquareTransform.name);
        }
    }

    /// <summary>
    /// Runs on the server (Host) to apply the move with the existing GameManager logic.
    /// </summary>
    private void ApplyMoveServer(Square startSquare, Transform pieceTransform, Transform closestSquareTransform, Piece promotionPiece)
    {
        // Use your existing single-player logic
        GameManager.Instance.OnPieceMoved(startSquare, pieceTransform, closestSquareTransform, promotionPiece);

        // Then broadcast to all clients so they update piece positions
        FinalizeMoveClientRpc(startSquare.File, startSquare.Rank, closestSquareTransform.name);
    }

    /// <summary>
    /// Called by a client to request that the server apply a move.
    /// We pass the relevant data (file, rank, etc.) since we can’t send UnityEngine.Transform directly.
    /// </summary>
    [ServerRpc]
    private void RequestMoveServerRpc(int startFile, int startRank, string endSquareName)
    {
        // Retrieve the piece transform from the board manager
        Square startSquare = new Square(startFile, startRank);
        GameObject pieceGO = BoardManager.Instance.GetPieceGOAtPosition(startSquare);
        if (pieceGO == null) return; // In case something went wrong

        // Also retrieve the target square's Transform
        Square endSquare = new Square(endSquareName);
        Transform endTransform = BoardManager.Instance.GetSquareGOByPosition(endSquare).transform;

        // Use existing logic on the server
        ApplyMoveServer(startSquare, pieceGO.transform, endTransform, null);
    }

    /// <summary>
    /// Runs on every client (including the host) to finalize piece positions visually.
    /// </summary>
    [ClientRpc]
    private void FinalizeMoveClientRpc(int startFile, int startRank, string endSquareName)
    {
        // Find the piece on this client
        Square startSquare = new Square(startFile, startRank);
        GameObject pieceGO = BoardManager.Instance.GetPieceGOAtPosition(startSquare);
        if (pieceGO == null) return;

        // Move it to the target square
        Square endSquare = new Square(endSquareName);
        Transform endTransform = BoardManager.Instance.GetSquareGOByPosition(endSquare).transform;

        pieceGO.transform.SetParent(endTransform, true);
        pieceGO.transform.position = endTransform.position;
    }
}
