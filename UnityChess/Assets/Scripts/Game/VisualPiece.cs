using System.Collections.Generic;
using UnityChess;
using UnityEngine;
using static UnityChess.SquareUtil;

/// <summary>
/// Represents a visual chess piece in the game. Handles drag-and-drop and movement.
/// </summary>
public class VisualPiece : MonoBehaviour
{
    public delegate void VisualPieceMovedAction(Square movedPieceInitialSquare, Transform movedPieceTransform, Transform closestBoardSquareTransform, Piece promotionPiece = null);
    public static event VisualPieceMovedAction VisualPieceMoved;

    public Side PieceColor;

    public Square CurrentSquare
    {
        get
        {
            if (transform.parent == null)
            {
                Debug.LogWarning($"{name} has no parent — cannot determine CurrentSquare.");
                return new Square(); // return default square (1,1) or handle differently if needed
            }

            return StringToSquare(transform.parent.name);
        }
    }

    private const float SquareCollisionRadius = 9f;

    private Camera boardCamera;
    private Vector3 piecePositionSS;
    private SphereCollider pieceBoundingSphere;
    private List<GameObject> potentialLandingSquares;
    private Transform thisTransform;

    private void Start()
    {
        potentialLandingSquares = new List<GameObject>();
        thisTransform = transform;
        boardCamera = Camera.main;
    }

    public void OnMouseDown()
    {
        if (!IsControllable()) return;

        piecePositionSS = boardCamera.WorldToScreenPoint(transform.position);
    }

    private void OnMouseDrag()
    {
        if (!IsControllable()) return;

        Vector3 nextPiecePositionSS = new Vector3(Input.mousePosition.x, Input.mousePosition.y, piecePositionSS.z);
        thisTransform.position = boardCamera.ScreenToWorldPoint(nextPiecePositionSS);
    }

    public void OnMouseUp()
    {
        if (!IsControllable()) return;

        potentialLandingSquares.Clear();
        BoardManager.Instance.GetSquareGOsWithinRadius(potentialLandingSquares, thisTransform.position, SquareCollisionRadius);

        if (potentialLandingSquares.Count == 0)
        {
            thisTransform.position = thisTransform.parent.position;
            return;
        }

        Transform closestSquareTransform = potentialLandingSquares[0].transform;
        float shortestDistanceSqr = (closestSquareTransform.position - thisTransform.position).sqrMagnitude;

        for (int i = 1; i < potentialLandingSquares.Count; i++)
        {
            Transform candidate = potentialLandingSquares[i].transform;
            float distanceSqr = (candidate.position - thisTransform.position).sqrMagnitude;

            if (distanceSqr < shortestDistanceSqr)
            {
                closestSquareTransform = candidate;
                shortestDistanceSqr = distanceSqr;
            }
        }

        VisualPieceMoved?.Invoke(CurrentSquare, thisTransform, closestSquareTransform);
    }

    /// <summary>
    /// Determines whether this piece can be interacted with.
    /// Currently: only the server (host) can move pieces.
    /// </summary>
    private bool IsControllable()
    {
        return Unity.Netcode.NetworkManager.Singleton.IsServer;
    }
}
