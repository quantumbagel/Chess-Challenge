using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class MyBot : IChessBot
{

    // Can save 4 tokens by removing this line and replacing `TABLE_SIZE` with a literal
    private const ulong TABLE_SIZE = 1 << 23;

    /// <summary>
    /// Transposition Table for caching previously computed positions during search.
    /// 
    /// To insert an entry:
    /// <code>TranspositionTable[zobrist % TABLE_SIZE] = (zobrist, depth, evaluation, nodeType, move);</code>
    /// 
    /// To retrieve an entry:
    /// <code>var (zobrist, depth, evaluation, nodeType, move) = TranspositionTable[zobrist % TABLE_SIZE];</code>
    /// 
    /// Node types:
    /// <list type="bullet">
    ///     <item>1: PV node, an exact evaluation</item>
    ///     <item>2: Beta cutoff node, a lower bound</item>
    ///     <item>3: All node, an upper bound</item>
    /// </list>
    /// </summary>
    private readonly
    (
        ulong,  // Zobrist
        int,    // Depth
        int,    // Evaluation
        int,    // Node Type
        Move    // Best move
    )[] transpositionTable = new (ulong, int, int, int, Move)[TABLE_SIZE];

    // Piece-Square Tables compressed int 
    private static readonly decimal[] pieceSquareTablesCompressed = new decimal[]
    {
        4978460806064095078296851476m,  6522240169798736936953058832m,  7763821172089717844421645332m,  6835380402392325270248166937m,
        6213973531253938010227351070m,  18644357406773104351716316180m, 22679784849068573230476182078m, 19892011539104306253099649097m,
        22679780126847616235260692288m, 19268191575007653487459059785m, 24234496730874047605944368700m, 24238133028299070709965934158m,
        25477281956367026345290650190m, 24238137787703438390116504146m, 24855894085084542900026560590m, 23615526749426401353162313808m,
        32623361016838571410296760681m, 32313876025463970406658042217m, 32623361016766513811947088232m, 32313876025463970406658042217m,
        32623361035357938231332793194m, 60899367951128420710422767977m, 62450433926275202050515650758m, 61830255036369583762076912073m,
        62450429203980777878731737287m, 61520765285662672672392792521m, 3109433038346208221776496324m,  4349762507829111347815123978m,
        623824723645700800090867208m,   1240367335658140438796436482m,  2427370447966817606566404m,     1240367335658140438762750464m,
        6835370883438909564355089428m,  6835370883583589910031242774m,  8078165589545016820724406296m,  8699562942742750794551532058m,
        6213973531253938010227351070m,  18644357406773104351716316180m, 22679784849068573230476182078m, 19892011539104306253099649097m,
        22679780126847616235260692288m, 19268191575007653487459059785m, 24234496730874047605944368700m, 24238133028299070709965934158m,
        25477281956367026345290650190m, 24238137787703438390116504146m, 24855894085084542900026560590m, 23615526749426401353162313808m,
        32623361016838571410296760681m, 32313876025463970406658042217m, 32623361016766513811947088232m, 32313876025463970406658042217m,
        32623361035357938231332793194m, 60899367951128420710422767977m, 62450433926275202050515650758m, 61830255036369583762076912073m,
        62450429203980777878731737287m, 61520765285662672672392792521m, 1242794646209009308353873604m,  1242823151419503330518631428m,
        5590110842907382193958291460m,  1247687337048965655042723858m,  3106958319953002390530951172m,  4854666820726964290325002m,
    };

    private static readonly int[] pieceSquareTables = pieceSquareTablesCompressed.SelectMany(decimal.GetBits).Where((_, i) => i % 4 != 3).SelectMany(BitConverter.GetBytes).Select(t => (int)t * 5).ToArray();


    // Store timer and board references to simplify function signatures
    private Timer timer;
    private Board board;

    private Move bestMove;
    //Move[] bestMovesByDepth;
    private readonly HashSet<Move>[] killerMoves = new HashSet<Move>[256].Select(t => new HashSet<Move>()).ToArray();
    private int bestEval;
    private bool isSearchCancelled;


    public Move Think(Board board, Timer timer)
    {
        /* Piece-Square Table Compression Code
        int[] newPieceSquareTablesPartialCompressed = new int[][]{
            pawnMidgameTable,
            knightSquareTable,
            bishopSquareTable,
            rookSquareTable,
            queenSquareTable,
            kingMidgameTable,
            pawnEndgameTable,
            knightSquareTable,
            bishopSquareTable,
            rookSquareTable,
            queenSquareTable,
            kingEndgameTable
        }.SelectMany(partialCompressPieceSquareTable).ToArray();

        decimal[] newTablesCompressed = compressIntArrayToDecimals(newPieceSquareTablesPartialCompressed);
        int[] testUncompress = testUncompressPieceSquareTable(newTablesCompressed);

        foreach (decimal item in newTablesCompressed) Console.WriteLine(item + "m,");
        Console.WriteLine("---");
        foreach (int item in testUncompress) Console.WriteLine(item + ",");
        //*/

        StartSearch(board, timer);
        //return bestMovesByDepth[0];
        return bestMove;
    }

    void StartSearch(Board _board, Timer _timer)
    {
        board = _board;
        timer = _timer;

        bestMove = default;
        //bestMovesByDepth = new Move[256];
        bestEval = 0;
        isSearchCancelled = false;

        for (int searchDepth = 1; !isSearchCancelled; searchDepth++)
        {
            // Use really large values to guarantee initial sets
            Search(searchDepth, 0, -9999999, 9999999);

            //Console.WriteLine($"completed depth: {searchDepth} Move: {bestMovesByDepth[0]}");
            Console.WriteLine($"completed depth: {searchDepth} {bestMove}");

            // Checkmate has been found; Hardcoded checkmate score to save tokens
            if (Math.Abs(bestEval) > 99000) break;
        }
    }

    int Search(int depth, int plyFromRoot, int alpha, int beta)
    {
        // Check if we need to cancel the search if we are out of time.
        isSearchCancelled = 30 * timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining;
        if (board.IsRepeatedPosition() || board.IsInsufficientMaterial()) return 0;

        // Check for Checkmate before we do anything else.
        if (board.IsInCheckmate()) return -100000 + plyFromRoot;


        // Once we reach target depth, search only captures to make the evaluation more accurate
        // Also if we're in check, add to depth so we make sure we don't screw ourselves
        if (depth == 0) 
        {
            if (board.IsInCheck()) depth++;
            else return QuiescenceSearch(alpha, beta); 
        }

        // Transposition table lookup
        // Probe before move generation to save time
        ulong zobrist = board.ZobristKey;
        ulong TTidx = zobrist % TABLE_SIZE;

        var (TTzobrist, TTdepth, TTeval, TTtype, TTm) = transpositionTable[TTidx];

        // The TT entry is from a different position, so no best move is available
        if (TTzobrist != zobrist)
            TTm = default;
        else if (plyFromRoot != 0 && TTdepth >= depth && (TTtype is 1 || TTtype is 2 && TTeval >= beta || TTtype is 3 && TTeval <= alpha))
            return TTeval;

        Span<Move> moves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref moves);

        // Stalemate Check
        if (moves.Length == 0) return 0;

        // Null Move Pruning: check if we beat beta even without moving
        if (depth > 2 && plyFromRoot != 0 && board.TrySkipTurn())
        {
            int eval = -Search(depth - 3, plyFromRoot + 3, -beta, -beta + 1);
            board.UndoSkipTurn();
            if (eval >= beta) return beta;
        }


        // Order the moves, making sure to put the best move from the previous iteration first
        Sort(ref moves, TTm);

        var oldAlpha = alpha;

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int eval = -Search(depth - 1, plyFromRoot + 1, -beta, -alpha);
            board.UndoMove(move);

            // Second Cancel Check just to be sure we aren't accidentally overriding
            // the previous best move with a cancelled search
            if (isSearchCancelled) return 0;

            if (eval >= beta)
            {
                killerMoves[board.PlyCount].Add(move);
                transpositionTable[TTidx] = (zobrist, depth, eval, 2, move);
                return beta; // *snip* :D
            }
            if (eval > alpha)
            {
                alpha = eval;
                TTm = move;
                if (plyFromRoot == 0) bestMove = TTm;
                bestEval = plyFromRoot == 0 ? eval : bestEval;
            }
        }

        transpositionTable[TTidx] = (zobrist, depth, alpha, alpha == oldAlpha ? 3 : 1, TTm);
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
                // 2. Killer Moves
                _ when killerMoves[board.PlyCount].Contains(move) => 1,
                // 3. Promotion
                { IsPromotion: true } => 2,
                // 4. Captures
                { IsCapture: true } => 1000 - 10 * (int)move.CapturePieceType + (int)move.MovePieceType,
                // 5. General Case
                _ => 100_000_000
            };
        }
        sortKeys.Sort(moves);
    }


    #region Evalution

    /* Old Tables
    
    int[] pawnMidgameTable = new int[]
    {
        100, 100, 100, 100, 100, 100, 100, 100,
        105, 110, 110,  80,  80, 110, 110, 105,
        105,  95,  90, 100, 100,  90,  95, 105,
        100, 100, 100, 120, 120, 100, 100, 100,
        105, 105, 110, 125, 125, 110, 105, 105,
        110, 110, 120, 130, 130, 120, 110, 110,
        150, 150, 150, 150, 150, 150, 150, 150,
        100, 100, 100, 100, 100, 100, 100, 100,
    };

    int[] pawnEndgameTable = new int[]
    {
        100, 100, 100, 100, 100, 100, 100, 100,
        110, 110, 110, 110, 110, 110, 110, 110,
        110, 110, 110, 110, 110, 110, 110, 110,
        120, 120, 120, 120, 120, 120, 120, 120,
        130, 130, 130, 130, 130, 130, 130, 130,
        140, 140, 140, 140, 140, 140, 140, 140,
        150, 150, 150, 150, 150, 150, 150, 150,
        100, 100, 100, 100, 100, 100, 100, 100,
    };

    int[] knightSquareTable = new int[]
    {
        300, 310, 320, 320, 320, 320, 310, 300,
        310, 330, 350, 355, 355, 350, 330, 310,
        320, 355, 360, 365, 365, 360, 355, 320,
        320, 350, 365, 370, 370, 365, 350, 320,
        320, 355, 365, 370, 370, 365, 355, 320,
        320, 350, 360, 365, 365, 360, 350, 320,
        310, 330, 350, 350, 350, 350, 330, 310,
        300, 310, 320, 320, 320, 320, 310, 300,
    };

    int[] bishopSquareTable = new int[]
    {
        380, 390, 390, 390, 390, 390, 390, 380,
        390, 405, 400, 400, 400, 400, 405, 390,
        390, 410, 410, 410, 410, 410, 410, 390,
        390, 400, 410, 410, 410, 410, 400, 390,
        390, 405, 405, 410, 410, 405, 405, 390,
        390, 400, 405, 410, 410, 405, 400, 390,
        390, 400, 400, 400, 400, 400, 400, 390,
        380, 390, 390, 390, 390, 390, 390, 380,
    };

    int[] rookSquareTable = new int[]
    {
        525, 525, 525, 530, 530, 525, 525, 525,
        520, 525, 525, 525, 525, 525, 525, 520,
        520, 525, 525, 525, 525, 525, 525, 520,
        520, 525, 525, 525, 525, 525, 525, 520,
        520, 525, 525, 525, 525, 525, 525, 520,
        520, 525, 525, 525, 525, 525, 525, 520,
        530, 535, 535, 535, 535, 535, 535, 530,
        525, 525, 525, 525, 525, 525, 525, 525,
    };

    int[] queenSquareTable = new int[]
    {
         980, 990, 990, 995, 995, 990, 990, 980,
         990,1000,1005,1000,1000,1000,1000, 990,
         990,1005,1005,1005,1005,1005,1000, 990,
        1000,1000,1005,1005,1005,1005,1000, 995,
         995,1000,1005,1005,1005,1005,1000, 995,
         990,1000,1005,1005,1005,1005,1000, 990,
         990,1000,1000,1000,1000,1000,1000, 990,
         980, 990, 990, 995, 995, 990, 990, 980,
    };

    int[] kingMidgameTable = new int[]
    {
        70, 80, 60, 50, 50, 60, 80, 70,
        70, 70, 50, 50, 50, 50, 70, 70,
        40, 30, 30, 30, 30, 30, 30, 40,
        30, 20, 20, 10, 10, 20, 20, 30,
        20, 10, 10,  0,  0, 10, 10, 20,
        20, 10, 10,  0,  0, 10, 10, 20,
        20, 10, 10,  0,  0, 10, 10, 20,
        20, 10, 10,  0,  0, 10, 10, 20,
    };

    int[] kingEndgameTable = new int[]
    {
         0, 20, 20, 20, 20, 20, 20,  0,
        20, 20, 50, 50, 50, 50, 20, 20,
        20, 40, 70, 80, 80, 70, 40, 20,
        20, 40, 80, 90, 90, 80, 40, 20,
        20, 40, 80, 90, 90, 80, 40, 20,
        20, 40, 70, 80, 80, 70, 40, 20,
        20, 20, 50, 50, 50, 50, 20, 20,
         0, 20, 20, 20, 20, 20, 20,  0,
    };

    //Represent the rank scores as a 64-bit int. Last couple rows are all copies
    static readonly ulong[] kingMidgameTableUlongs = new ulong[]
    {
        0b_00010100_00011110_00001010_00000000_00000000_00001010_00011110_00010100L,
        0b_00010100_00010100_00000000_00000000_00000000_00000000_00010100_00010100L,
        0b_11110110_11101100_11101100_11101100_11101100_11101100_11101100_11110110L,
        0b_11101100_11100010_11100010_11011000_11011000_11100010_11100010_11101100L,
        0b_11100010_11011000_11011000_11001110_11001110_11011000_11011000_11100010L
    };

    static readonly ulong[] kingEndgameTableUlongs = new ulong[]
    {
        0b_11100010_11110110_00011110_00101000_00101000_00011110_11110110_11100010L,
        0b_11100010_11110110_00010100_00011110_00011110_00010100_11110110_11100010L,
        0b_11100010_11100010_00000000_00000000_00000000_00000000_11100010_11100010L,
        0b_11001110_11100010_11100010_11100010_11100010_11100010_11100010_11001110L,
    };

    static readonly decimal[] compressedKingSquareTables = new decimal[]
    {
        94817714145992272125460m,       76419737758473778852349083648m, 64016064217427759519433417452m, 70205764042755225896833571022m,
        64016064216704357791052650722m, 64028200698568158524470778062m, 9309894698500220833627169506m,  70241150383004452811665970206m,
        9309894698505883490981967586m,  70216829454857141189382378526m, 14907727180646769358m,
    };
    //*/

    /* HELPER FUNCTIONS FOR DATA COMPRESSION

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
        return compressed.SelectMany(decimal.GetBits).Where((_, i) => i % 4 != 3).SelectMany(BitConverter.GetBytes).Select(t => (int)t * 5).ToArray();
    }

    private int[] partialCompressPieceSquareTable(int[] table)
    {
        Debug.Assert(table.Length == 64);
        int[] result = new int[16];
        for (int i = 0; i < 64; i++)
        {
            int arrayLoc = i / 4;
            result[arrayLoc] += (table[i] / 5) << 8 * i;
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

    //*/

    // Performs static evaluation of the current position.
    // The position is assumed to be 'quiet', i.e no captures are available that could drastically affect the evaluation.
    // The score that's returned is given from the perspective of whoever's turn it is to move.
    // So a positive score means the player who's turn it is to move has an advantage, while a negative score indicates a disadvantage.

    // might as well make this a static variable so we don't have to keep recreating it.
    static readonly int[] signs = new int[] { 1, -1 };
    public int Evaluate()
    {
        int mgScore = 0, 
            egScore = 0, 
            //material = 0, Don't need to check for material since it's built in to the 
            phase = 0;

        // Loop through white and black pieces (1 for white, -1 for black)
        foreach (var sign in signs)
        {
            for (var piece = 0; piece < 6; piece++)
            {
                ulong bitboard = board.GetPieceBitboard((PieceType)piece + 1, sign is 1);
                while (bitboard != 0)
                {
                    int pieceIndex = BitboardHelper.ClearAndGetIndexOfLSB(ref bitboard);
                    int tableIndex = piece * 64                                // table start index
                        + ( pieceIndex                                         // square index in the table
                        ^ (sign is -1 ? 56 : 0));                              // flip board for white pieces

                    mgScore += sign * pieceSquareTables[tableIndex];
                    egScore += sign * pieceSquareTables[tableIndex + 384];
                    phase += 0b_0100_0010_0001_0001_0000 >> 4 * piece & 0xF;
                    //material += sign * (int)(0b_1111101000_1000001101_0110010000_0101011110_0001100100L >> 10 * piece & 0x3FF);

                    /*
                    // In the endgame, get the rooks closer to the enemy king's rank/file
                    if (piece is 4) egScore += 50 - 10 * Math.Min(Math.Abs(enemyKingSquare.File - (pieceIndex % 8)), Math.Abs(enemyKingSquare.Rank - (pieceIndex / 8)));
                    // For other pieces, just get closer in general.
                    else egScore += 50 - 10 * Math.Max(Math.Abs(enemyKingSquare.File - (pieceIndex % 8)), Math.Abs(enemyKingSquare.Rank - (pieceIndex / 8)));
                    //*/
                }
            }
        }

        int eval = (mgScore * phase + egScore * (24 - phase)) / 24;
        // Tempo bonus for the current side to move
        return 10 + (board.IsWhiteToMove ? eval : -eval);
    }

    /* Old Eval Helpers
    float EndgamePhaseWeight(bool isWhite)
    {
        return 1 - Math.Min(1, (CountMaterial(isWhite) - board.GetPieceList(PieceType.Pawn, isWhite).Count * 100) / 1750);
    }

    int GetKingSafetyScoresOld(int file, int relativeRank, float endgameWeight)
    {
        sbyte midgameScore = (sbyte)((kingMidgameTable[Math.Min(relativeRank, 4)] >> file * 8) % 256);
        return (int)(midgameScore + ((sbyte)((kingEndgameTable[(int)Math.Abs(3.5 - relativeRank)] >> file * 8) % 256) - midgameScore) * endgameWeight);
    }


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
                    endgameBonus += 50 - 10 * Math.Max(Math.Abs(enemyKingSquare.File - pieceSquare.File), Math.Abs(enemyKingSquare.Rank - pieceSquare.Rank));
                    break;
            }
        }

        return (int)(endgameBonus * enemyEndgameWeight);
    }
    //*/

    #endregion
}