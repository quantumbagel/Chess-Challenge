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
        areWeWhite = board.IsWhiteToMove; // Update areWeWhite
        return Search(board, timer, searchDepth, int.MinValue, int.MaxValue, true).Item2;
    }

    private bool areWeWhite;
    private int searchDepth = 6;
    (int, Move) Search(Board board, Timer timer, int depth, int alpha, int beta, bool maximizingPlayer)
    {
        if (depth == 0)
        {
            return (Evaluate(board), new Move()); // Base case, use Evaluate
        }
        if (board.IsInCheckmate())
        {
            // If we are white and white's turn, or black and black's turn, we lose, otherwise we win
            if ((areWeWhite && board.IsWhiteToMove) || (!areWeWhite && !board.IsWhiteToMove))
            {
                return (int.MinValue, new Move());
            }
            else
            {
                return (int.MaxValue, new Move());
            }
        }
        Move[] moves = Order(board.GetLegalMoves()); // the ordered, legal moves
        int bestEval = maximizingPlayer ? int.MinValue : int.MaxValue;
        Move bestMove = new Move();
        foreach (Move move in moves)
        {
            board.MakeMove(move);
            var ret = Search(board, timer, depth - 1, alpha, beta, !maximizingPlayer); // recursion :)
            board.UndoMove(move);
            if (maximizingPlayer) // Our turn
            {
                
                if (ret.Item1 > bestEval)
                {
                    bestEval = ret.Item1;
                    bestMove = move;
                }

                alpha = Math.Max(alpha, ret.Item1);
                
            }
            else // not our turn
            {
                if (ret.Item1 < bestEval)
                {
                    bestEval = ret.Item1;
                    bestMove = move;
                }
                beta = Math.Min(beta, ret.Item1);
            }
            if (beta <= alpha)
            {
                break;
            }
        }

       
        return (bestEval, bestMove);
       
    }

    Move[] Order(Move[] moves)
    {
        Move[] returnThis = new Move[moves.Length];
        Dictionary<Move, int> orderedMoves = new Dictionary<Move, int>();
        foreach (Move move in moves)
        {
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