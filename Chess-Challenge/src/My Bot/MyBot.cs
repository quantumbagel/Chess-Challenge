using System;
using System.Collections.Generic;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        StartSearch(board, timer);
        return _bestMovesByDepth[0];
    }

    private int _maxSearchDepth = int.MaxValue;
    private int _maxMillisecondsPerSearch = 1000;
    private bool _isSearchCancelled;

    private const int PositiveInfinity = 2147483647;
    private const int NegativeInfinity = -2147483647;
    private List<Move> _bestMovesByDepth;

    void StartSearch(Board board, Timer timer)
    {
        _isSearchCancelled = false;
        _bestMovesByDepth = new List<Move>();
        for (int searchDepth = 1; searchDepth <= _maxSearchDepth; searchDepth++)
        {
            _bestMovesByDepth.Add(Move.NullMove);
            Search(board, timer, searchDepth, 0, NegativeInfinity, PositiveInfinity);
            if (_isSearchCancelled) break;
        }
    }


    int Search(Board board, Timer timer, int plyRemaining, int plyFromRoot, int alpha, int beta)
    {
        if (timer.MillisecondsElapsedThisTurn > _maxMillisecondsPerSearch)
        {
            _isSearchCancelled = true;
            return 0;
        }

        if (plyRemaining == 0) return QuiescenceSearch(board, alpha, beta);

        // Order the moves so we get more out of the beta pruning
        Move[] moves = Order(board.GetLegalMoves(), board, _bestMovesByDepth[plyFromRoot]);

        if (board.IsInCheckmate()) return NegativeInfinity; // Checkmate
        
        if (moves.Length == 0) return 0; // Stalemate

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int eval = -Search(board, timer, plyRemaining - 1, plyFromRoot + 1, -beta, -alpha);
            board.UndoMove(move);
            if (eval == PositiveInfinity) return PositiveInfinity; // checkmate can't be beat
            //if (eval >= beta) return beta;
            if (eval > alpha)
            {
                alpha = eval;
                _bestMovesByDepth[plyFromRoot] = move;
            }
        }
        return alpha;
    }

    int QuiescenceSearch(Board board, int alpha, int beta)
    {
        int eval = Evaluate(board);
        if (eval >= beta) return beta;
        alpha = (int)MathF.Max(alpha, eval);

        Move[] captureMoves = Order(board.GetLegalMoves(true), board, Move.NullMove);
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

    Move[] Order(Move[] moves, Board board, Move putThisFirst)
    {
        if (moves.Length == 0) return new Move[0];
        Move[] returnThis = new Move[moves.Length];
        Dictionary<Move, int> orderedMoves = new Dictionary<Move, int>();
        foreach (Move move in moves)
        {
            if (move == putThisFirst) continue;
            int moveScoreGuess = 0;
            if (move.IsCapture)
                moveScoreGuess +=
                    10 * GetPointValue(move.CapturePieceType) -
                    GetPointValue(move.MovePieceType);
            if (move.IsPromotion) moveScoreGuess += GetPointValue(move.PromotionPieceType);
            if (board.SquareIsAttackedByOpponent(move.TargetSquare))
                moveScoreGuess -= GetPointValue(move.MovePieceType);
            orderedMoves.Add(move, moveScoreGuess);
        }
        int counter = 0;
        if (moves.Contains(putThisFirst)) 
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
                return _pointValues[(int)type - 1];
        }
    }



    int[] _pointValues = new int[] { 100, 350, 350, 525, 1000 };

    
    private float GetProgression(Board board)
    {
        return 1 - BitboardHelper.GetNumberOfSetBits(board.AllPiecesBitboard) / 32f;
    }
    
    public int Evaluate(Board board)
    {
        // progression is 0 at early game, 1 at late game
        float progression = GetProgression(board);
        int perspective = board.IsWhiteToMove ? 1 : -1;


        // Material
        PieceList[] pieceLists = board.GetAllPieceLists();
        int material = (pieceLists[0].Count - pieceLists[6].Count) * _pointValues[0]
                       + (pieceLists[1].Count - pieceLists[7].Count) * _pointValues[1]
                       + (pieceLists[2].Count - pieceLists[8].Count) * _pointValues[2]
                       + (pieceLists[3].Count - pieceLists[9].Count) * _pointValues[3]
                       + (pieceLists[4].Count - pieceLists[10].Count) * _pointValues[4];
        material *= perspective;

        
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
        ulong ourBb = board.IsWhiteToMove ? board.WhitePiecesBitboard : board.BlackPiecesBitboard;
        Square enemyKingSquare = board.GetKingSquare(!board.IsWhiteToMove);
        while (ourBb != 0)
        {
            Piece piece = board.GetPiece(new Square(BitboardHelper.ClearAndGetIndexOfLSB(ref ourBb)));
            if (piece.IsPawn) continue;
            int chebyshev = (int)MathF.Max(MathF.Abs(piece.Square.File - enemyKingSquare.File),
                MathF.Abs(piece.Square.Rank - enemyKingSquare.Rank));
            if (chebyshev > 1)
            {
                endgameBonus +=
                    (piece.IsKing ? 50 : 100) -
                    (int)(25 * MathF.Pow(chebyshev - 2,
                        2)); // Give less points for kings so we have less chance of repeating

            }

            endgameBonus = (int)(endgameBonus * progression);
        }
        return material + pawnDevelopment + endgameBonus;
    }
}