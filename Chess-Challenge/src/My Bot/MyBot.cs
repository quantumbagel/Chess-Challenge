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
        return bestMove;
    }

    private int maxSearchDepth = 6;
    private int maxMillisecondsPerSearch = 5000;
    private bool isSearchCancelled = false;

    private Move bestMoveThisIteration;
    private int bestEvalThisIteration;

    private Move bestMove = Move.NullMove;
    private int bestEval = int.MinValue;

    void StartSearch(Board board, Timer timer)
    {
        for (int searchDepth = 1; searchDepth <= maxSearchDepth; searchDepth++)
        {
            bestMoveThisIteration = Move.NullMove;
            bestEvalThisIteration = int.MinValue;
            Search(board, timer, searchDepth, 0, int.MinValue, int.MaxValue);

            bestMove = bestMoveThisIteration;
            bestEval = bestEvalThisIteration;
            if (isSearchCancelled) break;
        }
    }


    int Search(Board board, Timer timer, int plyRemaining, int plyFromRoot, int alpha, int beta)
    {
        if (timer.MillisecondsElapsedThisTurn > maxMillisecondsPerSearch)
        {
            return 0;
        }

        if (plyRemaining == 0) return QuiescenceSearch(board, alpha, beta);

        // Order the moves so we get more out of the beta pruning
        Move[] moves = Order(board.GetLegalMoves(), board);

        if (board.IsInCheckmate()) return -(int.MaxValue - plyFromRoot); // Checkmate
        else if (moves.Length == 0) return 0; // Stalemate

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int eval = -Search(board, timer, plyRemaining - 1, plyFromRoot + 1, -beta, -alpha);
            board.UndoMove(move);

            if (eval >= beta) return beta;
            if (eval > alpha)
            {
                alpha = eval;
                bestMoveThisIteration = plyFromRoot == 0 ? move : bestMoveThisIteration;
                bestEvalThisIteration = plyFromRoot == 0 ? eval : bestEvalThisIteration;
            }
        }

        return alpha;
    }

    int QuiescenceSearch(Board board, int alpha, int beta)
    {
        int eval = Evaluate(board);
        if (eval >= beta) return beta;
        alpha = (int)MathF.Max(alpha, eval);

        Move[] captureMoves = Order(board.GetLegalMoves(true), board);
        foreach (Move captureMove in captureMoves)
        {
            board.MakeMove(captureMove);
            eval = -QuiescenceSearch(board, -beta, -alpha);
            board.UndoMove(captureMove);

            if (eval >= beta) return beta;
            alpha = (int)MathF.Max(alpha, eval);
        }
        return alpha;
    }

    Move[] Order(Move[] moves, Board board)
    {
        Move[] returnThis = new Move[moves.Length];

        Dictionary<Move, int> orderedMoves = new Dictionary<Move, int>();
        foreach (Move move in moves)
        {
            if (move.IsNull) continue;


            int moveScoreGuess = 0;
            if (move.IsCapture) moveScoreGuess += 10 * POINT_VALUES[(int)move.CapturePieceType - 1] - POINT_VALUES[(int)move.MovePieceType - 1]; // This should be running for null moves SO WHY IS IT??!?!?!?!
            if (move.IsPromotion) moveScoreGuess += POINT_VALUES[(int)move.PromotionPieceType - 1];
            if (board.SquareIsAttackedByOpponent(move.TargetSquare)) moveScoreGuess -= POINT_VALUES[(int)move.MovePieceType - 1];
            orderedMoves.Add(move, (int)move.CapturePieceType);
        }
        int counter = 0;
        foreach (var k in orderedMoves.OrderByDescending(x => x.Value))
        {
            returnThis[counter] = k.Key;
            counter++;
        }

        return returnThis;

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
        int whiteKingRelativeIndex = board.GetKingSquare(board.IsWhiteToMove).Index;
        int blackKingRelativeIndex = 63 - board.GetKingSquare(board.IsWhiteToMove).Index;
        int whiteKingPositioning = (int)Lerp(king_mg_table[whiteKingRelativeIndex], king_eg_table[whiteKingRelativeIndex], progression);
        int blackKingPositioning = (int)Lerp(king_mg_table[blackKingRelativeIndex], king_eg_table[blackKingRelativeIndex], progression);
        int kingPositioning = (whiteKingPositioning - blackKingPositioning) * perspective;


        //Pawn Development
        int pawnDevelopment = 0;
        foreach (Piece pawn in board.GetPieceList(PieceType.Pawn, board.IsWhiteToMove))
        {
            pawnDevelopment += 50 - 15 * (7 - pawn.Square.Rank);
        }
        foreach (Piece pawn in board.GetPieceList(PieceType.Pawn, !board.IsWhiteToMove))
        {
            pawnDevelopment -= 50 - 15 * (7 - pawn.Square.Rank);
        }
        pawnDevelopment = (int)(pawnDevelopment * progression);


        //Endgame Bonus
        int endgameBonus = 0;
        ulong ourBB = (board.IsWhiteToMove) ? board.WhitePiecesBitboard : board.BlackPiecesBitboard;
        Square enemyKingSquare = board.GetKingSquare(!board.IsWhiteToMove);
        while (ourBB != 0)
        {
            Piece piece = board.GetPiece(new Square(BitboardHelper.ClearAndGetIndexOfLSB(ref ourBB)));
            if (piece.IsPawn) continue;
            int chebyshev = (int)MathF.Max(MathF.Abs(piece.Square.File - enemyKingSquare.File), MathF.Abs(piece.Square.Rank - enemyKingSquare.Rank));
            if (chebyshev > 1)
            {
                endgameBonus += ((piece.IsKing) ? 75 : 100) - 25 * (chebyshev - 2); // Give less points for kings so we have less chance of repeating
            }
        }
        endgameBonus = (int)(endgameBonus * MathF.Pow(progression, 2));

        return material + kingPositioning + pawnDevelopment + endgameBonus;
    }
}