using System;
using System.Collections.Generic;
//using System.Collections.Generic;
using System.Linq;
using ChessChallenge.API;
//using ChessChallenge.Chess;
//using Board = ChessChallenge.API.Board;
//using Move = ChessChallenge.API.Move;

class MyBotGPT : IChessBot
// class EvilBot : IChessBot
{
    private const int MaxDepth = 4;
    private const int QuiescenceDepth = 2;

    public Move Think(Board board, Timer timer)
    {
        var legalMoves = GenerateLegalMoves(board);
        var bestScore = int.MinValue;
        var bestMove = legalMoves.First();

        foreach (var move in legalMoves)
        {
            board.MakeMove(move);

            int score;
            if (board.IsInCheck())
            {
                score = -AlphaBetaSearch(board, MaxDepth - 1, int.MinValue + 1, int.MaxValue, true);
            }
            else
            {
                // Null Move Pruning (NMP)
                score = -AlphaBetaSearchWithNullMove(board, MaxDepth - 2, int.MinValue + 1, int.MaxValue, true);
            }
            
            board.UndoMove(move);

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }
        
        Console.WriteLine($"Best score: {bestScore} Best {bestMove} Piece: {bestMove.MovePieceType} Player: {(board.IsWhiteToMove ? "White" : "Black")}");

        return bestMove;
    }

    IEnumerable<Move> GenerateLegalMoves(Board board) =>
        board.GetLegalMoves()
            .OrderByDescending(x => x.IsCapture)
            .ThenByDescending(x => x.IsCastles)
            .ThenByDescending(x => x.IsPromotion)
            .ThenByDescending(x => x.IsEnPassant)
            .ThenBy(x => x.MovePieceType);

    private int AlphaBetaSearch(Board board, int depth, int alpha, int beta, bool isMaximizingPlayer)
    {
        if (depth <= 0)
        {
            var qs = QuiescenceSearch(board, QuiescenceDepth, alpha, beta);
            // Console.WriteLine($"   qs value: {qs} Player: {(board.IsWhiteToMove ? "White" : "Black")}");
            return qs;
        }

        var legalMoves = GenerateLegalMoves(board);
        var bestValue = int.MinValue;

        foreach (var move in legalMoves)
        {
            board.MakeMove(move);

            int value;
            if (depth >= 2 && isMaximizingPlayer)
            {
                // Late Move Reductions (LMR)
                value = -AlphaBetaSearch(board, depth - 1, -alpha - 1, -alpha, !isMaximizingPlayer);
                if (value > alpha)
                {
                    // Full-depth search after LMR
                    value = -AlphaBetaSearch(board, depth - 1, -beta, -alpha, !isMaximizingPlayer);
                }
            }
            else
            {
                value = -AlphaBetaSearch(board, depth - 1, -beta, -alpha, !isMaximizingPlayer);
            }
            
            board.UndoMove(move);

            bestValue = Math.Max(bestValue, value);
            alpha = Math.Max(alpha, value);

            if (alpha >= beta)
            {
                break; // Beta cutoff
            }
        }
        
        Console.WriteLine($"   value: {bestValue} Player: {(board.IsWhiteToMove ? "White" : "Black")}");

        return bestValue;
    }

    private int AlphaBetaSearchWithNullMove(Board board, int depth, int alpha, int beta, bool isMaximizingPlayer)
    {
        if (depth <= 0)
        {
            return QuiescenceSearch(board, QuiescenceDepth, alpha, beta);
        }

        // Null Move Pruning
        board.ForceSkipTurn();
        var nullMoveReduction = (depth >= 4) ? 2 : 1;

        var value = -AlphaBetaSearch(board, depth - nullMoveReduction - 1, -beta, -beta + 1, !isMaximizingPlayer);
        board.UndoSkipTurn();

        if (value >= beta)
        {
            return beta; // Cut-off
        }

        // Continue with regular search
        var result = AlphaBetaSearch(board, depth, alpha, beta, !isMaximizingPlayer);


        return result;
    }

    private int QuiescenceSearch(Board board, int depth, int alpha, int beta)
    {
        if (depth <= 0 || board.IsDraw() || board.IsInCheckmate())
        {
            return Evaluate(board);
        }

        var captureMoves = GenerateLegalMoves(board).Where(x => x.IsCapture);

        foreach (var move in captureMoves)
        {
            board.MakeMove(move);
            var score = -QuiescenceSearch(board, depth - 1, -beta, -alpha);
            board.UndoMove(move);

            if (score >= beta)
            {
                return beta; // Beta cutoff
            }

            if (score > alpha)
            {
                alpha = score;
            }
        }

        return Evaluate(board);
    }
    
    // Assign values to pieces
    static readonly Dictionary<PieceType,int> pieceValues = new()
    {
        { PieceType.Pawn, 100 },
        { PieceType.Knight, 320 },
        { PieceType.Bishop, 330 },
        { PieceType.Rook, 500 },
        { PieceType.Queen, 900 },
        { PieceType.King, 20000 } // A high value to prioritize king safety
    };

    private int Evaluate(Board board)
    {
        if (board.IsInCheckmate())
            return -10000;
        if (board.IsDraw())
            return 0;
        
        var totalEvaluation = 0;
        var isMaximizingPlayer = board.IsWhiteToMove;

        foreach (var pieceList in board.GetAllPieceLists())
        foreach (var piece in pieceList)
        {
            var pieceValue = -pieceValues[piece.PieceType];
            var positionValue = -GetPositionValue(piece);

            totalEvaluation += (piece.IsWhite == isMaximizingPlayer) ? pieceValue + positionValue : -pieceValue - positionValue;
        }

        return totalEvaluation;
    }

    static readonly Dictionary<PieceType,int[]> positionValues = new()
        {
            { PieceType.Pawn, new [] 
                {   0,   0,   0,   0,   0,   0,   0,   0,
                    50,  50,  50,  50,  50,  50,  50,  50,
                    10,  10,  20,  30,  30,  20,  10,  10,
                     5,   5,  10,  25,  25,  10,   5,   5,
                     0,   0,   0,  20,  20,   0,   0,   0,
                     5,  -5, -10,   0,   0, -10,  -5,   5,
                     5,  10,  10, -20, -20,  10,  10,   5,
                     0,   0,   0,   0,   0,   0,   0,   0 }
            },

            { PieceType.Knight, new []  
                { -50, -40, -30, -30, -30, -30, -40, -50,
                  -40, -20,   0,   0,   0,   0, -20, -40,
                  -30,   0,  10,  15,  15,  10,   0, -30,
                  -30,   5,  15,  20,  20,  15,   5, -30,
                  -30,   0,  15,  20,  20,  15,   0, -30,
                  -30,   5,  10,  15,  15,  10,   5, -30,
                  -40, -20,   0,   5,   5,   0, -20, -40,
                  -50, -40, -30, -30, -30, -30, -40, -50 }
            },

            { PieceType.Bishop, new [] 
                { -20, -10, -10, -10, -10, -10, -10, -20,
                  -10,   0,   0,   0,   0,   0,   0, -10,
                  -10,   0,   5,  10,  10,   5,   0, -10,
                  -10,   5,   5,  10,  10,   5,   5, -10,
                  -10,   0,  10,  10,  10,  10,   0, -10,
                  -10,  10,  10,  10,  10,  10,  10, -10,
                  -10,   5,   0,   0,   0,   0,   5, -10,
                  -20, -10, -10, -10, -10, -10, -10, -20 }
            },

            { PieceType.Rook, new [] 
                {   0,   0,   0,   0,   0,   0,   0,   0,
                    5,  10,  10,  10,  10,  10,  10,   5,
                   -5,   0,   0,   0,   0,   0,   0,  -5,
                   -5,   0,   0,   0,   0,   0,   0,  -5,
                   -5,   0,   0,   0,   0,   0,   0,  -5,
                   -5,   0,   0,   0,   0,   0,   0,  -5,
                   -5,   0,   0,   0,   0,   0,   0,  -5,
                    0,   0,   0,   5,   5,   0,   0,   0 }
            },

            { PieceType.Queen, new [] 
                { -20, -10, -10,  -5,  -5, -10, -10, -20,
                  -10,   0,   0,   0,   0,   0,   0, -10,
                  -10,   0,   5,   5,   5,   5,   0, -10,
                   -5,   0,   5,   5,   5,   5,   0,  -5,
                    0,   0,   5,   5,   5,   5,   0,  -5,
                  -10,   5,   5,   5,   5,   5,   0, -10,
                  -10,   0,   5,   0,   0,   0,   0, -10,
                  -20, -10, -10,  -5,  -5, -10, -10, -20 }
            },

            { PieceType.King, new [] 
                {
                   20,  30,  10,   0,   0,  10,  30,  20,
                   20,  20,   0,   0,   0,   0,  20,  20,
                  -10, -20, -20, -20, -20, -20, -20, -10,
                  -20, -30, -30, -40, -40, -30, -30, -20,
                  -30, -40, -40, -50, -50, -40, -40, -30,
                  -30, -40, -40, -50, -50, -40, -40, -30,
                  -30, -40, -40, -50, -50, -40, -40, -30,
                  -30, -40, -40, -50, -50, -40, -40, -30 
                }
            }
        };

    private int GetPositionValue(Piece piece) => 
        positionValues[piece.PieceType][piece.IsWhite ? piece.Square.Index : 63 - piece.Square.Index];
}

