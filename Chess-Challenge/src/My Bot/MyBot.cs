using ChessChallenge.API;

public class MyBot : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        Move[] moves = board.GetLegalMoves();
        return moves[0];
    }

    public int Evaluate(Board board)
    {
        PieceList[] pieceLists = board.GetAllPieceLists();
        int material = (pieceLists[0].Count - pieceLists[6].Count) * 100
            + (pieceLists[1].Count - pieceLists[7].Count) * 350
            + (pieceLists[2].Count - pieceLists[8].Count) * 350
            + (pieceLists[3].Count - pieceLists[9].Count) * 525
            + (pieceLists[4].Count - pieceLists[10].Count) * 1000;
        material *= (board.IsWhiteToMove) ? 1 : -1;


        return material;
    }
}