using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class MyBot : IChessBot
{

    // Piece-Square Tables compressed int 
    static readonly decimal[] pieceSquareTablesCompressed = new decimal[]
    {
        73050598891932131575795286016m, 1852027132054302042446498540m, 7749238207795907812875304960m, 3107034359308944942007912985m,
        3617008641903833650m, 64016111440372001233801576448m, 4654392201943765757458443480m, 69943683425501485030533564943m,
        4654368590766923187343001058m, 67134068614839847746032110095m, 76431874238167385442330269902m, 76139357045148966610265372406m,
        3106943912037633032876329718m, 76139380842170805011001379338m, 4555625215806555881718m, 73337024327312145536499318784m,
        4630132762522656178176m, 77680737465157613332167917568m, 4648219218604617367803m, 77680737465157613332167917568m,
        363113758191127045m, 73337024419906153871109521408m, 1553497845662423103603802358m, 77680761169585442315894260997m,
        1553474234190302244684235003m, 76133312416050887966985291013m, 12231315200620454640809708m, 6213878712819216034459159040m,
        67123050982693027703474154742m, 70205764042755225897002001112m, 64016064216704357791052650722m, 70205764042755225896833571022m,
        3106986764541866412542525440m, 3106986765265268140923292170m, 9320960295072402694389109780m, 12427947061061072563524738590m,
        3617008641903833650m, 64016111440372001233801576448m, 4654392201943765757458443480m, 69943683425501485030533564943m,
        4654368590766923187343001058m, 67134068614839847746032110095m, 76431874238167385442330269902m, 76139357045148966610265372406m,
        3106943912037633032876329718m, 76139380842170805011001379338m, 4555625215806555881718m, 73337024327312145536499318784m,
        4630132762522656178176m, 77680737465157613332167917568m, 4648219218604617367803m, 77680737465157613332167917568m,
        363113758191127045m, 73337024419906153871109521408m, 1553497845662423103603802358m, 77680761169585442315894260997m,
        1553474234190302244684235003m, 76133312416050887966985291013m, 70217900526786406048616347372m, 70216829454857141189045576418m,
        12416834054915469431742461666m, 70241150383004452811665972776m, 1071440143570414605760226m, 64028200698568158524471377920m,
    };

    static readonly int[] pieceSquareTables = pieceSquareTablesCompressed.SelectMany(decimal.GetBits).Where((_, i) => i % 4 != 3).SelectMany(BitConverter.GetBytes).Select(t => (int)(sbyte)t).ToArray();


    // Store timer and board references to simplify function signatures
    private Timer timer;
    private Board board;

    Move[] bestMovesByDepth;
    HashSet<Move>[] killerMoves;
    int bestEval;
    bool isSearchCancelled;


    public Move Think(Board board, Timer timer)
    {
        StartSearch(board, timer);
        return bestMovesByDepth[0];
    }

    void StartSearch(Board _board, Timer _timer)
    {
        board = _board;
        timer = _timer;

        bestMovesByDepth = new Move[256];
        bestEval = 0;
        isSearchCancelled = false;

        killerMoves = new HashSet<Move>[256];
        for (int i = 0; i < 256; i++)
        {
            killerMoves[i] = new HashSet<Move>();
        }

        for (int searchDepth = 1; !isSearchCancelled; searchDepth++)
        {
            // Use really large values to guarantee initial sets
            Search(searchDepth, 0, -9999999, 9999999);

            Console.WriteLine($"completed depth: {searchDepth} Move: {bestMovesByDepth[0]}");

            // Checkmate has been found; Hardcoded checkmate score to save tokens
            if (Math.Abs(bestEval) > 99000) break;
        }
    }

    int Search(int depth, int plyFromRoot, int alpha, int beta)
    {
        // Cancel the search if we are out of time.
        isSearchCancelled = 30 * timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining;
        if (isSearchCancelled || board.IsRepeatedPosition() || board.IsInsufficientMaterial()) return 0;

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
            int eval = -Search(depth - 1, plyFromRoot + 1, -beta, -alpha);
            board.UndoMove(move);

            // Second Cancel Check just to be sure we aren't accidentally overriding
            // the previous best move with a cancelled search
            if (isSearchCancelled) return 0;

            if (eval >= beta)
            {
                killerMoves[board.PlyCount].Add(move);
                return beta; // *snip* :D
            }
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
    
    sbyte[] pawnMidgameTableSBytes = new sbyte[]
    {
         0,  0,   0,   0,   0,   0,  0,  0,
         5, 10,  10, -20, -20,  10, 10,  5,
         5,  -5, -10,   0,   0, -10, -5,  5,
         0,  0,   0,  20,  20,   0,  0,  0,
         5,  5,  10,  25,  25,  10,  5,  5,
        10, 10,  20,  30,  30,  20, 10, 10,
        50, 50,  50,  50,  50,  50, 50, 50,
         0,  0,   0,   0,   0,   0,  0,  0,
    };

    sbyte[] pawnEndgameTableSBytes = new sbyte[]
    {
         0,  0,  0,  0,  0,  0,  0,  0,
        10, 10, 10, 10, 10, 10, 10, 10,
        10, 10, 10, 10, 10, 10, 10, 10,
        20, 20, 20, 20, 20, 20, 20, 20,
        30, 30, 30, 30, 30, 30, 30, 30,
        40, 40, 40, 40, 40, 40, 40, 40,
        50, 50, 50, 50, 50, 50, 50, 50,
         0,  0,  0,  0,  0,  0,  0,  0,
    };

    sbyte[] knightSquareTableSBytes = new sbyte[]
    {
        -50,-40,-30,-30,-30,-30,-40,-50,
        -40,-20,  0,  5,  5,  0,-20,-40,
        -30,  5, 10, 15, 15, 10,  5,-30,
        -30,  0, 15, 20, 20, 15,  0,-30,
        -30,  5, 15, 20, 20, 15,  5,-30,
        -30,  0, 10, 15, 15, 10,  0,-30,
        -40,-20,  0,  0,  0,  0,-20,-40,
        -50,-40,-30,-30,-30,-30,-40,-50,
    };

    sbyte[] bishopSquareTableSBytes = new sbyte[]
    {
        -20,-10,-10,-10,-10,-10,-10,-20,
        -10,  5,  0,  0,  0,  0,  5,-10,
        -10, 10, 10, 10, 10, 10, 10,-10,
        -10,  0, 10, 10, 10, 10,  0,-10,
        -10,  5,  5, 10, 10,  5,  5,-10,
        -10,  0,  5, 10, 10,  5,  0,-10,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -20,-10,-10,-10,-10,-10,-10,-20,
    };

    sbyte[] rookSquareTableSBytes = new sbyte[]
    {
         0,  0,  0,  5,  5,  0,  0,  0,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
        -5,  0,  0,  0,  0,  0,  0, -5,
         5, 10, 10, 10, 10, 10, 10,  5,
         0,  0,  0,  0,  0,  0,  0,  0,
    };

    sbyte[] queenSquareTableSBytes = new sbyte[]
    {
        -20,-10,-10, -5, -5,-10,-10,-20,
        -10,  0,  5,  0,  0,  0,  0,-10,
        -10,  5,  5,  5,  5,  5,  0,-10,
          0,  0,  5,  5,  5,  5,  0, -5,
         -5,  0,  5,  5,  5,  5,  0, -5,
        -10,  0,  5,  5,  5,  5,  0,-10,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -20,-10,-10, -5, -5,-10,-10,-20,
    };

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

    //*/

    // Performs static evaluation of the current position.
    // The position is assumed to be 'quiet', i.e no captures are available that could drastically affect the evaluation.
    // The score that's returned is given from the perspective of whoever's turn it is to move.
    // So a positive score means the player who's turn it is to move has an advantage, while a negative score indicates a disadvantage.
    public int Evaluate()
    {
        int mgScore = 0, egScore = 0, material = 0, phase = 0;

        // Loop through white and black pieces (1 for white, -1 for black)
        foreach (var sign in new[] { 1, -1 })
        {
            Square enemyKingSquare = board.GetKingSquare(sign is -1);
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
                    material += sign * (int)(0b_1111101000_1000001101_0110010000_0101011110_0001100100L >> 10 * piece & 0x3FF);

                    /*
                    // In the endgame, get the rooks closer to the enemy king's rank/file
                    if (piece is 4) egScore += 50 - 10 * Math.Min(Math.Abs(enemyKingSquare.File - (pieceIndex % 8)), Math.Abs(enemyKingSquare.Rank - (pieceIndex / 8)));
                    // For other pieces, just get closer in general.
                    else egScore += 50 - 10 * Math.Max(Math.Abs(enemyKingSquare.File - (pieceIndex % 8)), Math.Abs(enemyKingSquare.Rank - (pieceIndex / 8)));
                    //*/
                }
            }
        }

        int eval = material + (mgScore * phase + egScore * (24 - phase)) / 24;
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