public class MyBotNegaMaxWithPVSAndLMRAndNMPAndRFP : IChessBot // NegaMaxWithPVSAndLMRAndNMPAndRFP
{
    private const int R = 2; // Reduction factor for Late Move Reduction
    private const int MIN_DEPTH_FOR_NULL_MOVE = 2;
    private const int RFP_MARGIN = 200; // Margin for Reverse Futility Pruning

    public Move Think(Board board, Timer timer)
    {
        return FindBestMove(board, 8);
    }

    public Move FindBestMove(Board board, int maxDepth)
    {
        int bestScore = int.MinValue;
        Move bestMove = Move.NullMove;
        int color = board.IsWhiteToMove ? 1 : -1;

        foreach (var move in GetLegalMoves(board))
        {
            board.MakeMove(move);

            int depth = maxDepth - 1;
            if (depth > 1 && !move.IsCapture) // Apply Late Move Reduction
                depth = Math.Max(1, depth - R);

            int score = -PVS(board, depth, -int.MaxValue, -bestScore, -color);
            board.UndoMove(move);

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }
        
        Console.WriteLine($"Best score: {bestScore} Best move: {bestMove} Player: {(board.IsWhiteToMove ? "White" : "Black")}");

        return bestMove;
    }

    IEnumerable<Move> GetLegalMoves(Board board) =>
        board.GetLegalMoves()
            .OrderByDescending(x => x.IsCapture)
            .ThenByDescending(x => x.IsCastles)
            .ThenByDescending(x => x.IsPromotion)
            .ThenByDescending(x => x.IsEnPassant)
            .ThenBy(x => x.MovePieceType);

