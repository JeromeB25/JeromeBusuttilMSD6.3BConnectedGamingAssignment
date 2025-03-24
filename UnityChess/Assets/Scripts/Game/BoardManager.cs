using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityChess;
using static UnityChess.SquareUtil;

public class BoardManager : MonoBehaviourSingleton<BoardManager>
{
    private readonly GameObject[] allSquaresGO = new GameObject[64];
    private Dictionary<Square, GameObject> positionMap;
    private Dictionary<Square, GameObject> pieceGOsBySquare = new Dictionary<Square, GameObject>();

    private const float BoardPlaneSideLength = 14f;
    private const float BoardPlaneSideHalfLength = BoardPlaneSideLength * 0.5f;
    private const float BoardHeight = 1.6f;

    private void Awake()
    {
        GameManager.NewGameStartedEvent += OnNewGameStarted;
        GameManager.GameResetToHalfMoveEvent += OnGameResetToHalfMove;

        positionMap = new Dictionary<Square, GameObject>(64);
        Transform boardTransform = transform;
        Vector3 boardPosition = boardTransform.position;

        for (int file = 1; file <= 8; file++)
        {
            for (int rank = 1; rank <= 8; rank++)
            {
                GameObject squareGO = new GameObject(SquareToString(file, rank))
                {
                    transform =
                    {
                        position = new Vector3(
                            boardPosition.x + FileOrRankToSidePosition(file),
                            boardPosition.y + BoardHeight,
                            boardPosition.z + FileOrRankToSidePosition(rank)
                        ),
                        parent = boardTransform
                    },
                    tag = "Square"
                };

                positionMap.Add(new Square(file, rank), squareGO);
                allSquaresGO[(file - 1) * 8 + (rank - 1)] = squareGO;
            }
        }
    }

    private void OnNewGameStarted()
    {
        ClearBoard();

        // ✅ Only the server should spawn network objects
        if (!NetworkManager.Singleton.IsServer) return;

        foreach ((Square square, Piece piece) in GameManager.Instance.CurrentPieces)
        {
            CreateAndPlacePieceGO(piece, square);
        }

        EnsureOnlyPiecesOfSideAreEnabled(GameManager.Instance.SideToMove);
    }

    private void OnGameResetToHalfMove()
    {
        ClearBoard();

        // ✅ Only the server should spawn network objects
        if (!NetworkManager.Singleton.IsServer) return;

        foreach ((Square square, Piece piece) in GameManager.Instance.CurrentPieces)
        {
            CreateAndPlacePieceGO(piece, square);
        }

        GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);
        if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate)
            SetActiveAllPieces(false);
        else
            EnsureOnlyPiecesOfSideAreEnabled(GameManager.Instance.SideToMove);
    }

    public void CreateAndPlacePieceGO(Piece piece, Square position)
    {
        if (!NetworkManager.Singleton.IsServer) return; // ✅ Server-only spawn

        string modelName = $"{piece.Owner} {piece.GetType().Name}";
        GameObject prefab = Resources.Load<GameObject>("PieceSets/Marble/" + modelName);
        if (prefab == null)
        {
            Debug.LogError($"Prefab not found for {modelName}");
            return;
        }

        Vector3 spawnPos = positionMap[position].transform.position;
        GameObject pieceGO = Instantiate(prefab, spawnPos, Quaternion.identity);

        NetworkObject netObj = pieceGO.GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsSpawned)
        {
            netObj.Spawn();
        }

        pieceGOsBySquare[position] = pieceGO;
    }

    public void CastleRook(Square rookPosition, Square endSquare)
    {
        if (!pieceGOsBySquare.TryGetValue(rookPosition, out GameObject rookGO)) return;

        rookGO.transform.position = positionMap[endSquare].transform.position;

        pieceGOsBySquare.Remove(rookPosition);
        pieceGOsBySquare[endSquare] = rookGO;
    }

    public void GetSquareGOsWithinRadius(List<GameObject> squareGOs, Vector3 positionWS, float radius)
    {
        float radiusSqr = radius * radius;
        foreach (GameObject squareGO in allSquaresGO)
        {
            if ((squareGO.transform.position - positionWS).sqrMagnitude < radiusSqr)
            {
                squareGOs.Add(squareGO);
            }
        }
    }

    public void SetActiveAllPieces(bool active)
    {
        foreach (var pieceGO in pieceGOsBySquare.Values)
        {
            VisualPiece vp = pieceGO.GetComponent<VisualPiece>();
            if (vp != null)
            {
                vp.enabled = active;
            }
        }
    }

    public void EnsureOnlyPiecesOfSideAreEnabled(Side side)
    {
        foreach (var kvp in pieceGOsBySquare)
        {
            Square square = kvp.Key;
            GameObject pieceGO = kvp.Value;

            VisualPiece vp = pieceGO.GetComponent<VisualPiece>();
            if (vp == null) continue;

            Piece piece = GameManager.Instance.CurrentBoard[square];
            bool canMove = GameManager.Instance.HasLegalMoves(piece);

            vp.enabled = (vp.PieceColor == side) && canMove;
        }
    }

    public void TryDestroyVisualPiece(Square position)
    {
        if (pieceGOsBySquare.TryGetValue(position, out GameObject pieceGO))
        {
            DestroyImmediate(pieceGO);
            pieceGOsBySquare.Remove(position);
        }
    }

    public GameObject GetPieceGOAtPosition(Square position)
    {
        pieceGOsBySquare.TryGetValue(position, out GameObject pieceGO);
        return pieceGO;
    }

    public GameObject GetSquareGOByPosition(Square position) =>
        Array.Find(allSquaresGO, go => go.name == SquareToString(position));

    private void ClearBoard()
    {
        foreach (var kvp in pieceGOsBySquare)
        {
            DestroyImmediate(kvp.Value);
        }
        pieceGOsBySquare.Clear();
    }

    private static float FileOrRankToSidePosition(int index)
    {
        float t = (index - 1) / 7f;
        return Mathf.Lerp(-BoardPlaneSideHalfLength, BoardPlaneSideHalfLength, t);
    }
}
