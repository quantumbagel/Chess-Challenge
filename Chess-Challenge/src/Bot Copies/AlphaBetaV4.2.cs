using ChessChallenge.API;
using System;
using System.Linq;

public class AlphaBetaV4_2 : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        StartSearch(board, timer);
        return bestMovesByDepth[0];
    }

    // Store timer and board references to simplify function signatures
    private Timer timer;
    private Board board;

    Move[] bestMovesByDepth;
    int bestEval;
    bool isSearchCancelled;


    void StartSearch(Board _board, Timer _timer)
    {
        board = _board;
        timer = _timer;

        bestMovesByDepth = new Move[256];
        bestEval = 0;
        isSearchCancelled = false;

        for (int searchDepth = 1; !isSearchCancelled; searchDepth++)
        {
            // Use really large values to guarantee initial sets
            Search(searchDepth, 0, -9999999, 9999999);

            // Checkmate has been found
            if (Math.Abs(bestEval) > 99000) break;
        }
    }

    int Search(int depth, int plyFromRoot, int alpha, int beta)
    {
        // Cancel the search if we are out of time.
        isSearchCancelled = 30 * timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining;
        if (isSearchCancelled || board.IsRepeatedPosition()) return 0;

        // Check for Checkmate before we do anything else.
        if (board.IsInCheckmate()) return -100000 + plyFromRoot;


        // Once we reach target depth, search only captures to make the evaluation more accurate
        // Also if we're in check, add to depth so we make sure we don't screw ourselves
        if (depth == 0)
        {
            if (board.IsInCheck()) depth++;
            else return QuiescenceSearch(alpha, beta);
        }

        Span<Move> moves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref moves);

        // Stalemate Check
        if (moves.Length == 0) return 0;

        // Order the moves, making sure to put the best move from the previous iteration first
        Sort(ref moves, bestMovesByDepth[plyFromRoot]);

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            // extend if we put the king in check
            int eval = -Search(depth - 1, plyFromRoot + 1, -beta, -alpha);
            board.UndoMove(move);

            if (eval >= beta) return beta; // *snip* :D
            if (eval > alpha)
            {
                alpha = eval;
                bestMovesByDepth[plyFromRoot] = move;
                bestEval = plyFromRoot == 0 ? eval : bestEval;
            }
        }

        return alpha;
    }

    int QuiescenceSearch(int alpha, int beta)
    {
        int eval = Evaluate();
        if (eval >= beta) return beta; // *snip* :D
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

            if (eval >= beta) return beta; // *snip* :D
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
                // 4. Prioritize moves with deep beta cutoffs; TODO
                _ => 100_000_000
            };
        }
        sortKeys.Sort(moves);
    }


    #region Evalution

    /*
    sbyte[] kingMidgameTableSBytes = new sbyte[]
    {
        20, 30, 10, 0, 0, 10, 30, 20,
        20, 20, 0, 0, 0, 0, 20, 20,
        -10, -20, -20, -20, -20, -20, -20, -10,
        -20, -30, -30, -40, -40, -30, -30, -20,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
    };

    sbyte[] kingEndgameTableSBytes = new sbyte[]
    {
        -50, -30, -30, -30, -30, -30, -30, -50,
        -30, -30, 0, 0, 0, 0, -30, -30,
        -30, -10, 20, 30, 30, 20, -10, -30,
        -30, -10, 30, 40, 40, 30, -10, -30,
        -30, -10, 30, 40, 40, 30, -10, -30,
        -30, -10, 20, 30, 30, 20, -10, -30,
        -30, -30, 0, 0, 0, 0, -30, -30,
        -50, -30, -30, -30, -30, -30, -30, -50,
    };

    //Represent the rank scores as a 64-bit int. Last couple rows are all copies
    static readonly ulong[] kingMidgameTable = new ulong[]
    {
        0b_00010100_00011110_00001010_00000000_00000000_00001010_00011110_00010100L,
        0b_00010100_00010100_00000000_00000000_00000000_00000000_00010100_00010100L,
        0b_11110110_11101100_11101100_11101100_11101100_11101100_11101100_11110110L,
        0b_11101100_11100010_11100010_11011000_11011000_11100010_11100010_11101100L,
        0b_11100010_11011000_11011000_11001110_11001110_11011000_11011000_11100010L
    };

    static readonly ulong[] kingEndgameTable = new ulong[]
    {
        0b_11100010_11110110_00011110_00101000_00101000_00011110_11110110_11100010L,
        0b_11100010_11110110_00010100_00011110_00011110_00010100_11110110_11100010L,
        0b_11100010_11100010_00000000_00000000_00000000_00000000_11100010_11100010L,
        0b_11001110_11100010_11100010_11100010_11100010_11100010_11100010_11001110L,
    };
    */

    static readonly decimal[] compressedKingSquareTables = new decimal[]
    {
        94817714145992272125460m, 76419737758473778852349083648m, 64016064217427759519433417452m, 70205764042755225896833571022m,
        64016064216704357791052650722m, 64028200698568158524470778062m, 9309894698500220833627169506m, 70241150383004452811665970206m,
        9309894698505883490981967586m, 70216829454857141189382378526m, 14907727180646769358m,
    };

    static readonly int[] kingSquareTables = compressedKingSquareTables.SelectMany(decimal.GetBits).Where((_, i) => i % 4 != 3).SelectMany(BitConverter.GetBytes).Select(t => (int)(sbyte)t).ToArray();

    /*
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

    private int[] testUncompressPieceSquareTable(decimal[] compressed)
    {
        return compressed.SelectMany(decimal.GetBits).Where((_, i) => i % 4 != 3).SelectMany(BitConverter.GetBytes).Select(t => (int)(sbyte)t).ToArray();
    }

    private int[] partialCompressPieceSquareTable(sbyte[] table)
    {
        Debug.Assert(table.Length == 64);
        int[] result = new int[16];
        for (int i = 0; i < 64; i++)
        {
            int arrayLoc = i / 4;
            result[arrayLoc] += (byte)table[i] << 8 * i;
        }
        return result;
    }

    private decimal[] compressIntArrayToDecimals(int[] values)
    {
        int resultLength = (int)Math.Ceiling(values.Length / 3.0);
        decimal[] result = new decimal[resultLength];
        int[] constructor = new int[4];
        for (int i = 0; i < values.Length; i++)
        {
            constructor[i % 3] = values[i];
            if (i % 3 == 2)
            {
                result[i / 3] = new decimal(constructor);
                constructor = new int[4];
            }
        }
        // Fill last decimal if there is any data left over.
        if (values.Length % 3 != 0) result[resultLength - 1] = new decimal(constructor);

        return result;
    }

    #endregion
    //*/

    // Performs static evaluation of the current position.
    // The position is assumed to be 'quiet', i.e no captures are available that could drastically affect the evaluation.
    // The score that's returned is given from the perspective of whoever's turn it is to move.
    // So a positive score means the player who's turn it is to move has an advantage, while a negative score indicates a disadvantage.
    public int Evaluate()
    {
        //Mobility; Maybe try to move the mobility bonus to the search function since it already looks at all available moves.
        int mobility = GetMobilityBonus();
        if (board.TrySkipTurn())
        {
            mobility -= GetMobilityBonus();
            board.UndoSkipTurn();
        }
        else mobility = 0; // ignore mobility if we can't get it for both sides

        return (CountMaterial(true) - CountMaterial(false)
            + GetKingSafetyScores(board.GetKingSquare(true).Index, EndgamePhaseWeight(true))
            - GetKingSafetyScores(board.GetKingSquare(false).Index ^ 56, EndgamePhaseWeight(false)) // Bitwise XOR-ing with 56 flips the board perspective
            + GetEndgameBonus(true)
            - GetEndgameBonus(false))
            * (board.IsWhiteToMove ? 1 : -1)
            + mobility + 10; // add 10 points for tempo. Makes the bot better, makes zero sense :D
    }

    float EndgamePhaseWeight(bool isWhite)
    {
        return 1 - Math.Min(1, (CountMaterial(isWhite) - board.GetPieceList(PieceType.Pawn, isWhite).Count * 100) / 1750);
    }

    int GetMobilityBonus()
    {
        double mobility = 0;
        foreach (Move move in board.GetLegalMoves())
        {
            switch (move.MovePieceType)
            {
                case PieceType.Knight:
                    mobility += 10.5; // More points for knight since it has a smaller maximum of possible moves
                    break;
                case PieceType.Bishop:
                    mobility += 2.5;
                    break;
                case PieceType.Rook:
                    mobility += 3;
                    break;
                case PieceType.Queen:
                    mobility += 2;
                    break;
            }
        }
        return (int)mobility;
    }

    int GetKingSafetyScores(int index, float endgameWeight)
    {
        return (int)(kingSquareTables[index] + (kingSquareTables[index + 64] - kingSquareTables[index]) * endgameWeight);
    }

    /*
    int GetKingSafetyScoresOld(int file, int relativeRank, float endgameWeight)
    {
        sbyte midgameScore = (sbyte)((kingMidgameTable[Math.Min(relativeRank, 4)] >> file * 8) % 256);
        return (int)(midgameScore + ((sbyte)((kingEndgameTable[(int)Math.Abs(3.5 - relativeRank)] >> file * 8) % 256) - midgameScore) * endgameWeight);
    }
    */


    int CountMaterial(bool isWhite)
    {
        return board.GetPieceList(PieceType.Pawn, isWhite).Count * 100
            + board.GetPieceList(PieceType.Knight, isWhite).Count * 350
            + board.GetPieceList(PieceType.Bishop, isWhite).Count * 400
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
                    // Use power growth so we prioritice moving further pieces closer
                    endgameBonus += 50 - 10 * Math.Max(Math.Abs(enemyKingSquare.File - pieceSquare.File), Math.Abs(enemyKingSquare.Rank - pieceSquare.Rank));
                    break;
            }
        }

        return (int)(endgameBonus * enemyEndgameWeight);
    }

    #endregion
}