    private int PVS(Board board, int depth, int alpha, int beta, int color)
    {
        if (depth <= 0 || board.IsDraw() || board.IsInCheckmate())
        {
            return color * EvaluatePosition(board);
        }

        if (depth >= MIN_DEPTH_FOR_NULL_MOVE && !board.IsInCheck())
        {
            board.ForceSkipTurn();
            int nullMoveScore = -PVS(board, depth - 1 - R, -beta, -beta + 1, -color);
            board.UndoSkipTurn();

            if (nullMoveScore >= beta)
                return beta; // Null Move Pruning
        }

        if (depth <= 2 && EvaluatePosition(board) + RFP_MARGIN <= alpha)
            return alpha; // Reverse Futility Pruning

        int originalAlpha = alpha;
        int score;

        foreach (var move in GetLegalMoves(board))
        {
            board.MakeMove(move);

            if (alpha == originalAlpha)
                score = -PVS(board, depth - 1, -beta, -alpha, -color);
            else
                score = -PVS(board, depth - 1, -alpha - 1, -alpha, -color);

            if (score > alpha && score < beta)
            {
                score = -PVS(board, depth - 1, -beta, -score, -color);
            }

            board.UndoMove(move);

            alpha = Math.Max(alpha, score);

            if (alpha >= beta)
                break; // Beta cut-off
        }

        return alpha;
    }

    private int EvaluatePosition(Board board)
    {
        var score = 0;

        if (board.IsInCheckmate())
            return 10000;
        if (board.IsDraw())
            return 0;
        
        board.ForceSkipTurn();

        foreach (var pieceList in board.GetAllPieceLists())
        foreach (var piece in pieceList)
        {
            var piecePositionValue = GetPoints(piece.PieceType);

            if (piece.IsWhite == board.IsWhiteToMove)
            {
                if (!board.SquareIsAttackedByOpponent(piece.Square))
                    score += piecePositionValue;
                else
                    score += piecePositionValue / 2;
            }
            else
                score -= piecePositionValue;
        }
        
        board.UndoSkipTurn();

        return score;
    }
    
    static int GetPoints(PieceType pieceType) => Points[(int)pieceType];
    static readonly int[] Points = { 0, 1, 10, 15, 50, 90, 100 };
}


