using ChessChallenge.API;
using System;

public class MyBot : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        Move[] moves = board.GetLegalMoves();
        return moves[0];
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

            offense += POINT_VALUES[(int)capturedType] * 2 - POINT_VALUES[(int)movingType];
        }
        
        // King Safety
        if (board.IsWhiteToMove)
        {

        }

        return material + mobility + offense;
    }
}