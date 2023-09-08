using System;
using System.Collections.Generic;
using System.Linq;
using ChessChallenge.API;

class MyBot930 : IChessBot
//class EvilBot : IChessBot
{
    Board board;
    Timer timer;
    int maxThinkTime;
    int nodes;

    public Move Think(Board brd, Timer tmr)
    {
        board = brd;
        timer = tmr;

        maxThinkTime = Math.Min(timer.MillisecondsRemaining/2, 5000);
        nodes = 0;

        var bestScore = int.MinValue;
        var bestMove = Move.NullMove;

        var allPiecesCount = BitboardHelper.GetNumberOfSetBits(board.AllPiecesBitboard);
        var maxDepth = allPiecesCount > 12 ? 5 : 7;

        var wasInTT = transpositionTable.TryGetValue(board.ZobristKey, out var tt);
        
        for (var depth = 1; depth <= maxDepth; depth++)
        {
            bestScore = Search(depth, int.MinValue, int.MaxValue);
            bestMove = transpositionTable[board.ZobristKey].move;
            Console.WriteLine($"Depth {depth}: value: {bestScore} ({bestMove}) Player: {(board.IsWhiteToMove ? "White" : "Black")}");
            if (bestMove == Move.NullMove && depth == 1)
            {
                // huh?
                Console.WriteLine("Null move at depth 1");
            }
            
            if (bestScore == 100000)
                break; // Stop searching if we found a checkmate

            if (timer.MillisecondsRemaining < maxThinkTime)
                break; // Stop searching if we are running out of time
        }
        
        Console.Write($"Value: {bestScore} ({bestMove}) Player: {(board.IsWhiteToMove ? "White" : "Black")}, nodes: {nodes:#,#} ");
        Console.WriteLine($"table: {transpositionTable.Count:#,#} ");

        if (wasInTT)
        {
            Console.WriteLine($"Was in TT {tt.nodeType}");
            if (tt.move != bestMove || tt.score != bestScore)
            {
                var color = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"TT was diffent! value: {tt.score} ({tt.move}) Player: {(board.IsWhiteToMove ? "White" : "Black")}");
                Console.ForegroundColor = color;
            }
        }
        else
        {
            Console.WriteLine("Was not in TT");
        }

        if (bestMove.MovePieceType is PieceType.Queen or PieceType.Rook)
        {
            //var printMoves = false;
            //board.MakeMove(bestMove);
            if (board.SquareIsAttackedByOpponent(bestMove.TargetSquare))
            {
                var color = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Queen/Rook move was attacked! {bestMove} {bestMove.MovePieceType}");
                Console.ForegroundColor = color;
                //printMoves = true;
            }
            //board.UndoMove(bestMove);
            
            //if (printMoves)
            //    PrintMoves();
        }

        // PrintMoves();

        return bestMove;
    }

    void PrintMoves()
    {
        var isWhiteToMove = board.IsWhiteToMove;
        var undoMoves = new Stack<Move>();
        while (transpositionTable.TryGetValue(board.ZobristKey, out var tt))
        {
            board.MakeMove(tt.move);
            Console.Write($"{(isWhiteToMove ? "W" : "B")}{tt.move.MovePieceType.ToString()[0]}{tt.move.TargetSquare.Name}{(tt.move.IsCapture ? "x" : "")}{(board.IsInCheckmate() ? "#" : board.IsInCheck() ? "+" : "")}({tt.score}) ");
            undoMoves.Push(tt.move);
            isWhiteToMove = !isWhiteToMove;
        }
        Console.WriteLine();
        Console.WriteLine();
        while (undoMoves.Count > 0)
            board.UndoMove(undoMoves.Pop());
    }

    int Search(int depth, int alpha, int beta)
    {
        if (depth == 0 || board.IsInCheckmate() || board.IsDraw())
            return QuiescenceSearch(alpha, beta);
            //return Evaluate();

        var hash = board.ZobristKey;
        if (transpositionTable.TryGetValue(hash, out var tt))
        {
            if (tt.depth >= depth)
            {
                if (tt.nodeType == NodeType.Exact)
                    return tt.score;
                if (tt.nodeType == NodeType.LowerBound && tt.score >= beta)
                    alpha = Math.Max(alpha, tt.score);
                if (tt.nodeType == NodeType.UpperBound && tt.score <= alpha)
                    beta = Math.Min(beta, tt.score);
            }

            if (alpha >= beta)
                return tt.score;
        }

        var bestScore = int.MinValue;
        var bestMove = Move.NullMove;

        foreach (var nextMove in GenerateLegalMoves())
        {
            board.MakeMove(nextMove.Move);
            nodes++;

            var score = -Search(depth - 1, -beta, -alpha);
            
            board.UndoMove(nextMove.Move);
            
            if (score > bestScore)
            {
                bestScore = score;
                bestMove = nextMove.Move;
                // Console.WriteLine($"Candi. Depth {depth}: value: {bestScore} ({bestMove}, {bestMove.MovePieceType})  Player: {(board.IsWhiteToMove ? "White" : "Black")}");
            }
            else if (score == bestScore && score == 100000 && depth == 6)
            {
                Console.WriteLine($"found same score {score} for {bestMove} and {nextMove.Move} at depth {depth}");
            }
            alpha = Math.Max(alpha, bestScore);

            if (alpha >= beta)
                break; // Prune the remaining branches

            //if (timer.MillisecondsRemaining < maxThinkTime)
            //    break; // Stop searching if we are running out of time
        }

        var nodeType = bestScore <= alpha ? NodeType.UpperBound : (bestScore >= beta ? NodeType.LowerBound : NodeType.Exact);
        transpositionTable[hash] = new TranspositionTableEntry(bestScore, depth, nodeType, bestMove);

        return bestScore;
    }

    int QuiescenceSearch(int alpha, int beta)
    {
        var score = Evaluate();
        
        if (board.IsInCheckmate() || board.IsDraw())
            return score;

        if (score >= beta)
            return beta;

        if (alpha < score)
            alpha = score;

        foreach (var nextMove in GenerateLegalCaptures())
        {
            board.MakeMove(nextMove.Move);
            nodes++;

            score = -QuiescenceSearch(-beta, -alpha);

            board.UndoMove(nextMove.Move);

            if (score >= beta)
                return beta;

            if (score > alpha)
                alpha = score;
        }

        return alpha;
    }

    IEnumerable<ExtendedMove> GenerateLegalMoves() =>
        board.GetLegalMoves().Select(Extend)
            .OrderByDescending(x => x.Move.IsCapture)
            .ThenByDescending(x => x.Move.IsCapture ? x.Move.CapturePieceType : PieceType.None)
            .ThenBy(x => x.Move.IsCapture ? x.Move.MovePieceType : PieceType.None)
            .ThenByDescending(x => x.IsCheckMate)
            .ThenByDescending(x => x.IsInCheck)
            .ThenByDescending(x => x.Move.IsCastles)
            .ThenByDescending(x => x.Move.IsPromotion ? x.Move.PromotionPieceType : PieceType.None)
            .ThenByDescending(x => x.Move.IsEnPassant)
            .ThenBy(x => x.Move.MovePieceType)
            .ThenBy(x => x.IsDraw)
            .ThenBy(x => x.IsRepeatedPosition)
            .ThenBy(_ => Random.Shared.Next()) // little randomization to avoid always picking the same move
            ;

    IEnumerable<ExtendedMove> GenerateLegalCaptures() =>
        board.GetLegalMoves(true).Select(Extend)
            .OrderByDescending(x => x.Move.CapturePieceType)
            .ThenByDescending(x => x.Move.CapturePieceType)
            .ThenBy(x => x.Move.MovePieceType)
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
    static readonly int[] pieceValues = new [] { 0, 100, 320, 330, 500, 900, 0 };

    int Evaluate()
    {
        if (board.IsInCheckmate())
            return -100000;

        if (board.IsRepeatedPosition())
            return 1000;

        if (board.IsDraw())
            return 0;

        var totalEvaluation = 0;
        var isMaximizingPlayer = board.IsWhiteToMove;

        if (board.IsInCheck())
        {
            totalEvaluation = -100;
            totalEvaluation -= (8 - board.GetLegalMoves().Count(x => x.MovePieceType == PieceType.King)) * 1000;
        }

        var lists = board.GetAllPieceLists();
        for (var i = 0; i < lists.Length; i++)
        {
            var pieceList = lists[i];
            for (var j = 0; j < pieceList.Count; j++)
            {
                var piece = pieceList[j];
                var pieceValue = pieceValues[(int)piece.PieceType];
                var value = pieceValue + 5;
                totalEvaluation += piece.IsWhite == isMaximizingPlayer ? value : -value;

                if (piece.IsWhite == isMaximizingPlayer && board.SquareIsAttackedByOpponent(piece.Square))
                    totalEvaluation -= pieceValue;
            }
        }

        board.ForceSkipTurn();

        isMaximizingPlayer = board.IsWhiteToMove;

        if (board.IsInCheck())
        {
            totalEvaluation += 100;
            totalEvaluation += (8 - board.GetLegalMoves().Count(x => x.MovePieceType == PieceType.King)) * 1000;
        }

        lists = board.GetAllPieceLists();
        for (var i = 0; i < lists.Length; i++)
        {
            var pieceList = lists[i];
            for (var j = 0; j < pieceList.Count; j++)
            {
                var piece = pieceList[j];
                var pieceValue = pieceValues[(int)piece.PieceType];
                var value = pieceValue + 50;
                totalEvaluation += piece.IsWhite == isMaximizingPlayer ? -value : value;

                if (piece.IsWhite == isMaximizingPlayer && board.SquareIsAttackedByOpponent(piece.Square))
                    totalEvaluation += pieceValue;
            }
        }

        board.UndoSkipTurn();

        return totalEvaluation;
    }

    readonly Dictionary<ulong, TranspositionTableEntry> transpositionTable = new();
    
    record TranspositionTableEntry(int score, int depth, NodeType nodeType, Move move);
    
    enum NodeType
    {
        Exact,
        UpperBound,
        LowerBound
    }
}