public class MyBotSmallBrain : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        var bestMove = Move.NullMove;
        var bestScore = int.MinValue;
        
        foreach (var move in board.GetLegalMoves()
                     .OrderByDescending(x => IsCheckMate(board, x).checkMate)
                     .ThenBy(x => IsCheckMate(board, x).draw)
                     .ThenByDescending(x => x.IsCapture)
                     .ThenByDescending(x => x.IsPromotion)
                     .ThenByDescending(x => x.IsCastles)
                     .ThenByDescending(x => x.IsEnPassant)
                     .ThenByDescending(x => board.SquareIsAttackedByOpponent(x.StartSquare))
                     .ThenBy(x => board.SquareIsAttackedByOpponent(x.TargetSquare))
                     .ThenBy(x => x.MovePieceType)
            )
        {
            var score = -NegaMax(board, 6, int.MinValue, int.MaxValue, board.IsWhiteToMove ? 1 : -1);
            if (bestScore >= score) continue;
            bestScore = score;
            bestMove = move;
        }
        
        Console.WriteLine($"Best score: {bestScore} Best move: {bestMove}");

        return bestMove;
    }
    
    int NegaMax(Board board, int depth, int alpha, int beta, int color)
    {
        if (depth == 0 || board.IsDraw() || board.IsInCheckmate())
        {
            return color * EvaluatePosition(board);
        }

        var maxScore = int.MinValue;
        foreach (var move in board.GetLegalMoves()
                     .OrderByDescending(x => IsCheckMate(board, x).checkMate)
                     .ThenBy(x => IsCheckMate(board, x).draw)
                     .ThenByDescending(x => x.IsCapture)
                     .ThenByDescending(x => x.IsPromotion)
                     .ThenByDescending(x => x.IsCastles)
                     .ThenByDescending(x => x.IsEnPassant)
                     .ThenByDescending(x => board.SquareIsAttackedByOpponent(x.StartSquare))
                     .ThenBy(x => board.SquareIsAttackedByOpponent(x.TargetSquare))
                     .ThenBy(x => x.MovePieceType))
        {
            board.MakeMove(move);
            var score = -NegaMax(board, depth - 1, -beta, -alpha, -color);
            board.UndoMove(move);

            maxScore = Math.Max(maxScore, score);
            alpha = Math.Max(alpha, score);

            if (alpha >= beta)
                break; // Beta cut-off
        }

        return maxScore;
    }

    (bool checkMate, bool draw) IsCheckMate(Board board, Move move)
    {
        board.MakeMove(move);
        var result = (board.IsInCheckmate(), board.IsDraw());
        board.UndoMove(move);
        return result;
    }
    
    int EvaluatePosition(Board board)
    {
        var score = 0;

        if (board.IsInCheckmate())
            return -10000;
        if (board.IsDraw())
            return 0;

        foreach (var pieceList in board.GetAllPieceLists())
        foreach (var piece in pieceList)
        {
            var piecePositionValue = (int)piece.PieceType;
            
            if (piece.IsWhite == board.IsWhiteToMove)
            {
                if (!board.SquareIsAttackedByOpponent(piece.Square))
                    score += piecePositionValue;
            }
            else
                score -= piecePositionValue;
        }

        return score;
    }
}
/*
public class MyBot2 : IChessBot
{
    Board board;
    Timer timer;
    Dictionary<ulong, TranspositionEntry> transpositionTable = new();

    public Move Thinkd(Board board, Timer timer)
    {
        this.board = board;
        Console.WriteLine($"score: {EvaluatePosition()}");
        Console.WriteLine($"{board}");
        Console.WriteLine();
        
        foreach (var move in this.board.GetLegalMoves())
        {
            board.MakeMove(move);
            Console.WriteLine($"{move} score: {EvaluatePosition()} for player: {(board.IsWhiteToMove ? "White" : "Black")}");
            Console.WriteLine($"{board}");
            Console.WriteLine();
            board.ForceSkipTurn();
            Console.WriteLine($"{move} score: {EvaluatePosition()} for player: {(board.IsWhiteToMove ? "White" : "Black")}");
            Console.WriteLine($"{board}");
            board.UndoSkipTurn();
            board.UndoMove(move);
        }
        
        return Move.NullMove;
    }

    public Move Think(Board board, Timer timer)
    {
        this.board = board;
        this.timer = timer;
        var bestScore = int.MinValue;
        var bestMove = Move.NullMove;
        var color = board.IsWhiteToMove ? 1 : -1;

        for (var depth = 1; depth <= 6; depth++)
        {
            var alpha = int.MinValue;
            var beta = int.MaxValue;

            foreach (var move in board.GetLegalMoves()
                         .OrderByDescending(x => x.IsCapture)
                         .ThenByDescending(x => x.IsPromotion)
                         .ThenByDescending(x => x.IsCastles)
                         .ThenByDescending(x => x.IsEnPassant)
                         .ThenByDescending(x => board.SquareIsAttackedByOpponent(x.StartSquare))
                         .ThenBy(x => board.SquareIsAttackedByOpponent(x.TargetSquare)))
            {
                board.MakeMove(move);
                var score = -NegaMax(depth - 1, -beta, -alpha, -color);
                
                Console.WriteLine($"Player: {(board.IsWhiteToMove ? "White" : "Black")} score: {score} {move} depth {depth}");
                // Console.WriteLine($"{board}");
                
                board.UndoMove(move);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }

                alpha = Math.Max(alpha, score);
            }
        }
        
        Console.WriteLine($"Player: {(board.IsWhiteToMove ? "White" : "Black")} Best score: {bestScore} Best move: {bestMove}");

        return bestMove;
    }
    
    int NegaMax(int depth, int alpha, int beta, int color)
    {
        // Check transposition table for cached results
        if (transpositionTable.TryGetValue(board.ZobristKey, out var ttEntry))
        {
            if (ttEntry.Depth >= depth)
            {
                if (ttEntry.Type == NodeType.Exact)
                    return ttEntry.Score;
                if (ttEntry.Type == NodeType.LowerBound && ttEntry.Score >= beta)
                    return ttEntry.Score;
                if (ttEntry.Type == NodeType.UpperBound && ttEntry.Score <= alpha)
                    return ttEntry.Score;
            }
        }
        
        if (depth == 0 || board.IsDraw() || board.IsInCheckmate())
        {
            return color * QuiescenceSearch(alpha, beta, color, 0); // Depth-limited Quiescence
        }

        var maxScore = int.MinValue;
        foreach (var move in board.GetLegalMoves()
                     .OrderByDescending(x => x.IsCapture)
                     .ThenByDescending(x => x.IsPromotion)
                     .ThenByDescending(x => x.IsCastles)
                     .ThenByDescending(x => x.IsEnPassant)
                     .ThenByDescending(x => board.SquareIsAttackedByOpponent(x.StartSquare))
                     .ThenBy(x => board.SquareIsAttackedByOpponent(x.TargetSquare)))
        {
            board.MakeMove(move);
            var score = -NegaMax(depth - 1, -beta, -alpha, -color);
            board.UndoMove(move);

            maxScore = Math.Max(maxScore, score);
            alpha = Math.Max(alpha, score);

            if (alpha >= beta)
                break; // Beta cut-off
        }
        
        // Store result in transposition table
        var nodeType = (maxScore <= alpha) ? NodeType.UpperBound :
            (maxScore >= beta) ? NodeType.LowerBound :
            NodeType.Exact;
        transpositionTable[board.ZobristKey] = new TranspositionEntry(nodeType, maxScore, depth, Move.NullMove);

        return maxScore;
    }

    private int QuiescenceSearch(int alpha, int beta, int color, int depth)
    {
        if (depth == 0 || board.IsDraw() || board.IsInCheckmate())
        {
            return color * EvaluatePosition();
        }

        var standPat = color * EvaluatePosition();

        if (color == 1) // Maximizer
        {
            alpha = Math.Max(alpha, standPat);
            if (alpha >= beta)
                return alpha;
        }
        else // Minimizer
        {
            beta = Math.Min(beta, standPat);
            if (alpha >= beta)
                return beta;
        }

        foreach (var move in board.GetLegalMoves()
                     .OrderByDescending(x => x.IsCapture)
                     .ThenByDescending(x => x.IsPromotion)
                     .ThenByDescending(x => x.IsCastles)
                     .ThenByDescending(x => x.IsEnPassant)
                     .ThenByDescending(x => board.SquareIsAttackedByOpponent(x.StartSquare))
                     .ThenBy(x => board.SquareIsAttackedByOpponent(x.TargetSquare)))
        {
            board.MakeMove(move);
            var score = -QuiescenceSearch(-beta, -alpha, -color, depth - 1);
            board.UndoMove(move);

            if (color == 1) // Maximizer
            {
                alpha = Math.Max(alpha, score);
                if (alpha >= beta)
                    return alpha;
            }
            else // Minimizer
            {
                beta = Math.Min(beta, score);
                if (alpha >= beta)
                    return beta;
            }
        }

        return color == 1 ? alpha : beta;
    }

    static readonly int[][] PawnPositions = 
    {
        new [] { 00, 00, 00, 00, 00, 00, 00, 00 },
        new [] { 05, 10, 10, -20, -20, 10, 10, 05 },
        new [] { 05, -05, -10, 00, 00, -10, -05, 05 },
        new [] { 00, 00, 00, 20, 20, 00, 00, 00 },
        new [] { 05, 05, 10, 25, 25, 10, 05, 05 },
        new [] { 10, 10, 20, 30, 30, 20, 10, 10 },
        new [] { 50, 50, 50, 50, 50, 50, 50, 50 },
        new [] { 00, 00, 00, 00, 00, 00, 00, 00 },
    };
    static readonly int[][] RookPositions = 
    {
        new [] { 00, 00, 00, 00, 00, 00, 00, 00 },
        new [] { 05, 10, 10, 10, 10, 10, 10, 05 },
        new [] { -05, 00, 00, 00, 00, 00, 00, -05 },
        new [] { -05, 00, 00, 00, 00, 00, 00, -05 },
        new [] { -05, 00, 00, 00, 00, 00, 00, -05 },
        new [] { -05, 00, 00, 00, 00, 00, 00, -05 },
        new [] { -05, 00, 00, 00, 00, 00, 00, -05 },
        new [] { 00, 00, 00, 05, 05, 00, 00, 00 }
    };
    static readonly int[][] KnightPositions = 
    {
        new [] { -50, -40, -30, -30, -30, -30, -40, -50 },
        new [] { -40, -20, 00, 00, 00, 00, -20, -40 },
        new [] { -30, 00, 10, 15, 15, 10, 00, -30 },
        new [] { -30, 05, 15, 20, 20, 15, 05, -30 },
        new [] { -30, 00, 15, 20, 20, 15, 00, -30 },
        new [] { -30, 05, 10, 15, 15, 10, 05, -30 },
        new [] { -40, -20, 00, 05, 05, 00, -20, -40 },
        new [] { -50, -40, -30, -30, -30, -30, -40, -50 }
    };
    static readonly int[][] BishopPositions = 
    {
        new [] { -20, -10, -10, -10, -10, -10, -10, -20 },
        new [] { -10, 00, 00, 00, 00, 00, 00, -10 },
        new [] { -10, 00, 05, 10, 10, 05, 00, -10 },
        new [] { -10, 05, 05, 10, 10, 05, 05, -10 },
        new [] { -10, 00, 10, 10, 10, 10, 00, -10 },
        new [] { -10, 10, 10, 10, 10, 10, 10, -10 },
        new [] { -10, 05, 00, 00, 00, 00, 05, -10 },
        new [] { -20, -10, -10, -10, -10, -10, -10, -20 }
    };
    static readonly int[][] QueenPositions = 
    {
        new [] { -20, -10, -10, -05, -05, -10, -10, -20 },
        new [] { -10, 00, 00, 00, 00, 00, 00, -10 },
        new [] { -10, 00, 05, 05, 05, 05, 00, -10 },
        new [] { 00, 00, 05, 05, 05, 05, 00, -05 },
        new [] { -05, 00, 05, 05, 05, 05, 00, -05 },
        new [] { -10, 05, 05, 05, 05, 05, 00, -10 },
        new [] { -10, 00, 05, 00, 00, 00, 00, -10 },
        new [] { -20, -10, -10, -05, -05, -10, -10, -20 }
    };
    static readonly int[][] KingPositionsMiddleGame = 
    {
        new [] { 20, 30, 10, 00, 00, 10, 30, 20 },
        new [] { 20, 20, 00, 00, 00, 00, 20, 20 },
        new [] { -10, -20, -20, -20, -20, -20, -20, -10 },
        new [] { -20, -30, -30, -40, -40, -30, -30, -20 },
        new [] { -30, -40, -40, -50, -50, -40, -40, -30 },
        new [] { -30, -40, -40, -50, -50, -40, -40, -30 },
        new [] { -30, -40, -40, -50, -50, -40, -40, -30 },
        new [] { -30, -40, -40, -50, -50, -40, -40, -30 },
    };
    static readonly int[][] KingPositionsEndGame = 
    {
        new [] { -50, -30, -30, -30, -30, -30, -30, -50 },
        new [] { -30, -30, 00, 00, 00, 00, -30, -30 },
        new [] { -30, -10, 20, 30, 30, 20, -10, -30 },
        new [] { -30, -10, 30, 40, 40, 30, -10, -30 },
        new [] { -30, -10, 30, 40, 40, 30, -10, -30 },
        new [] { -30, -10, 20, 30, 30, 20, -10, -30 },
        new [] { -30, -20, -10, 00, 00, -10, -20, -30 },
        new [] { -50, -40, -30, -20, -20, -30, -40, -50 },
    };

    private int EvaluatePosition()
    {
        // Console.WriteLine($"Eval for player: {(board.IsWhiteToMove ? "White" : "Black")}");
        var score = EvaluatePositionInner(1);
        // if (score is not 10000 and not -10000)
        // {
        //     board.ForceSkipTurn();
        //     score -= EvaluatePositionInner(-1);
        //     board.UndoSkipTurn();
        // }
        return score;

        int EvaluatePositionInner(int color)
        {
            if (board.IsInCheckmate()) return -10000 * color;
            if (board.IsDraw()) return 10000 * color;

            var totalScore = 0;

            if (board.IsInCheck())
            {
                totalScore -= 80;
            }

            // Loop through all the pieces on the board and calculate their positional values
            foreach (var pieceList in board.GetAllPieceLists())
            foreach (var piece in pieceList)
            {
                //var pieceTypeIndex = (int)piece.PieceType;
                //var pieceSquareIndex = piece.Square.Index;
                //var piecePositionValue = 0;//positionValues[pieceTypeIndex][pieceSquareIndex];
                var piecePositionValue = GetPoints(piece.PieceType);

                var rank = color == 1 ? piece.Square.Rank : (7 - piece.Square.Rank);
                if (piece.PieceType == PieceType.Pawn)
                {
                    piecePositionValue += PawnPositions[rank][piece.Square.File];
                } 
                if (piece.PieceType == PieceType.Rook)
                {
                    piecePositionValue += RookPositions[rank][piece.Square.File];
                }
                if (piece.PieceType == PieceType.Knight)
                {
                    piecePositionValue += KnightPositions[rank][piece.Square.File];
                }
                if (piece.PieceType == PieceType.Bishop)
                {
                    piecePositionValue += BishopPositions[rank][piece.Square.File];
                }
                if (piece.PieceType == PieceType.Queen)
                {
                    piecePositionValue += QueenPositions[rank][piece.Square.File];
                }
                if (piece.PieceType == PieceType.King)
                {
                    piecePositionValue += KingPositionsMiddleGame[rank][piece.Square.File];
                }

                if (piece.IsWhite == board.IsWhiteToMove)
                {
                    if (!board.SquareIsAttackedByOpponent(piece.Square))
                        totalScore += piecePositionValue;
                }
                else
                    totalScore -= piecePositionValue;
            }

            return totalScore;
        }
    }
    
    enum NodeType { Exact, LowerBound, UpperBound }
    record struct TranspositionEntry(NodeType Type, int Score, int Depth, Move BestMove);
    static int GetPoints(PieceType pieceType) => Points[(int)pieceType];
    static readonly int[] Points = { 0, 50, 100, 150, 500, 900, 0 };
}
*/
public class EvilBot1 : MyBot1
{
    public EvilBot1() : base() { }
}

