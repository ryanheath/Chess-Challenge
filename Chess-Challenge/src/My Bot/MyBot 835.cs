using System;
using System.Collections.Generic;
using System.Linq;
using ChessChallenge.API;

class MyBot835 : IChessBot
// class EvilBot : IChessBot
{
    Board board;
    Timer timer;
    int maxThinkTime;

    public Move Think(Board board, Timer timer)
    {
        this.board = board;
        this.timer = timer;

        maxThinkTime = Math.Min(timer.MillisecondsRemaining/2, 5000);

        var bestScore = int.MinValue;
        var bestMove = Move.NullMove;
        var bestDepth = int.MinValue;

        var allPiecesCount = BitboardHelper.GetNumberOfSetBits(board.AllPiecesBitboard);

        foreach (var extMove in GenerateLegalMoves())
        {
            board.MakeMove(extMove.Move);

            var (score, depth) = Search(extMove, allPiecesCount > 12 ? 4 : 6, -int.MaxValue, -(int.MinValue+1));
            score = -score;
            
            board.UndoMove(extMove.Move);
            
            // Console.WriteLine($"  score: {score} ({extMove.Move}) Player: {(board.IsWhiteToMove ? "White" : "Black")}");
            
            if (score > bestScore || (score == bestScore && bestDepth < depth))
            {
                bestDepth = depth;
                bestScore = score;
                bestMove = extMove.Move;
            }

            if (timer.MillisecondsRemaining < maxThinkTime)
                break; // Stop searching if we are running out of time
        }

        // foreach(var vs in positionValues)
        // {
        //     foreach(var ii in vs.Chunk(8))
        //     {
        //         ulong v = 0;
        //         foreach(var i in ii)
        //         {
        //             v <<= 8;
        //             v |= (byte)i;
        //         }
        //         Console.Write($"0x{v:x16}, ");
        //     }
        //     Console.WriteLine();
        // }
        // throw new Exception("Best move not found");

        //Console.WriteLine($"Best score: {bestScore} ({bestMove}) depth:{bestDepth} Player: {(board.IsWhiteToMove ? "White" : "Black")}");
        //Console.WriteLine($"fen {board.GetFenString()}");

        return bestMove;
    }

    (int score, int depth) Search(ExtendedMove extMove, int depth, int alpha, int beta)
    {
        //if (depth == 0 || board.IsInCheckmate() || board.IsDraw()) // extMove.IsCheckMate || extMove.IsDraw)
        if (depth == 0 || extMove.IsCheckMate || extMove.IsDraw)
            return (Evaluate(extMove), depth);

        var hash = board.ZobristKey;
        if (transpositionTable.TryGetValue(hash, out var tt))
        {
            if (tt.depth >= depth)
            {
                if (tt.nodeType == NodeType.Exact)
                    return (tt.score, tt.depth);
                if (tt.nodeType == NodeType.LowerBound && tt.score >= beta)
                    alpha = Math.Max(alpha, tt.score);
                if (tt.nodeType == NodeType.UpperBound && tt.score <= alpha)
                    beta = Math.Min(beta, tt.score);
            }

            if (alpha >= beta)
                return (tt.score, tt.depth);
        }

        var bestScore = int.MinValue;
        var bestDepth = int.MinValue;

        foreach (var nextMove in GenerateLegalMoves())
        {
            board.MakeMove(nextMove.Move);

            var (score, _) = Search(nextMove, depth - 1, -beta, -alpha);
            score = -score;
            
            board.UndoMove(nextMove.Move);
            
            if (score > bestScore || (score == bestScore && bestDepth < depth - 1))
            {
                bestDepth = depth - 1;
                bestScore = score;
            }

            alpha = Math.Max(alpha, bestScore);

            if (alpha >= beta)
                break; // Prune the remaining branches

            if (timer.MillisecondsRemaining < maxThinkTime)
                break; // Stop searching if we are running out of time
        }

        var nodeType = bestScore <= alpha ? NodeType.UpperBound : (bestScore >= beta ? NodeType.LowerBound : NodeType.Exact);
        transpositionTable[hash] = new TranspositionTableEntry(bestScore, depth, nodeType);

        return (bestScore, bestDepth);
    }

    IEnumerable<ExtendedMove> GenerateLegalMoves() =>
        board.GetLegalMoves().Select(Extend)
            .OrderByDescending(x => x.Move.IsCapture)
            .ThenByDescending(x => x.IsCheckMate)
            //.ThenByDescending(x => x.IsInCheck)
            .ThenByDescending(x => x.Move.IsCastles)
            .ThenByDescending(x => x.Move.IsPromotion ? x.Move.PromotionPieceType : PieceType.None)
            .ThenByDescending(x => x.Move.IsEnPassant)
            //.ThenBy(x => x.Move.MovePieceType)
            .ThenBy(x => x.IsDraw)
            .ThenBy(x => x.IsRepeatedPosition)
            .ThenBy(_ => Random.Shared.Next()) // little randomization to avoid always picking the same move
            ;

    record ExtendedMove(Move Move, bool IsCheckMate, bool IsInCheck, bool IsDraw, bool IsRepeatedPosition);

    ExtendedMove Extend(Move move)
    {
        board.MakeMove(move);
        var isCheckMate = board.IsInCheckmate();
        var isInCheck = board.IsInCheck();
        var isDraw = board.IsDraw();
        var IsRepeatedPosition = board.IsRepeatedPosition();
        board.UndoMove(move);
        return new (move, isCheckMate, isInCheck, isDraw, IsRepeatedPosition);
    }
    
    // Assign values to pieces
    static readonly int[] pieceValues = new [] { 0, 100, 320, 330, 500, 900, 20000 };

    int Evaluate(ExtendedMove extMove)
    {
        if (extMove.IsCheckMate)
        //if (board.IsInCheckmate())
            return -10000;

        if (extMove.IsRepeatedPosition)
        //if (board.IsRepeatedPosition())
            return 1000;

        if (extMove.IsDraw)
        //if (board.IsDraw())
            return 0;

        var totalEvaluation = 0;
        
        board.ForceSkipTurn();

        var isMaximizingPlayer = board.IsWhiteToMove;

        var lists = board.GetAllPieceLists();
        for (var i = 0; i < lists.Length; i++)
        {
            var pieceList = lists[i];
            for (var j = 0; j < pieceList.Count; j++)
            {
                var piece = pieceList[j];
                var pieceValue = pieceValues[(int)piece.PieceType];
                var index = piece.IsWhite ? 63 - piece.Square.Index : piece.Square.Index;
                var positionValue =
                    (int)(positionValues[((int)piece.PieceType - 1) * 8 + (index >> 3)] >> ((index & 7) << 1)) & 0xFF;

                var value = pieceValue + /* positionValue + */ 50;
                totalEvaluation += piece.IsWhite == isMaximizingPlayer ? -value : value;

                if (piece.IsWhite == isMaximizingPlayer && board.SquareIsAttackedByOpponent(piece.Square))
                    totalEvaluation += pieceValue;
            }
        }

        board.UndoSkipTurn();

        return totalEvaluation;
    }

    static readonly ulong[] positionValues = new ulong[]
    {
        // Pawn
        0x0000000000000000, 0x3232323232323232, 0x0a0a141e1e140a0a, 0x05050a19190a0505, 0x0000001414000000, 0x05fbf60000f6fb05, 0x050a0aecec0a0a05, 0x0000000000000000,
        // Knight
        0xced8e2e2e2e2d8ce, 0xd8ec00000000ecd8, 0xe2000a0f0f0a00e2, 0xe2050f14140f05e2, 0xe2000f14140f00e2, 0xe2050a0f0f0a05e2, 0xd8ec00050500ecd8, 0xced8e2e2e2e2d8ce,
        // Bishop
        0xecf6f6f6f6f6f6ec, 0xf6000000000000f6, 0xf600050a0a0500f6, 0xf605050a0a0505f6, 0xf6000a0a0a0a00f6, 0xf60a0a0a0a0a0af6, 0xf6050000000005f6, 0xecf6f6f6f6f6f6ec,
        // Rook
        0x0000000000000000, 0x050a0a0a0a0a0a05, 0xfb000000000000fb, 0xfb000000000000fb, 0xfb000000000000fb, 0xfb000000000000fb, 0xfb000000000000fb, 0x0000000505000000,
        // Queen
        0xecf6f6fbfbf6f6ec, 0xf6000000000000f6, 0xf6000505050500f6, 0xfb000505050500fb, 0x00000505050500fb, 0xf6050505050500f6, 0xf6000500000000f6, 0xecf6f6fbfbf6f6ec,
        // King
        0xe2d8d8ceced8d8e2, 0xe2d8d8ceced8d8e2, 0xe2d8d8ceced8d8e2, 0xe2d8d8ceced8d8e2, 0xece2e2d8d8e2e2ec, 0xf6ececececececf6, 0x1414000000001414, 0x141e0a00000a1e14,
    };

    // static readonly int[][] positionValues = new []
    //     {
    //         // Empty
    //         new int [] {},
    //         // Pawn
    //         new [] 
    //             {   0,   0,   0,   0,   0,   0,   0,   0,
    //                 50,  50,  50,  50,  50,  50,  50,  50,
    //                 10,  10,  20,  30,  30,  20,  10,  10,
    //                  5,   5,  10,  25,  25,  10,   5,   5,
    //                  0,   0,   0,  20,  20,   0,   0,   0,
    //                  5,  -5, -10,   0,   0, -10,  -5,   5,
    //                  5,  10,  10, -20, -20,  10,  10,   5,
    //                  0,   0,   0,   0,   0,   0,   0,   0 },
    //         // Knight
    //         new []  
    //             { -50, -40, -30, -30, -30, -30, -40, -50,
    //               -40, -20,   0,   0,   0,   0, -20, -40,
    //               -30,   0,  10,  15,  15,  10,   0, -30,
    //               -30,   5,  15,  20,  20,  15,   5, -30,
    //               -30,   0,  15,  20,  20,  15,   0, -30,
    //               -30,   5,  10,  15,  15,  10,   5, -30,
    //               -40, -20,   0,   5,   5,   0, -20, -40,
    //               -50, -40, -30, -30, -30, -30, -40, -50 },
    //         // Bishop
    //         new [] 
    //             { -20, -10, -10, -10, -10, -10, -10, -20,
    //               -10,   0,   0,   0,   0,   0,   0, -10,
    //               -10,   0,   5,  10,  10,   5,   0, -10,
    //               -10,   5,   5,  10,  10,   5,   5, -10,
    //               -10,   0,  10,  10,  10,  10,   0, -10,
    //               -10,  10,  10,  10,  10,  10,  10, -10,
    //               -10,   5,   0,   0,   0,   0,   5, -10,
    //               -20, -10, -10, -10, -10, -10, -10, -20 },
    //         // Rook
    //         new [] 
    //             {   0,   0,   0,   0,   0,   0,   0,   0,
    //                 5,  10,  10,  10,  10,  10,  10,   5,
    //                -5,   0,   0,   0,   0,   0,   0,  -5,
    //                -5,   0,   0,   0,   0,   0,   0,  -5,
    //                -5,   0,   0,   0,   0,   0,   0,  -5,
    //                -5,   0,   0,   0,   0,   0,   0,  -5,
    //                -5,   0,   0,   0,   0,   0,   0,  -5,
    //                 0,   0,   0,   5,   5,   0,   0,   0 },
    //         // Queen
    //         new [] 
    //             { -20, -10, -10,  -5,  -5, -10, -10, -20,
    //               -10,   0,   0,   0,   0,   0,   0, -10,
    //               -10,   0,   5,   5,   5,   5,   0, -10,
    //                -5,   0,   5,   5,   5,   5,   0,  -5,
    //                 0,   0,   5,   5,   5,   5,   0,  -5,
    //               -10,   5,   5,   5,   5,   5,   0, -10,
    //               -10,   0,   5,   0,   0,   0,   0, -10,
    //               -20, -10, -10,  -5,  -5, -10, -10, -20 },
    //         // King
    //         new [] 
    //             {
    //               -30, -40, -40, -50, -50, -40, -40, -30,
    //               -30, -40, -40, -50, -50, -40, -40, -30,
    //               -30, -40, -40, -50, -50, -40, -40, -30,
    //               -30, -40, -40, -50, -50, -40, -40, -30,
    //               -20, -30, -30, -40, -40, -30, -30, -20,
    //               -10, -20, -20, -20, -20, -20, -20, -10,
    //                20,  20,   0,   0,   0,   0,  20,  20,
    //                20,  30,  10,   0,   0,  10,  30,  20,
    //             }
    //     };

    readonly Dictionary<ulong, TranspositionTableEntry> transpositionTable = new();
    
    record TranspositionTableEntry(int score, int depth, NodeType nodeType);
    
    enum NodeType
    {
        Exact,
        UpperBound,
        LowerBound
    }
}