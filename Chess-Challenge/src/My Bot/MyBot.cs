using ChessChallenge.API;
using System;

public class MyBot : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        Move[] moves = board.GetLegalMoves();
        Move bestMove = Move.NullMove;
        int bestMoveEval = int.MinValue;
        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int eval = Evaluate(board);
            if (eval > bestMoveEval)
            {
                bestMove = move;
                bestMoveEval = eval;
            }
            board.UndoMove(move);
        }

        return bestMove;
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

        // Mobility
        int mobility = 0;
        int offense = 0;
        bool skipped = board.TrySkipTurn();
        if (skipped)
        {
            foreach (Move move in board.GetLegalMoves())
            {
                switch (move.MovePieceType)
                {
                    case PieceType.Knight:
                        mobility += 175;
                        break;
                    case PieceType.Rook:
                        mobility += 50 + (int)(150 * progression);
                        break;
                    case PieceType.Bishop:
                        mobility += 125;
                        break;
                    case PieceType.Queen:
                        mobility += 250;
                        break;
                }

                if (move.IsCapture)
                {
                    offense += (int) MathF.Max(0, POINT_VALUES[(int)move.CapturePieceType - 1] - POINT_VALUES[(int)move.MovePieceType - 1]);
                }
            }

            board.UndoSkipTurn();
        }


        // King Safety
        Square kingSquare = board.GetKingSquare(board.IsWhiteToMove);
        int kingRelativeIndex = (board.IsWhiteToMove) ? kingSquare.Index : 64 - kingSquare.Index;
        int kingPositioning = king_mg_table[kingRelativeIndex] + (int)(progression * (king_eg_table[kingRelativeIndex] - king_mg_table[kingRelativeIndex]));

        return material + mobility + offense + kingPositioning;
    }
}