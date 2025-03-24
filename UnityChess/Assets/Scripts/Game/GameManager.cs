using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityChess;
using UnityEngine;

/// <summary>
/// Manages the overall game state, including game start, moves execution,
/// special moves handling (castling, en passant, promotion), and game reset.
/// Inherits from a singleton base class to ensure a single instance throughout the application.
/// </summary>
public class GameManager : MonoBehaviourSingleton<GameManager>
{
    // Events signalling various game state changes.
    public static event Action NewGameStartedEvent;
    public static event Action GameEndedEvent;
    public static event Action GameResetToHalfMoveEvent;
    public static event Action MoveExecutedEvent;

    /// <summary>
    /// The underlying Game (chess logic).
    /// </summary>
    private Game game;

    // Promotion handling
    private CancellationTokenSource promotionUITaskCancellationTokenSource;
    private ElectedPiece userPromotionChoice = ElectedPiece.None;

    // Serializers
    private FENSerializer fenSerializer;
    private PGNSerializer pgnSerializer;
    private Dictionary<GameSerializationType, IGameSerializer> serializersByType;
    private GameSerializationType selectedSerializationType = GameSerializationType.FEN;

    // Debug utility
    [SerializeField] private UnityChessDebug unityChessDebug;

    /// <summary>
    /// Gets the current board from the game timeline.
    /// </summary>
    public Board CurrentBoard
    {
        get
        {
            game.BoardTimeline.TryGetCurrent(out Board currentBoard);
            return currentBoard;
        }
    }

    /// <summary>
    /// Gets the side (White/Black) whose turn it is.
    /// </summary>
    public Side SideToMove
    {
        get
        {
            game.ConditionsTimeline.TryGetCurrent(out GameConditions conditions);
            return conditions.SideToMove;
        }
    }

    /// <summary>
    /// Gets the side that started the game (White or Black).
    /// </summary>
    public Side StartingSide => game.ConditionsTimeline[0].SideToMove;

    /// <summary>
    /// The timeline of half-moves made in the game.
    /// </summary>
    public Timeline<HalfMove> HalfMoveTimeline => game.HalfMoveTimeline;

    /// <summary>
    /// The index of the most recent half-move in the timeline.
    /// </summary>
    public int LatestHalfMoveIndex => game.HalfMoveTimeline.HeadIndex;

    /// <summary>
    /// Computes the full move number based on the starting side and the latest half-move index.
    /// </summary>
    public int FullMoveNumber
    {
        get
        {
            return StartingSide switch
            {
                Side.White => LatestHalfMoveIndex / 2 + 1,
                Side.Black => (LatestHalfMoveIndex + 1) / 2 + 1,
                _ => -1
            };
        }
    }

    /// <summary>
    /// Gets a list of all current pieces on the board, along with their positions.
    /// </summary>
    public List<(Square, Piece)> CurrentPieces
    {
        get
        {
            currentPiecesBacking.Clear();
            for (int file = 1; file <= 8; file++)
            {
                for (int rank = 1; rank <= 8; rank++)
                {
                    Piece piece = CurrentBoard[file, rank];
                    if (piece != null)
                        currentPiecesBacking.Add((new Square(file, rank), piece));
                }
            }
            return currentPiecesBacking;
        }
    }
    private readonly List<(Square, Piece)> currentPiecesBacking = new List<(Square, Piece)>();

    /// <summary>
    /// Unity Start method, initializes the game and sets up event handlers.
    /// </summary>
    public void Start()
    {
        VisualPiece.VisualPieceMoved += OnPieceMoved;

        // Initialize serializers for FEN and PGN formats
        serializersByType = new Dictionary<GameSerializationType, IGameSerializer>
        {
            [GameSerializationType.FEN] = new FENSerializer(),
            [GameSerializationType.PGN] = new PGNSerializer()
        };

        // Begin a new game
        StartNewGame();

#if DEBUG_VIEW
        unityChessDebug.gameObject.SetActive(true);
        unityChessDebug.enabled = true;
#endif
    }

    /// <summary>
    /// Starts a new game, creating a new Game instance and invoking NewGameStartedEvent.
    /// </summary>
    public async void StartNewGame()
    {
        game = new Game();
        NewGameStartedEvent?.Invoke();
    }

    /// <summary>
    /// Serializes the current game state using the selected serialization format.
    /// </summary>
    public string SerializeGame()
    {
        return serializersByType.TryGetValue(selectedSerializationType, out IGameSerializer serializer)
            ? serializer?.Serialize(game)
            : null;
    }

    /// <summary>
    /// Loads a game from the given serialized game state string.
    /// </summary>
    public void LoadGame(string serializedGame)
    {
        game = serializersByType[selectedSerializationType].Deserialize(serializedGame);
        NewGameStartedEvent?.Invoke();
    }

    /// <summary>
    /// Resets the game to a specific half-move index.
    /// </summary>
    public void ResetGameToHalfMoveIndex(int halfMoveIndex)
    {
        if (!game.ResetGameToHalfMoveIndex(halfMoveIndex)) return;

        UIManager.Instance.SetActivePromotionUI(false);
        promotionUITaskCancellationTokenSource?.Cancel();
        GameResetToHalfMoveEvent?.Invoke();
    }

    /// <summary>
    /// Determines if the specified piece has any legal moves.
    /// </summary>
    public bool HasLegalMoves(Piece piece)
    {
        return game.TryGetLegalMovesForPiece(piece, out _);
    }

