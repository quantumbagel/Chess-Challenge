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
    int[] PROTECTED_VALUES = new int[] { 50, 35, 30, 10, 4 };
    float[] PROTECTOR_VALUES = new float[] { 8, 4.5f, 4, 3, 2.5f, 2 };

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

    private void FillPartialAttackTable(ref ushort[,] table, Board board, bool isWhite)
    {
        foreach (PieceType pieceType in Enum.GetValues(typeof(PieceType)))
        {
            ulong pieceBB = board.GetPieceBitboard(pieceType, isWhite);
            while (pieceBB != 0)
            {
                Square square = new Square(BitboardHelper.ClearAndGetIndexOfLSB(ref pieceBB));
                ulong attacks = 0;
                ushort data = 0;
                switch (pieceType)
                {
                    case PieceType.Pawn:
                        attacks = BitboardHelper.GetPawnAttacks(square, isWhite);
                        data = 1;
                        break;
                    case PieceType.Knight:
                        attacks = BitboardHelper.GetKnightAttacks(square);
                        data = 4; 
                        break;
                    case PieceType.Bishop:
                        attacks = BitboardHelper.GetSliderAttacks(pieceType, square, board.AllPiecesBitboard);
                        data = 32; 
                        break;
                    case PieceType.Rook:
                        attacks = BitboardHelper.GetSliderAttacks(pieceType, square, board.AllPiecesBitboard);
                        data = 256; 
                        break;
                    case PieceType.Queen:
                        attacks = BitboardHelper.GetSliderAttacks(pieceType, square, board.AllPiecesBitboard);
                        data = 2048; 
                        break;
                    case PieceType.King:
                        attacks = BitboardHelper.GetKingAttacks(square);
                        data = 32768; 
                        break;

                }

                while (attacks != 0)
                {
                    int index = BitboardHelper.ClearAndGetIndexOfLSB(ref attacks);
                    table[(isWhite) ? 1 : 0, index] += data;
                }
            }
        }
    }


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

        //Attack Tables, attack_table[0, x] = black pieces attacking square x, attack_table[1, x] = white pieces attacking square x
        //data format: num pawns [2 bits] - num knights, bishops, rooks, [3 bits each] -  num queens [4 bits] - is king attacking [1 bit]
        ushort[,] attack_table = new ushort[2, 64];
        FillPartialAttackTable(ref attack_table, board, true);
        FillPartialAttackTable(ref attack_table, board, false);


        //Connectivity
        ///*
        int connectivity = 0;
        foreach (PieceList pl in pieceLists)
        {
            if (pl.TypeOfPieceInList == PieceType.King) continue;
            foreach (Piece piece in pl)
            {
                ushort defense_data = attack_table[(piece.IsWhite) ? 1 : 0, piece.Square.Index];
                float to_add = 0;
                to_add += PROTECTED_VALUES[(int)piece.PieceType - 1] * MathF.Pow(8, MathF.Sqrt(defense_data % 4));
                to_add += PROTECTED_VALUES[(int)piece.PieceType - 1] * MathF.Pow(4.5f, MathF.Sqrt((defense_data / 4) % 8));
                to_add += PROTECTED_VALUES[(int)piece.PieceType - 1] * MathF.Pow(4, MathF.Sqrt((defense_data / 32) % 8));
                to_add += PROTECTED_VALUES[(int)piece.PieceType - 1] * MathF.Pow(3, MathF.Sqrt((defense_data / 256) % 8));
                to_add += PROTECTED_VALUES[(int)piece.PieceType - 1] * MathF.Pow(2.5f, MathF.Sqrt(defense_data / 32768));
                to_add += PROTECTED_VALUES[(int)piece.PieceType - 1] * MathF.Pow(2, MathF.Sqrt((defense_data / 2048) % 16));
                connectivity += (int)to_add * ((piece.IsWhite) ? 1 : -1);
            }
        }
        connectivity *= perspective;
        //*/


        // King Safety
        int whiteKingRelativeIndex = board.GetKingSquare(board.IsWhiteToMove).Index;
        int blackKingRelativeIndex = 63 - board.GetKingSquare(board.IsWhiteToMove).Index;
        int whiteKingPositioning = king_mg_table[whiteKingRelativeIndex] + (int)(progression * (king_eg_table[whiteKingRelativeIndex] - king_mg_table[whiteKingRelativeIndex]));
        int blackKingPositioning = king_mg_table[blackKingRelativeIndex] + (int)(progression * (king_eg_table[blackKingRelativeIndex] - king_mg_table[blackKingRelativeIndex]));
        int kingPositioning = (whiteKingPositioning - blackKingPositioning) * perspective;

        return material + connectivity + kingPositioning;
    }
}