using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using static System.Formats.Asn1.AsnWriter;

public class MyBot : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        int[] testTable = { 1, 2, 3, 1 };
        decimal testCompressed = compressSByteArray(testTable);
        int[] testUncompressed = testUncompressPieceSquareTable(testCompressed);
        foreach (int item in testUncompressed) Console.WriteLine(item);
        StartSearch(board, timer);
        return bestMovesByDepth[0];
    }

    // Can save a lot of tokens by hardcoding these values in
    const int immediateMateScore = 100000;
    const int positiveInfinity = 9999999;
    const int negativeInfinity = -positiveInfinity;
    const int maxMillisecondsPerSearch = 1500;

    // Store timer and board references to simplify function signatures
    private Timer timer;
    private Board board;

    List<Move> bestMovesByDepth;
    bool isSearchCancelled;


    void StartSearch(Board _board, Timer _timer)
    {
        board = _board;
        timer = _timer;

        bestMovesByDepth = new List<Move>();
        isSearchCancelled = false;

        Console.WriteLine("---"); // debug
        for (int searchDepth = 1; !isSearchCancelled; searchDepth++)
        {
            bestMovesByDepth.Add(Move.NullMove);
            Search(searchDepth, 0, negativeInfinity, positiveInfinity);

            Console.WriteLine(searchDepth); // debug
        }
    }

    int Search(int depth, int plyFromRoot, int alpha, int beta)
    {
        // Cancel the search if we are out of time
        isSearchCancelled = timer.MillisecondsElapsedThisTurn > maxMillisecondsPerSearch;
        if (isSearchCancelled) return 0;

        // Check for Checkmate before we do anything else.
        if (board.IsInCheckmate()) return -immediateMateScore + plyFromRoot;


        // Once we reach target depth, search all captures to make the evaluation more accurate
        if (depth == 0) return QuiescenceSearch(alpha, beta);

        Span<Move> moves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref moves);

        // Stalemate Check
        if (moves.Length == 0) return 0;

        // Order the moves, making sure to put the best move from the previous iteration first
        Sort(ref moves, bestMovesByDepth[plyFromRoot]);

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int eval = -Search(depth - 1, plyFromRoot + 1, -beta, -alpha);
            board.UndoMove(move);

            if (eval >= beta) return beta;
            if (eval > alpha)
            {
                alpha = eval;
                bestMovesByDepth[plyFromRoot] = move;

                // Saved creation of a variable by moving the checkmate check to here.
                // There is the possibility that this stops the search before the computer finds a quicker checkmate.
                // Worth it to give up some tokens?
                if (plyFromRoot == 0 && Math.Abs(eval) > immediateMateScore - 1000) isSearchCancelled = true;
            }
        }

        return alpha;
    }

    int QuiescenceSearch(int alpha, int beta)
    {
        int eval = Evaluate();
        if (eval >= beta) return beta;
        alpha = Math.Max(alpha, eval);

        // Order the moves
        Span<Move> moves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref moves, !board.IsInCheck());
        Sort(ref moves, default);

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            eval = -QuiescenceSearch(-beta, -alpha);
            board.UndoMove(move);

            if (eval >= beta) return beta;
            alpha = Math.Max(alpha, eval);
        }

        return alpha;
    }

    void Sort(ref Span<Move> moves, Move putThisFirst)
    {
        Span<int> sortKeys = stackalloc int[moves.Length];
        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];
            sortKeys[i] = move switch
            {
                // 1. Priority Move
                _ when move == putThisFirst => 0,
                // 2. Promotion
                { IsPromotion: true } => 1,
                // 3. Captures
                { IsCapture: true } => 1000 - 10 * (int)move.CapturePieceType + (int)move.MovePieceType,
                _ => 1001
            };
        }
        sortKeys.Sort(moves);
    }


    #region Evalution

    //Represent the rank scores as a 64-bit int. Last couple rows are all copies
    static readonly ulong[] kingMidgameTable = new ulong[]
    {
        0b_00010100_00011110_00001010_00000000_00000000_00001010_00011110_00010100L,
        0b_00010100_00010100_00000000_00000000_00000000_00000000_00010100_00010100L,
        0b_11110110_11101100_11101100_11101100_11101100_11101100_11101100_11110110L,
        0b_11101100_11100010_11100010_11011000_11011000_11100010_11100010_11101100L,
        0b_11100010_11011000_11011000_11001110_11001110_11011000_11011000_11100010L
    };

    readonly ulong[] kingEndgameTable = new ulong[]
    {
        0b_11100010_11110110_00011110_00101000_00101000_00011110_11110110_11100010L,
        0b_11100010_11110110_00010100_00011110_00011110_00010100_11110110_11100010L,
        0b_11100010_11100010_00000000_00000000_00000000_00000000_11100010_11100010L,
        0b_11001110_11100010_11100010_11100010_11100010_11100010_11100010_11001110L,
    };

    #region HELPER FUNCTIONS FOR DATA COMPRESSION

    private decimal compressSByteArray(int[] values)
    {
        decimal result = 0;
        for (int i = 0; i < values.Length; i++)
        {
            result += (sbyte)values[i] << 8 * i;
        }
        return result;
    }

    private int[] testUncompressPieceSquareTable(decimal compressed)
    {
        return decimal.GetBits(compressed).Where((_, i) => i % 4 != 3).SelectMany(BitConverter.GetBytes).Select(t => (int)(sbyte)t).ToArray();
    }

    private int[] partialCompressPieceSquareTable(sbyte[] table)
    {
        Debug.Assert(table.Length == 64);
        int[] result = new int[16];
        for (int i = 0; i < 64; i++)
        {
            int arrayLoc = i / 4;
            result[arrayLoc] += table[i] << 8 * i;
        }
        return result;
    }

    private decimal[] compressIntArrayToDecimals(int[] values)
    {
        Debug.Assert(values.Length % 3 == 0);
        decimal[] result = new decimal[values.Length / 3];
        for (int i = 0; i < values.Length; i += 3)
        {
            result[i / 3] = new decimal(new int[]{ values[i], values[i + 1], values[i + 2], 0 });
        }
        return result;
    }

    #endregion

    // Performs static evaluation of the current position.
    // The position is assumed to be 'quiet', i.e no captures are available that could drastically affect the evaluation.
    // The score that's returned is given from the perspective of whoever's turn it is to move.
    // So a positive score means the player who's turn it is to move has an advantage, while a negative score indicates a disadvantage.
    public int Evaluate()
    {
        Square whiteKingSquare = board.GetKingSquare(true);
        Square blackKingSquare = board.GetKingSquare(false);

        //Mobility
        int mobility = GetMobilityBonus();
        if (board.TrySkipTurn())
        {
            mobility -= GetMobilityBonus();
            board.UndoSkipTurn();
        }
        else mobility = 0; // ignore mobility if we can't get it for both sides


        return (CountMaterial(true) - CountMaterial(false)
            + GetKingSafetyScores(whiteKingSquare.File, whiteKingSquare.Rank, EndgamePhaseWeight(true))
            - GetKingSafetyScores(blackKingSquare.File, 7 - blackKingSquare.Rank, EndgamePhaseWeight(false))
            + GetEndgameBonus(true)
            - GetEndgameBonus(false))
            * (board.IsWhiteToMove ? 1 : -1)
            + mobility;
    }


    int[] POINT_VALUES = { 100, 350, 350, 525, 1000 };
    int GetPointValue(PieceType type)
    {
        switch (type)
        {
            case PieceType.None: return 0;
            case PieceType.King: return positiveInfinity;
            default: return POINT_VALUES[(int)type - 1];
        }
    }

    float EndgamePhaseWeight(bool isWhite)
    {
        return 1 - Math.Min(1, (CountMaterial(isWhite) - board.GetPieceList(PieceType.Pawn, isWhite).Count * 100) / 1750);
    }

    int GetMobilityBonus()
    {
        int mobility = 0;
        foreach (Move move in board.GetLegalMoves())
        {
            switch (move.MovePieceType)
            {
                case PieceType.Knight:
                    mobility += 100; // More points for knight since it has a smaller maximum of possible moves
                    break;
                case PieceType.Bishop:
                    mobility += 5;
                    break;
                case PieceType.Rook:
                    mobility += 6;
                    break;
                case PieceType.Queen:
                    mobility += 4;
                    break;
            }
        }
        return mobility;
    }

    int GetKingSafetyScores(int file, int relativeRank, float endgameWeight)
    {
        sbyte midgameScore = (sbyte)((kingMidgameTable[Math.Min(relativeRank, 4)] >> file * 8) % 256);
        return (int)(midgameScore + (midgameScore - (sbyte)((kingEndgameTable[(int)Math.Abs(3.5 - relativeRank)] >> file * 8) % 256)) * endgameWeight);
    }


    int CountMaterial(bool isWhite)
    {
        return board.GetPieceList(PieceType.Pawn, isWhite).Count * 100
            + board.GetPieceList(PieceType.Knight, isWhite).Count * 350
            + board.GetPieceList(PieceType.Bishop, isWhite).Count * 350
            + board.GetPieceList(PieceType.Rook, isWhite).Count * 525
            + board.GetPieceList(PieceType.Queen, isWhite).Count * 1000;
    }

    int GetEndgameBonus(bool isWhite)
    {
        float enemyEndgameWeight = EndgamePhaseWeight(!isWhite);
        if (enemyEndgameWeight <= 0) return 0;
        ulong ourBB = isWhite ? board.WhitePiecesBitboard : board.BlackPiecesBitboard;
        Square enemyKingSquare = board.GetKingSquare(!isWhite);

        int endgameBonus = 0;
        while (ourBB != 0)
        {
            Square pieceSquare = new Square(BitboardHelper.ClearAndGetIndexOfLSB(ref ourBB));
            switch (board.GetPiece(pieceSquare).PieceType)
            {
                case PieceType.Pawn:
                    // Encourage pawns to move forward
                    endgameBonus += 50 - 10 * (isWhite ? 7 - pieceSquare.Rank : pieceSquare.Rank);
                    break;
                case PieceType.Rook:
                    //Encourage rooks to get close to the same rank/file as the king
                    endgameBonus += 50 - 10 * Math.Min(Math.Abs(enemyKingSquare.File - pieceSquare.File), Math.Abs(enemyKingSquare.Rank - pieceSquare.Rank));
                    break;
                default:
                    // In general, we want to get our pieces closer to the enemy king, will give us a better chance of finding a checkmate.
                    // Use power growth so we prioritive
                    endgameBonus += 50 - (int)(10 * Math.Pow(Math.Max(Math.Abs(enemyKingSquare.File - pieceSquare.File), Math.Abs(enemyKingSquare.Rank - pieceSquare.Rank)), 1.5));
                    break;
            }
        }

        return (int)(endgameBonus * enemyEndgameWeight);
    }

    #endregion
}