public class MyBot1 : IChessBot
{
    readonly int maxDepth = 6;
    readonly bool useAttacks = false;
    readonly bool useTranspositionTable = true;
    // readonly bool useNodes = false;
    readonly Random rng = new();
    Board board;
    readonly bool lowerAttacks;
    readonly bool moveKingLast;

    public MyBot1(bool lowerAttacks = true, bool moveKingLast = true) 
    {
        this.lowerAttacks = lowerAttacks;
        this.moveKingLast = moveKingLast;
    }

    public Move Think(Board board, Timer timer)
    {
        this.board = board;
        var pieceCount = BitboardHelper.GetNumberOfSetBits(board.IsWhiteToMove ? board.WhitePiecesBitboard : board.BlackPiecesBitboard);
        var isEndGame = pieceCount <= 6;
        var isMiddleGame = pieceCount <= 12;
        var runningOutOfTime = timer.MillisecondsRemaining < 5000;
        // var rootNode = useNodes ? new Node(Move.NullMove) : null;
        var (bestMove, _, _) = Minimax(board.IsWhiteToMove, maxDepth + (isEndGame ? (runningOutOfTime ? -2 : 1) : 0), int.MinValue, int.MaxValue/*, rootNode*/);
        return bestMove;
    }
    
    int CalcScore(Move move, bool isWhite, int currentAttacks, int currentPiecesScore, bool isMoveAttacked)
    {
        if (board.IsInCheckmate())
            return isWhite ? 10000 : -10000;
        if (board.IsRepeatedPosition())
            return isWhite ? -10000 : 10000;
        if (board.IsDraw())
            return 0;
        
        var score = currentPiecesScore;

        if (board.IsInCheck() && !isMoveAttacked)
        {
            AddScore(80);
        }

        if (move.IsCapture)
        {
            AddScore(GetPoints(move.CapturePieceType));
        }

        if (lowerAttacks && isMoveAttacked)
        {
            AddScore(-GetPoints(move.MovePieceType));
        }

        if (useAttacks)
        {
            AddScore(Attacks(isWhite));
        }

        return score;

        void AddScore(int addScore)
        {
            score += isWhite ? addScore : -addScore;
        }
    }

