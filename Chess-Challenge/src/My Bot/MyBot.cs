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
    private int millisecondsPerSearch = 3000;
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

            if (timer.MillisecondsElapsedThisTurn >= millisecondsPerSearch) break;
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


    public int Evaluate(Board board)
    {

        if (board.IsInCheckmate()) return -100000000;
        

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


        // King Safety
        int whiteKingRelativeIndex = board.GetKingSquare(board.IsWhiteToMove).Index;
        int blackKingRelativeIndex = 63 - board.GetKingSquare(board.IsWhiteToMove).Index;
        int whiteKingPositioning = king_mg_table[whiteKingRelativeIndex] + (int)(progression * (king_eg_table[whiteKingRelativeIndex] - king_mg_table[whiteKingRelativeIndex]));
        int blackKingPositioning = king_mg_table[blackKingRelativeIndex] + (int)(progression * (king_eg_table[blackKingRelativeIndex] - king_mg_table[blackKingRelativeIndex]));
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
                endgameBonus += 100 - 20 * (chebyshev - 2);
            }
        }
        endgameBonus = (int)(endgameBonus * MathF.Pow(progression, 1.5f));

        return material + kingPositioning + pawnDevelopment;
    }
}