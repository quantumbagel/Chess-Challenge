using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Mail;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        StartSearch(board, timer);
        return bestMovesByDepth[0];
    }

    private int maxSearchDepth = 10;
    private int maxMillisecondsPerSearch = 1000;
    private bool isSearchCancelled;

    const int immediateMateScore = 100000;
    const int positiveInfinity = 9999999;
    const int negativeInfinity = -positiveInfinity;

    private Move[] bestMovesByDepth;

    void StartSearch(Board board, Timer timer)
    {
        isSearchCancelled = false;
        bestMovesByDepth = new Move[maxSearchDepth];
        for (int searchDepth = 1; searchDepth <= maxSearchDepth; searchDepth++)
        {
            bestMovesByDepth[searchDepth - 1] = Move.NullMove;
            Search(board, timer, searchDepth, 0, negativeInfinity, positiveInfinity);
            if (isSearchCancelled) break;
        }
    }

    // From what I can gather, alpha is the best eval for us we've found so far. Beta is the best eval for the opponent so far at a higher depth (the alpha of the opponent).
    // We prune branches when our eval is greater than our beta, hence worse for our opponent, so that means this branch won't even be considered by them and thus isn't worth searching.
    int Search(Board board, Timer timer, int plyRemaining, int plyFromRoot, int alpha, int beta)
    {
        if (timer.MillisecondsElapsedThisTurn > maxMillisecondsPerSearch)
        {
            isSearchCancelled = true;
            return 0;
        }

        if (plyFromRoot > 0)
        {
            // Skip this position if a mating sequence has already been found earlier in
            // the search, which would be shorter than any mate we could find from here.
            // This is done by observing that alpha can't possibly be worse (and likewise
            // beta can't  possibly be better) than being mated in the current position.
            alpha = Math.Max(alpha, -immediateMateScore + plyFromRoot);
            beta = Math.Min(beta, immediateMateScore - plyFromRoot);
            if (alpha >= beta)
            {
                return alpha;
            }
        }

        if (plyRemaining == 0) return QuiescenceSearch(board, alpha, beta);

        // Order the moves so we get more out of the beta pruning
        Move[] moves = Order(board.GetLegalMoves(), board, bestMovesByDepth[plyFromRoot]);

        if (board.IsInCheckmate()) return negativeInfinity; // Checkmate
        if (moves.Length == 0) return 0; // Stalemate

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int eval = -Search(board, timer, plyRemaining - 1, plyFromRoot + 1, -beta, -alpha);
            board.UndoMove(move);

            if (eval == positiveInfinity) return positiveInfinity;
            if (eval >= beta) return beta; // *snip* :D
            if (eval > alpha)
            {
                alpha = eval;
                bestMovesByDepth[plyFromRoot] = move;
            }
        }

        return alpha;
    }

    int QuiescenceSearch(Board board, int alpha, int beta)
    {
        int eval = Evaluate(board);
        if (eval >= beta) return beta;
        alpha = Math.Max(alpha, eval);

        Move[] captureMoves = Order(board.GetLegalMoves(true), board, Move.NullMove);
        foreach (Move captureMove in captureMoves)
        {
            board.MakeMove(captureMove);
            eval = -QuiescenceSearch(board, -beta, -alpha);
            board.UndoMove(captureMove);

            if (eval >= beta) return beta;
            alpha = Math.Max(alpha, eval);
        }
        return alpha;
    }

    Move[] Order(Move[] moves, Board board, Move putThisFirst)
    {
        if (moves.Length == 0) return new Move[0];
        Move[] returnThis = new Move[moves.Length];

        Dictionary<Move, int> orderedMoves = new Dictionary<Move, int>();
        foreach (Move move in moves)
        {
            if (move == putThisFirst) continue;
            if (move.IsNull) continue;


            int moveScoreGuess = 0;
            if (move.IsCapture) moveScoreGuess += 10 * GetPointValue(move.CapturePieceType) - GetPointValue(move.MovePieceType); // This should be running for null moves SO WHY IS IT??!?!?!?!
            if (move.IsPromotion) moveScoreGuess += GetPointValue(move.PromotionPieceType);
            if (board.SquareIsAttackedByOpponent(move.TargetSquare)) moveScoreGuess -= GetPointValue(move.MovePieceType);
            orderedMoves.Add(move, moveScoreGuess);
        }

        int counter = 0;
        if (!putThisFirst.IsNull && moves.Contains(putThisFirst))
        {
            returnThis[0] = putThisFirst;
            counter = 1;
        }

        foreach (var k in orderedMoves.OrderByDescending(x => x.Value))
        {
            returnThis[counter] = k.Key;
            counter++;
        }

        return returnThis;

    }

    int GetPointValue(PieceType type)
    {
        switch (type)
        {
            case PieceType.None: return 0;
            case PieceType.King: return int.MaxValue;
            default:
                return POINT_VALUES[(int)type - 1];
        }
    }



    int[] POINT_VALUES = new int[] { 100, 350, 350, 525, 1000 };

    int[] king_mg_table = new int[]
    {
        20, 30, 10,  0,  0, 10, 30, 20,
        20, 20,  0,  0,  0,  0, 20, 20,
        -10,-20,-20,-20,-20,-20,-20,-10,
        -20,-30,-30,-40,-40,-30,-30,-20,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
    };

    //Represent the rank scores as a 64-bit int. NEED TO FINISH
    ulong[] king_mg_table_v2 = new ulong[]
    {
        0b0001010000011110000010100000000000000000000010100001111000010100L,
        0b0001010000010100000000000000000000000000000000000001010000010100L,
    };
    int[] king_eg_table = new int[]
    {
        -50,-30,-30,-30,-30,-30,-30,-50,
        -30,-30,  0,  0,  0,  0,-30,-30,
        -30,-10, 20, 30, 30, 20,-10,-30,
        -30,-10, 30, 40, 40, 30,-10,-30,
        -30,-10, 30, 40, 40, 30,-10,-30,
        -30,-10, 20, 30, 30, 20,-10,-30,
        -30,-20,-10,  0,  0,-10,-20,-30,
        -50,-40,-30,-20,-20,-30,-40,-50,
    };

    private float GetProgression(Board board)
    {
        return 1 - (BitboardHelper.GetNumberOfSetBits(board.AllPiecesBitboard) / 32f);
    }

    private float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }



    public int Evaluate(Board board)
    {
        // progression is 0 at early game, 1 at late game
        float progression = GetProgression(board);
        int perspective = (board.IsWhiteToMove) ? 1 : -1;


        // Material
        PieceList[] pieceLists = board.GetAllPieceLists();
        int material = (pieceLists[0].Count - pieceLists[6].Count) * POINT_VALUES[0]
            + (pieceLists[1].Count - pieceLists[7].Count) * POINT_VALUES[1]
            + (pieceLists[2].Count - pieceLists[8].Count) * POINT_VALUES[2]
            + (pieceLists[3].Count - pieceLists[9].Count) * POINT_VALUES[3]
            + (pieceLists[4].Count - pieceLists[10].Count) * POINT_VALUES[4];
        material *= perspective;


        // King Safety
        int whiteKingRelativeIndex = board.GetKingSquare(true).Index;
        int blackKingRelativeIndex = new Square(board.GetKingSquare(false).File, 7 - board.GetKingSquare(false).Rank).Index;
        int whiteKingPositioning = (int)Lerp(king_mg_table[whiteKingRelativeIndex], king_eg_table[whiteKingRelativeIndex], progression);
        int blackKingPositioning = (int)Lerp(king_mg_table[blackKingRelativeIndex], king_eg_table[blackKingRelativeIndex], progression);
        int kingPositioning = (whiteKingPositioning - blackKingPositioning) * perspective;


        //Pawn Development
        int pawnDevelopment = 0;
        foreach (Piece pawn in board.GetPieceList(PieceType.Pawn, true))
        {
            pawnDevelopment += 50 - 15 * (7 - pawn.Square.Rank);
        }
        foreach (Piece pawn in board.GetPieceList(PieceType.Pawn, false))
        {
            pawnDevelopment -= 50 - 15 * (pawn.Square.Rank);
        }
        pawnDevelopment = (int)(pawnDevelopment * progression * perspective);


        //Endgame Bonus
        int endgameBonus = 0;
        ulong ourBB = (board.IsWhiteToMove) ? board.WhitePiecesBitboard : board.BlackPiecesBitboard;
        Square enemyKingSquare = board.GetKingSquare(!board.IsWhiteToMove);
        while (ourBB != 0)
        {
            Piece piece = board.GetPiece(new Square(BitboardHelper.ClearAndGetIndexOfLSB(ref ourBB)));
            if (piece.IsPawn) continue;
            int chebyshev = (int)MathF.Max(MathF.Abs(piece.Square.File - enemyKingSquare.File), MathF.Abs(piece.Square.Rank - enemyKingSquare.Rank));
            endgameBonus += ((piece.IsKing) ? 50 : 100) - (int)(25 * MathF.Pow(chebyshev - 1, 2)); // Give less points for kings so we have less chance of repeating
        }
        endgameBonus = (int)(endgameBonus * progression);

        return material + kingPositioning + pawnDevelopment + endgameBonus;
    }
}