    int PiecesScore()
    {
        var score = 0;

        foreach (var pieceList in board.GetAllPieceLists())
        {
            var plusScore = Points[(int)pieceList.TypeOfPieceInList] * pieceList.Count;
            
            if (pieceList.IsWhitePieceList)
                score += plusScore;
            else
                score -= plusScore;
        }

        return score;
    }

    int Attacks(bool isWhite)
    {
        var score = 0;

        score -= CalcScore(!isWhite) * 2;
        board.ForceSkipTurn();
        score += CalcScore(isWhite);
        board.UndoSkipTurn();

        return score;

        int CalcScore(bool isWhite)
        {
            var score = 0;

            var lists = board.GetAllPieceLists();
            for (var i = 0; i < lists.Length; i++)
            {
                var pieceList = lists[i];
                if (pieceList.IsWhitePieceList == isWhite)
                    for (var index = 0; index < pieceList.Count; index++)
                    {
                        var piece = pieceList[index];
                        if (board.SquareIsAttackedByOpponent(piece.Square))
                            score += GetPoints(piece.PieceType) * (isWhite ? -1 : 1);
                    }
            }

            return score;
        }
    }

    State Minimax(bool isWhite, int depth, int bestWhiteScore, int bestBlackScore/*, Node? node*/)
    {
        ulong hashKey = 0;
        if (useTranspositionTable)
        {
            // Generate a hash key for the current board position
            hashKey = board.ZobristKey;

            // Check the transposition table for the current board position
            if (transpositionTable.TryGetValue(hashKey, out var ttEntry) && ttEntry.State.Depth >= depth)
            {
                // Use the stored information to perform a cutoff if possible
                if (ttEntry.Type == NodeType.Exact)
                    return ttEntry.State;
                if (ttEntry.Type == NodeType.LowerBound)
                    bestWhiteScore = Math.Max(bestWhiteScore, ttEntry.State.Score);
                else if (ttEntry.Type == NodeType.UpperBound)
                    bestBlackScore = Math.Min(bestBlackScore, ttEntry.State.Score);

                if (bestWhiteScore >= bestBlackScore)
                    return ttEntry.State;
            }
        }

        var attacks = 0;
        var piecesScore = PiecesScore();
        var bestSoFarMove = Move.NullMove;
        var bestScore = isWhite ? int.MinValue : int.MaxValue;
        var bestDepth = int.MinValue;
        var pieceCount = BitboardHelper.GetNumberOfSetBits(isWhite ? board.WhitePiecesBitboard : board.BlackPiecesBitboard);
        var isEndGame = pieceCount <= 6;
        var isMiddleGame = pieceCount <= 12;

        foreach (var move in board.GetLegalMoves()
                     .OrderByDescending(x => moveKingLast && board.SquareIsAttackedByOpponent(x.StartSquare))
                     .ThenByDescending(x => moveKingLast && !board.SquareIsAttackedByOpponent(x.TargetSquare))
                     .ThenByDescending(x => x.IsCapture)
                     .ThenByDescending(x => x.IsPromotion)
                     .ThenByDescending(x => x.IsCastles)
                     .ThenByDescending(x => isEndGame && x.MovePieceType == PieceType.Pawn)
                     .ThenBy(x => moveKingLast && !isEndGame && x.MovePieceType == PieceType.King) // move king as late as possible
                     .ThenBy(_ => rng.Next())
                )
        {
            // var newNode = useNodes ? new Node(move) : null;
            // if (useNodes) node.NextMoves.Add(newNode!);
            
            // Apply Late Move Reduction if the move is not a capture and the depth is greater than 2
            // var reducedDepth = depth - 1;
            // if (!move.IsCapture && depth > 2)
            //     reducedDepth = (depth + 1) / 2; // Reduce the depth by half (you can experiment with different reduction factors).

            var isMoveAttacked = board.SquareIsAttackedByOpponent(move.TargetSquare);
            board.MakeMove(move);
            var mm = (depth - 1 <= 0 || board.IsInCheckmate() || board.IsDraw())
                ? new(move, CalcScore(move, isWhite, attacks, piecesScore, isMoveAttacked), depth)
                : Minimax(!isWhite, depth - 1, bestWhiteScore, bestBlackScore/*, newNode*/);
            board.UndoMove(move);

            // if (useNodes)
            // {
            //     newNode.Score = mm.Score;
            //     newNode.Depth = mm.Depth;
            // }

            if ((isWhite && mm.Score > bestScore) || (!isWhite && mm.Score < bestScore)
                || (mm.Score == bestScore && bestDepth < mm.Depth))
            {
                bestDepth = mm.Depth;
                (bestSoFarMove, bestScore) = (move, mm.Score);

                if (isWhite)
                    bestWhiteScore = Math.Max(bestWhiteScore, mm.Score);
                else
                    bestBlackScore = Math.Min(bestBlackScore, mm.Score);

                if (bestBlackScore <= bestWhiteScore)
                    break;
            }
        }

        var state = new State(bestSoFarMove, bestScore, bestDepth);

        if (useTranspositionTable)
        {
            // Determine the node type based on the extremeScore
            var nodeType = NodeType.Exact;
            if (bestScore <= bestWhiteScore)
                nodeType = NodeType.UpperBound;
            else if (bestScore >= bestBlackScore)
                nodeType = NodeType.LowerBound;

            // Store the current board position in the transposition table
            transpositionTable[hashKey] = new TranspositionEntry(nodeType, state);
        }

        return state;
    }

    // record Node(Move Move)
    // {
    //     public int Score { get; set; }
    //     public int Depth { get; set; }
    //     public List<Node> NextMoves { get; } = new();
    // }

    record struct State(Move Move, int Score, int Depth);

    static int GetPoints(PieceType pieceType) => Points[(int)pieceType];
    static readonly int[] Points = { 0, 1, 10, 15, 50, 90, 100 };

    record struct TranspositionEntry(NodeType Type, State State);
    Dictionary<ulong, TranspositionEntry> transpositionTable = new();

    enum NodeType
    {
        Exact,
        UpperBound,
        LowerBound
    }
}