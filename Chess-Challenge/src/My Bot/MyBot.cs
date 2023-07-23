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
        // REMOVEME
        Console.Write("Pruned this many times on last move: ");
        Console.WriteLine(pruneTimes);
        Console.Write("Searched this many nodes on last move: ");
        Console.WriteLine(searchedNodes);
        pruneTimes = 0;
        searchedNodes = 0;
        return Search(board, timer, 7, int.MinValue, int.MaxValue, false).Item2;
        
    }

    private int searchedNodes = 0; // REMOVEME
    private int pruneTimes = 0; // REMOVEME
    (int, Move) Search(Board board, Timer timer, int depth, int alpha, int beta, bool maximizingPlayer)
    {
        searchedNodes += 1; // REMOVEME
        if (depth == 0)
        {
            return (Evaluate(board), new Move());
        }
        Move[] moves = Order(board.GetLegalMoves());
        if (maximizingPlayer) // maximizing
        {
            int maxEval = int.MinValue;
            Move bestMove = new Move();
            foreach (Move move in moves)
            {
                board.MakeMove(move);
                var ret = Search(board, timer, depth - 1, alpha, beta, !maximizingPlayer);
                int eval = ret.Item1;
                if (maxEval < eval)
                {
                    maxEval = eval;
                    bestMove = move;
                  
                }
                board.UndoMove(move);
                alpha = Math.Max(alpha, eval);
                if (beta <= alpha)
                {
                    pruneTimes++; //REMOVEME
                    break;
                }
            }
            return (maxEval, bestMove);
        }
        {
            int minEval = int.MaxValue;
            Move bestMove = new Move();
            foreach (Move move in moves)
            {
                board.MakeMove(move);
                var ret = Search(board, timer, depth - 1, alpha, beta, !maximizingPlayer);
                int eval = ret.Item1;
                if (minEval > eval)
                {
                    minEval = eval;
                    bestMove = move;
                    
                }
                beta = Math.Min(beta, eval);
                board.UndoMove(move);
                if (beta <= alpha)
                {
                    pruneTimes++; // REMOVEME
                    break;
                }
            }
            return (minEval, bestMove);
        }
    }

    Move[] Order(Move[] moves)
    {
        Move[] returnThis = new Move[moves.Length];
        Dictionary<Move, int> orderedMoves = new Dictionary<Move, int>();
        foreach (Move move in moves)
        {
            int movePotentialValue = 0;
            if (move.IsCapture)
            {
                movePotentialValue = (int) move.CapturePieceType;
            }
            orderedMoves.Add(move, movePotentialValue);
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

    public int Evaluate(Board board)
    {
        
        // Material
        PieceList[] pieceLists = board.GetAllPieceLists();
        int material = (pieceLists[0].Count - pieceLists[6].Count) * POINT_VALUES[0]
                       + (pieceLists[1].Count - pieceLists[7].Count) * POINT_VALUES[1]
                       + (pieceLists[2].Count - pieceLists[8].Count) * POINT_VALUES[2]
                       + (pieceLists[3].Count - pieceLists[9].Count) * POINT_VALUES[3]
                       + (pieceLists[4].Count - pieceLists[10].Count) * POINT_VALUES[4];
        int perspective = (board.IsWhiteToMove) ? 1 : -1;
        material *= perspective;

        // progression is 0 at early game, 1 at late game
        float progression = 32;
        foreach (PieceList pl in pieceLists) {
            progression -= pl.Count;
        }
        progression /= 32;

        // Mobility and Offense
        int mobility = 0;
        int offense = 0;
        foreach (Move move in board.GetLegalMoves())
        {
            PieceType movingType = move.MovePieceType;
            PieceType capturedType = move.CapturePieceType;
            switch (movingType)
            {
                case PieceType.Pawn:
                    mobility += 100;
                    break;
                case PieceType.Knight:
                    mobility += 350;
                    break;
                case PieceType.Rook:
                    mobility += 100 + (int)(300 * progression);
                    break;
                case PieceType.Bishop:
                    mobility += 250;
                    break;
                case PieceType.Queen:
                    mobility += 500;
                    break;
            }
            // if (capturedType != PieceType.None && movingType != PieceType.None)
            // {
            //     offense += POINT_VALUES[(int)capturedType - 1] * 2 - POINT_VALUES[(int)movingType - 1];
            // }
        }
        
        // King Safety
        if (board.IsWhiteToMove)
        {

        }

        return material + mobility;
    }
    bool MoveIsCheckmate(Board board, Move move)
    {
        board.MakeMove(move);
        bool isMate = board.IsInCheckmate();
        board.UndoMove(move);
        return isMate;
    }
}