    /// <summary>
    /// Handles the event triggered when a visual chess piece is moved (drag-and-drop).
    /// Validates the move, handles special moves, and updates the board state.
    /// </summary>
    public async void OnPieceMoved(Square movedPieceInitialSquare, Transform movedPieceTransform, Transform closestBoardSquareTransform, Piece promotionPiece = null)
    {
        // Convert the transform name to a Square
        Square endSquare = new Square(closestBoardSquareTransform.name);

        // Attempt to retrieve a legal move from the game logic
        if (!game.TryGetLegalMove(movedPieceInitialSquare, endSquare, out Movement move))
        {
            // If no legal move found, reset the piece's position
            movedPieceTransform.position = movedPieceTransform.parent.position;
#if DEBUG_VIEW
            // In debug view, log the legal moves for further analysis
            Piece movedPiece = CurrentBoard[movedPieceInitialSquare];
            if (movedPiece != null && game.TryGetLegalMovesForPiece(movedPiece, out var legalMoves))
            {
                UnityChessDebug.ShowLegalMovesInLog(legalMoves);
            }
#endif
            return;
        }

        // If the move is a promotion move, set the promotion piece
        if (move is PromotionMove promoMove)
        {
            promoMove.SetPromotionPiece(promotionPiece);
        }

        // If the move is not special OR special was handled, and the move executes successfully...
        if ((move is not SpecialMove specialMove || await TryHandleSpecialMoveBehaviourAsync(specialMove))
            && TryExecuteMove(move))
        {
            // For non-special moves, destroy any piece at the destination
            if (move is not SpecialMove) 
                BoardManager.Instance.TryDestroyVisualPiece(move.End);

            // For promotion, update the moved piece transform to the newly created visual piece
            if (move is PromotionMove)
            {
                movedPieceTransform = BoardManager.Instance.GetPieceGOAtPosition(move.End).transform;
            }

            // Re-parent the moved piece to the destination square and update its position
            movedPieceTransform.parent = closestBoardSquareTransform;
            movedPieceTransform.position = closestBoardSquareTransform.position;
        }
    }

    /// <summary>
    /// Attempts to execute a move in the game.
    /// </summary>
    private bool TryExecuteMove(Movement move)
    {
        // Attempt to execute within the game logic
        if (!game.TryExecuteMove(move)) 
            return false;

        // Retrieve the latest half-move
        HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);

        // If checkmate or stalemate, disable further moves
        if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate)
        {
            BoardManager.Instance.SetActiveAllPieces(false);
            GameEndedEvent?.Invoke();
        }
        else
        {
            // Otherwise, enable only the pieces for the side to move
            BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);
        }

        MoveExecutedEvent?.Invoke();
        return true;
    }

    /// <summary>
    /// Handles special move behavior asynchronously (castling, en passant, promotion).
    /// </summary>
    private async Task<bool> TryHandleSpecialMoveBehaviourAsync(SpecialMove specialMove)
    {
        switch (specialMove)
        {
            case CastlingMove castlingMove:
                BoardManager.Instance.CastleRook(castlingMove.RookSquare, castlingMove.GetRookEndSquare());
                return true;

            case EnPassantMove enPassantMove:
                BoardManager.Instance.TryDestroyVisualPiece(enPassantMove.CapturedPawnSquare);
                return true;

            case PromotionMove { PromotionPiece: null } promotionMove:
                // Activate the promotion UI and disable all pieces
                UIManager.Instance.SetActivePromotionUI(true);
                BoardManager.Instance.SetActiveAllPieces(false);

                // Cancel any pending promotion UI tasks
                promotionUITaskCancellationTokenSource?.Cancel();
                promotionUITaskCancellationTokenSource = new CancellationTokenSource();

                // Await user's promotion choice asynchronously
                ElectedPiece choice = await Task.Run(GetUserPromotionPieceChoice, promotionUITaskCancellationTokenSource.Token);

                // Deactivate the promotion UI and re-enable all pieces
                UIManager.Instance.SetActivePromotionUI(false);
                BoardManager.Instance.SetActiveAllPieces(true);

                // If the task was cancelled, return false
                if (promotionUITaskCancellationTokenSource == null 
                    || promotionUITaskCancellationTokenSource.Token.IsCancellationRequested)
                {
                    return false;
                }

                // Set the chosen promotion piece
                promotionMove.SetPromotionPiece(
                    PromotionUtil.GeneratePromotionPiece(choice, SideToMove)
                );

                // Update the board visuals for the promotion
                BoardManager.Instance.TryDestroyVisualPiece(promotionMove.Start);
                BoardManager.Instance.TryDestroyVisualPiece(promotionMove.End);
                BoardManager.Instance.CreateAndPlacePieceGO(promotionMove.PromotionPiece, promotionMove.End);

                promotionUITaskCancellationTokenSource = null;
                return true;

            case PromotionMove promotionMoveDone:
                BoardManager.Instance.TryDestroyVisualPiece(promotionMoveDone.Start);
                BoardManager.Instance.TryDestroyVisualPiece(promotionMoveDone.End);
                BoardManager.Instance.CreateAndPlacePieceGO(promotionMoveDone.PromotionPiece, promotionMoveDone.End);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Blocks until the user selects a piece for pawn promotion.
    /// </summary>
    private ElectedPiece GetUserPromotionPieceChoice()
    {
        while (userPromotionChoice == ElectedPiece.None) { }

        ElectedPiece result = userPromotionChoice;
        userPromotionChoice = ElectedPiece.None;
        return result;
    }

    /// <summary>
    /// Allows the user to elect a promotion piece.
    /// </summary>
    public void ElectPiece(ElectedPiece choice)
    {
        userPromotionChoice = choice;
    }
}
