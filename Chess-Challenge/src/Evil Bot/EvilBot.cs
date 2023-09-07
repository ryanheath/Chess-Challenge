using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessChallenge.Example
{
    // A simple bot that can spot mate in one, and always captures the most valuable piece it can.
    // Plays randomly otherwise.
    public class EvilBotOrg : IChessBot
    {
        // Piece values: null, pawn, knight, bishop, rook, queen, king
        int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };

        public Move Think(Board board, Timer timer)
        {
            Move[] allMoves = board.GetLegalMoves();

            // Pick a random move to play if nothing better is found
            Random rng = new();
            Move moveToPlay = allMoves[rng.Next(allMoves.Length)];
            int highestValueCapture = 0;

            foreach (Move move in allMoves)
            {
                // Always play checkmate in one
                if (MoveIsCheckmate(board, move))
                {
                    moveToPlay = move;
                    break;
                }

                // Find highest value capture
                Piece capturedPiece = board.GetPiece(move.TargetSquare);
                int capturedPieceValue = pieceValues[(int)capturedPiece.PieceType];

                if (capturedPieceValue > highestValueCapture)
                {
                    moveToPlay = move;
                    highestValueCapture = capturedPieceValue;
                }
            }

            return moveToPlay;
        }

        // Test if this move gives checkmate
        bool MoveIsCheckmate(Board board, Move move)
        {
            board.MakeMove(move);
            bool isMate = board.IsInCheckmate();
            board.UndoMove(move);
            return isMate;
        }
    }

    public class EvilBot1 : IChessBot
    {
        // Piece values: null, pawn, knight, bishop, rook, queen, king
        int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };
        Random rng = new();

        public Move Think(Board board, Timer timer)
        {
            int bestScore = -int.MaxValue;

            Move[] moves = GetMoves(board);
            Move bestMove = moves[0];

            foreach (Move move in moves)
            {
                board.MakeMove(move);
                int score = -AlphaBeta(board, -int.MaxValue, int.MaxValue, 0);
                board.UndoMove(move);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }
            }
            return bestMove;
        }

        Move[] GetMoves(Board board)
        {
            return board.GetLegalMoves().OrderBy(x => rng.Next()).ToArray();
        }

        int Quiesce(Board board, int alpha, int beta)
        {
            int stand_pat = Evaluate(board);
            if( stand_pat >= beta )
                return beta;
            if( alpha < stand_pat )
                alpha = stand_pat;

            foreach (Move move in board.GetLegalMoves(true))
            {
                board.MakeMove(move);
                int score = -Quiesce( board, -beta, -alpha );
                board.UndoMove(move);

                if( score >= beta )
                    return beta;
                if( score > alpha )
                alpha = score;
            }
            return alpha;
        }

        int Evaluate(Board board)
        {
            if (board.IsInCheckmate()) return -10000;
            if (board.IsDraw()) return 0;
            int score = 0;
            PieceList[] pieceLists = board.GetAllPieceLists();
            for (int i = 0; i < 5; i++)
            {
                int val = pieceValues[i + 1];
                score += (pieceLists[i].Count - pieceLists[i + 6].Count) * val;
            }
            return score * (board.IsWhiteToMove ? 1 : -1);
        }

        int AlphaBeta(Board board, int alpha, int beta, int depthLeft)
        {
            if (depthLeft == 0) return Quiesce(board, alpha, beta);
            foreach (Move move in board.GetLegalMoves())
            {
                board.MakeMove(move);
                int score = -AlphaBeta(board, -beta, -alpha, depthLeft - 1);
                board.UndoMove(move);
                if (score >= beta)
                    return beta;
                if (score > alpha)
                    alpha = score;
            }
            return alpha;
        }

        // int AlphaBetaMax(Board board, int alpha, int beta, int depth_left)
        // {
        //     if (depth_left == 0) return Evaluate(board);
        //     Move[] moves = board.GetLegalMoves();
        //     foreach (Move move in moves)
        //     {
        //         board.MakeMove(move);
        //         int score = AlphaBetaMin(board, alpha, beta, depth_left - 1);
        //         board.UndoMove(move);
        //         if (score >= beta) return beta;
        //         if (score > alpha) alpha = score;
        //     }
        //     return alpha;
        // }

        // int AlphaBetaMin(Board board, int alpha, int beta, int depth_left)
        // {
        //     if (depth_left == 0) return -Evaluate(board);
        //     Move[] moves = board.GetLegalMoves();
        //     foreach (Move move in moves)
        //     {
        //         board.MakeMove(move);
        //         int score = AlphaBetaMax(board, alpha, beta, depth_left - 1);
        //         board.UndoMove(move);
        //         if (score <= alpha) return alpha;
        //         if (score < beta) beta = score;
        //     }
        //     return beta;
        // }
    }
    
    public class EvilBot2 : IChessBot
    {
        public Move Think(Board board, Timer timer)
        {
            StartSearch(board, timer);
            return bestMovesByDepth[0];
        }


        const int immediateMateScore = 100000;
        const int positiveInfinity = 9999999;
        const int negativeInfinity = -positiveInfinity;
        const int maxSearchDepth = int.MaxValue;
        const int maxMillisecondsPerSearch = 1500;

        List<Move> bestMovesByDepth;
        int bestEval;

        bool isSearchCancelled;


        void StartSearch(Board board, Timer timer)
        {
            bestMovesByDepth = new List<Move>();
            bestEval = 0;
            isSearchCancelled = false;

            for (int searchDepth = 1; searchDepth < int.MaxValue; searchDepth++)
            {
                bestMovesByDepth.Add(Move.NullMove);
                Search(board, timer, searchDepth, 0, negativeInfinity, positiveInfinity);

                if (isSearchCancelled || IsMateScore(bestEval)) break;
            }
        }

        int Search(Board board, Timer timer, int plyRemaining, int plyFromRoot, int alpha, int beta)
        {
            if (timer.MillisecondsElapsedThisTurn > maxMillisecondsPerSearch) // Cancel the search if we are out of time
            {
                isSearchCancelled = true;
                return 0;
            }

            if (board.IsInCheckmate()) return -immediateMateScore + plyFromRoot; // Check for Checkmate before we do anything else.


            // Once we reach target depth, search all captures to make the evaluation more accurate
            if (plyRemaining == 0) return QuiescenceSearch(board, alpha, beta);

            Move[] unorderedMoves = board.GetLegalMoves();
            if (unorderedMoves.Length == 0) return 0; // Stalemate

            // Order the moves, making sure to put the best move from the previous iteration first
            Move[] orderedMoves = Order(unorderedMoves, board, bestMovesByDepth[plyFromRoot]);

            foreach (Move move in orderedMoves)
            {
                board.MakeMove(move);
                int eval = -Search(board, timer, plyRemaining - 1, plyFromRoot + 1, -beta, -alpha);
                board.UndoMove(move);

                //Console.WriteLine("-------");
                //Console.WriteLine("Board FEN String: " + board.GetFenString());
                //Console.WriteLine("Depth: " + plyFromRoot);
                //Console.WriteLine("Eval: " + eval + ", Alpha: " + alpha + ", Beta: " + beta);

                if (eval >= beta) return beta;
                if (eval > alpha)
                {
                    alpha = eval;
                    bestMovesByDepth[plyFromRoot] = move;
                }
            }

            return alpha;
        }

        int QuiescenceSearch(Board board, int alpha, int beta)
        {
            int eval = Evaluate(board);
            if (eval >= beta) return beta;
            alpha = Math.Max(alpha, eval);

            // Order the moves
            Move[] orderedMoves = Order(board.GetLegalMoves(true), board, Move.NullMove);

            foreach (Move move in orderedMoves)
            {
                board.MakeMove(move);
                eval = -QuiescenceSearch(board, -beta, -alpha);
                board.UndoMove(move);

                if (eval >= beta) return beta;
                alpha = Math.Max(alpha, eval);
            }

            return alpha;
        }

        Move[] Order(Move[] moves, Board board, Move putThisFirst)
        {
            if (moves.Length == 0) return new Move[0];
            Move[] returnThis = new Move[moves.Length];

            Dictionary<Move, int> orderedMoves = new Dictionary<Move, int>();
            foreach (Move move in moves)
            {
                if (move.IsNull) continue;

                int moveScoreGuess = 0;
                if (move.IsCapture) moveScoreGuess += 10 * GetPointValue(move.CapturePieceType) - GetPointValue(move.MovePieceType);
                if (move.IsPromotion) moveScoreGuess += GetPointValue(move.PromotionPieceType);
                if (board.SquareIsAttackedByOpponent(move.TargetSquare)) moveScoreGuess -= GetPointValue(move.MovePieceType);
                if (move == putThisFirst) moveScoreGuess += 100000;
                orderedMoves.Add(move, moveScoreGuess);
            }
            int counter = 0;
            foreach (var k in orderedMoves.OrderByDescending(x => x.Value))
            {
                returnThis[counter] = k.Key;
                counter++;
            }

            return returnThis;

        }

        int[] POINT_VALUES = { 100, 350, 350, 525, 1000 };
        int GetPointValue(PieceType type)
        {
            switch (type)
            {
                case PieceType.None: return 0;
                case PieceType.King: return positiveInfinity;
                default: return POINT_VALUES[(int)type - 1];
            }
        }

        bool IsMateScore(int score)
        {
            return Math.Abs(score) > immediateMateScore - 1000;
        }

        #region Evalution

        const float endgameMaterialStart = 1750;

        int[] kingMidgameTable = new int[]
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

        //Represent the rank scores as a 64-bit int. NEED TO FINISH
        ulong[] kingMidgameTable_v2 = new ulong[]
        {
            0b0001010000011110000010100000000000000000000010100001111000010100L,
            0b0001010000010100000000000000000000000000000000000001010000010100L,
        };
        int[] kingEndgameTable = new int[]
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

        // Performs static evaluation of the current position.
        // The position is assumed to be 'quiet', i.e no captures are available that could drastically affect the evaluation.
        // The score that's returned is given from the perspective of whoever's turn it is to move.
        // So a positive score means the player who's turn it is to move has an advantage, while a negative score indicates a disadvantage.
        public int Evaluate(Board board)
        {
            int whiteEval = 0;
            int blackEval = 0;

            int whiteMaterial = CountMaterial(board, true);
            int blackMaterial = CountMaterial(board, false);

            int whiteMaterialWithoutPawns = whiteMaterial - board.GetPieceList(PieceType.Pawn, true).Count * POINT_VALUES[0];
            int blackMaterialWithoutPawns = blackMaterial - board.GetPieceList(PieceType.Pawn, false).Count * POINT_VALUES[0];
            float whiteEndgamePhaseWeight = EndgamePhaseWeight(whiteMaterialWithoutPawns);
            float blackEndgamePhaseWeight = EndgamePhaseWeight(blackMaterialWithoutPawns);

            // Material
            whiteEval += whiteMaterial;
            blackEval += blackMaterial;

            // King Safety
            int whiteKingRelativeIndex = board.GetKingSquare(true).Index;
            int blackKingRelativeIndex = new Square(board.GetKingSquare(false).File, 7 - board.GetKingSquare(false).Rank).Index;
            whiteEval += (int)Lerp(kingMidgameTable[whiteKingRelativeIndex], kingEndgameTable[whiteKingRelativeIndex], whiteEndgamePhaseWeight);
            blackEval += (int)Lerp(kingMidgameTable[blackKingRelativeIndex], kingEndgameTable[blackKingRelativeIndex], blackEndgamePhaseWeight);

            // Endgame Bonuses
            whiteEval += GetEndgameBonus(board, blackEndgamePhaseWeight, true);
            blackEval += GetEndgameBonus(board, whiteEndgamePhaseWeight, false);


            return (whiteEval - blackEval) * ((board.IsWhiteToMove) ? 1 : -1);
        }

        float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        float EndgamePhaseWeight(int materialCountWithoutPawns)
        {
            return 1 - Math.Min(1, materialCountWithoutPawns / endgameMaterialStart);
        }


        int CountMaterial(Board board, bool isWhite)
        {
            return board.GetPieceList(PieceType.Pawn, isWhite).Count * POINT_VALUES[0]
                + board.GetPieceList(PieceType.Knight, isWhite).Count * POINT_VALUES[1]
                + board.GetPieceList(PieceType.Bishop, isWhite).Count * POINT_VALUES[2]
                + board.GetPieceList(PieceType.Rook, isWhite).Count * POINT_VALUES[3]
                + board.GetPieceList(PieceType.Queen, isWhite).Count * POINT_VALUES[4];
        }

        int GetEndgameBonus(Board board, float enemyEndgameWeight, bool isWhite)
        {
            if (enemyEndgameWeight <= 0) return 0;
            ulong ourBB = (isWhite) ? board.WhitePiecesBitboard : board.BlackPiecesBitboard;
            Square enemyKingSquare = board.GetKingSquare(!isWhite);

            int endgameBonus = 0;
            while (ourBB != 0) {
                Square pieceSquare = new Square(BitboardHelper.ClearAndGetIndexOfLSB(ref ourBB));
                switch (board.GetPiece(pieceSquare).PieceType)
                {
                    case PieceType.Pawn:
                        // Encourage pawns to move forward
                        endgameBonus += 50 - 10 * ((isWhite) ? 7 - pieceSquare.Rank : pieceSquare.Rank);
                        break;
                    case PieceType.Rook:
                        //Encourage rooks to get close to the same rank/file as the king
                        endgameBonus += 50 - 10 * Math.Min(Math.Abs(enemyKingSquare.File - pieceSquare.File), Math.Abs(enemyKingSquare.Rank - pieceSquare.Rank));
                        break;
                    default:
                        // In general, we want to get our pieces closer to the enemy king, will give us a better chance of finding a checkmate.
                        // Use power growth so we prioritive
                        endgameBonus += 50 - (int)(10 * Math.Pow(Math.Max(Math.Abs(enemyKingSquare.File - pieceSquare.File), Math.Abs(enemyKingSquare.Rank - pieceSquare.Rank)), 1.5));
                        break;
                }
            }

            return (int)(endgameBonus * enemyEndgameWeight);
        }

        #endregion
    }
    
    public class EvilBot3 : IChessBot
    {
        public Move Think(Board board, Timer timer)
        {
            StartSearch(board, timer);
            return bestMovesByDepth[0];
        }


        const int immediateMateScore = 100000;
        const int positiveInfinity = 9999999;
        const int negativeInfinity = -positiveInfinity;
        const int maxSearchDepth = int.MaxValue;
        const int maxMillisecondsPerSearch = 1500;

        List<Move> bestMovesByDepth;
        int bestEval;

        bool isSearchCancelled;


        void StartSearch(Board board, Timer timer)
        {
            bestMovesByDepth = new List<Move>();
            bestEval = 0;
            isSearchCancelled = false;

            for (int searchDepth = 1; !isSearchCancelled; searchDepth++)
            {
                bestMovesByDepth.Add(Move.NullMove);
                Search(board, timer, searchDepth, 0, negativeInfinity, positiveInfinity);

                if (Math.Abs(bestEval) > immediateMateScore - 1000) break;
            }
        }

        int Search(Board board, Timer timer, int plyRemaining, int plyFromRoot, int alpha, int beta)
        {
            if (timer.MillisecondsElapsedThisTurn > maxMillisecondsPerSearch) // Cancel the search if we are out of time
            {
                isSearchCancelled = true;
                return 0;
            }

            if (board.IsInCheckmate()) return -immediateMateScore + plyFromRoot; // Check for Checkmate before we do anything else.


            // Once we reach target depth, search all captures to make the evaluation more accurate
            if (plyRemaining == 0) return QuiescenceSearch(board, alpha, beta);

            Move[] unorderedMoves = board.GetLegalMoves();
            if (unorderedMoves.Length == 0) return 0; // Stalemate

            // Order the moves, making sure to put the best move from the previous iteration first
            Move[] orderedMoves = Order(unorderedMoves, board, bestMovesByDepth[plyFromRoot]);

            foreach (Move move in orderedMoves)
            {
                board.MakeMove(move);
                int eval = -Search(board, timer, plyRemaining - 1, plyFromRoot + 1, -beta, -alpha);
                board.UndoMove(move);

                if (eval >= beta) return beta;
                if (eval > alpha)
                {
                    alpha = eval;
                    bestMovesByDepth[plyFromRoot] = move;
                    bestEval = plyFromRoot == 0 ? eval : bestEval;
                }
            }

            return alpha;
        }

        int QuiescenceSearch(Board board, int alpha, int beta)
        {
            int eval = Evaluate(board);
            if (eval >= beta) return beta;
            alpha = Math.Max(alpha, eval);

            // Order the moves
            Move[] orderedMoves = Order(board.GetLegalMoves(true), board, Move.NullMove);

            foreach (Move move in orderedMoves)
            {
                board.MakeMove(move);
                eval = -QuiescenceSearch(board, -beta, -alpha);
                board.UndoMove(move);

                if (eval >= beta) return beta;
                alpha = Math.Max(alpha, eval);
            }

            return alpha;
        }

        Move[] Order(Move[] moves, Board board, Move putThisFirst)
        {
            if (moves.Length == 0) return new Move[0];
            Move[] returnThis = new Move[moves.Length];

            Dictionary<Move, int> orderedMoves = new Dictionary<Move, int>();
            foreach (Move move in moves)
            {
                if (move.IsNull) continue;

                int moveScoreGuess = 0;
                if (move.IsCapture) moveScoreGuess += 10 * GetPointValue(move.CapturePieceType) - GetPointValue(move.MovePieceType);
                if (move.IsPromotion) moveScoreGuess += GetPointValue(move.PromotionPieceType);
                if (board.SquareIsAttackedByOpponent(move.TargetSquare)) moveScoreGuess -= GetPointValue(move.MovePieceType);
                if (move == putThisFirst) moveScoreGuess += 100000;
                orderedMoves.Add(move, moveScoreGuess);
            }
            int counter = 0;
            foreach (var k in orderedMoves.OrderByDescending(x => x.Value))
            {
                returnThis[counter] = k.Key;
                counter++;
            }

            return returnThis;

        }


        #region Evalution

        //Represent the rank scores as a 64-bit int. Last couple rows are all copies
        ulong[] kingMidgameTable = new ulong[]
        {
            0b_00010100_00011110_00001010_00000000_00000000_00001010_00011110_00010100L,
            0b_00010100_00010100_00000000_00000000_00000000_00000000_00010100_00010100L,
            0b_11110110_11101100_11101100_11101100_11101100_11101100_11101100_11110110L,
            0b_11101100_11100010_11100010_11011000_11011000_11100010_11100010_11101100L,
            0b_11100010_11011000_11011000_11001110_11001110_11011000_11011000_11100010L
        };

        ulong[] kingEndgameTable = new ulong[]
        {
            0b_11100010_11110110_00011110_00101000_00101000_00011110_11110110_11100010L,
            0b_11100010_11110110_00010100_00011110_00011110_00010100_11110110_11100010L,
            0b_11100010_11100010_00000000_00000000_00000000_00000000_11100010_11100010L,
            0b_11001110_11100010_11100010_11100010_11100010_11100010_11100010_11001110L,
        };

        // Performs static evaluation of the current position.
        // The position is assumed to be 'quiet', i.e no captures are available that could drastically affect the evaluation.
        // The score that's returned is given from the perspective of whoever's turn it is to move.
        // So a positive score means the player who's turn it is to move has an advantage, while a negative score indicates a disadvantage.
        public int Evaluate(Board board)
        {
            Square whiteKingSquare = board.GetKingSquare(true);
            Square blackKingSquare = board.GetKingSquare(false);

            //Mobility
            int mobility = GetMobilityBonus(board);
            if (board.TrySkipTurn())
            {
                mobility -= GetMobilityBonus(board);
                board.UndoSkipTurn();
            }
            else mobility = 0; // ignore mobility if we can't get it for both sides


            return (CountMaterial(board, true) - CountMaterial(board, false)
                + GetKingSafetyScores(whiteKingSquare.File, whiteKingSquare.Rank, EndgamePhaseWeight(board, true))
                - GetKingSafetyScores(blackKingSquare.File, 7 - blackKingSquare.Rank, EndgamePhaseWeight(board, false))
                + GetEndgameBonus(board, true)
                - GetEndgameBonus(board, false)) 
                * (board.IsWhiteToMove ? 1 : -1) 
                + mobility;
        }


        int[] POINT_VALUES = { 100, 350, 350, 525, 1000 };
        int GetPointValue(PieceType type)
        {
            switch (type)
            {
                case PieceType.None: return 0;
                case PieceType.King: return positiveInfinity;
                default: return POINT_VALUES[(int)type - 1];
            }
        }

        float EndgamePhaseWeight(Board board, bool isWhite)
        {
            return 1 - Math.Min(1, (CountMaterial(board, isWhite) - board.GetPieceList(PieceType.Pawn, isWhite).Count * 100) / 1750);
        }

        int GetMobilityBonus(Board board)
        {
            int mobility = 0;
            foreach (Move move in board.GetLegalMoves())
            {
                switch (move.MovePieceType)
                {
                    case PieceType.Knight:
                        mobility += 100; // More points for knight since it has a smaller maximum of possible moves
                        break;
                    case PieceType.Bishop:
                        mobility += 5;
                        break;
                    case PieceType.Rook:
                        mobility += 6;
                        break;
                    case PieceType.Queen:
                        mobility += 4;
                        break;
                }
            }
            return mobility;
        }

        int GetKingSafetyScores(int file, int relativeRank, float endgameWeight)
        {
            sbyte midgameScore = (sbyte)((kingMidgameTable[Math.Min(relativeRank, 4)] >> file * 8) % 256);
            return (int)(midgameScore + (  midgameScore - (sbyte)( (kingEndgameTable[(int)Math.Abs(3.5 - relativeRank)] >> file * 8) % 256 )  ) * endgameWeight);
        }


        int CountMaterial(Board board, bool isWhite)
        {
            return board.GetPieceList(PieceType.Pawn, isWhite).Count * 100
                + board.GetPieceList(PieceType.Knight, isWhite).Count * 350
                + board.GetPieceList(PieceType.Bishop, isWhite).Count * 350
                + board.GetPieceList(PieceType.Rook, isWhite).Count * 525
                + board.GetPieceList(PieceType.Queen, isWhite).Count * 1000;
        }

        int GetEndgameBonus(Board board, bool isWhite)
        {
            float enemyEndgameWeight = EndgamePhaseWeight(board, !isWhite);
            if (enemyEndgameWeight <= 0) return 0;
            ulong ourBB = isWhite ? board.WhitePiecesBitboard : board.BlackPiecesBitboard;
            Square enemyKingSquare = board.GetKingSquare(!isWhite);

            int endgameBonus = 0;
            while (ourBB != 0) {
                Square pieceSquare = new Square(BitboardHelper.ClearAndGetIndexOfLSB(ref ourBB));
                switch (board.GetPiece(pieceSquare).PieceType)
                {
                    case PieceType.Pawn:
                        // Encourage pawns to move forward
                        endgameBonus += 50 - 10 * (isWhite ? 7 - pieceSquare.Rank : pieceSquare.Rank);
                        break;
                    case PieceType.Rook:
                        //Encourage rooks to get close to the same rank/file as the king
                        endgameBonus += 50 - 10 * Math.Min(Math.Abs(enemyKingSquare.File - pieceSquare.File), Math.Abs(enemyKingSquare.Rank - pieceSquare.Rank));
                        break;
                    default:
                        // In general, we want to get our pieces closer to the enemy king, will give us a better chance of finding a checkmate.
                        // Use power growth so we prioritive
                        endgameBonus += 50 - (int)(10 * Math.Pow(Math.Max(Math.Abs(enemyKingSquare.File - pieceSquare.File), Math.Abs(enemyKingSquare.Rank - pieceSquare.Rank)), 1.5));
                        break;
                }
            }

            return (int)(endgameBonus * enemyEndgameWeight);
        }

        #endregion
    }
    
    public class EvilBot4 : IChessBot
    {
        private int numEvals; // #DEBUG
        private readonly int[] pieceValues = { 0, 82, 337, 365, 477, 1025, 20000 };
        private readonly double[] phaseTransitions = { 0, 0, 1, 1, 2, 4, 0 };

        private int searchDepth;
        private Move bestMove;
        private int bigNumber = 500000;

        private ulong[] pstMidgame =
        {
        8608480569177386391, 8685660850885265542, 7455859293954733975, 7450775221175809911,
        244781470246865543, 6528739145396427400, 8613284402485037191, 7311444969172399718,
        7441747066522933893, 8762203425850628487, 8685359588994222216, 8685060590264547174,
        11068062936632834697, 8685624712745224566, 7383519125143254902, 6298135045248419942,
        7464902858680793738, 8613304275280820343, 8536422973693196167, 7460081354432607845,
        5226240968522954598, 8680521733502297718, 6297815023207405174, 8679658625515751048,
    };

        private ulong[] pstEndgame =
        {
        8608480570020589039, 13599369747493255048, 9833459666392676215, 9838262333166024567,
        6230279642916812389, 7388286466382137478, 7460362824862697318, 6230578787355420244,
        8536422969399277431, 8608481667260647543, 8613284334033995639, 8536422969128744823,
        9838263501683455879, 9837963266004318071, 8612984167358494583, 8608480567731124086,
        8685342001104267415, 7532720663170754985, 7532720731856603271, 7378996696646514277,
        5072874484798097800, 9838264673941096839, 7460362897878190215, 7455858129732331365,
    };

        public Move Think(Board board, Timer timer)
        {
            for (int depth = 1; depth <= 4; depth++)
            {
                numEvals = 0; // #DEBUG

                if (timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining / 30) break;

                searchDepth = depth;
                Negamax(board, -bigNumber, bigNumber, depth);

                // Console.WriteLine($"{numEvals} evals at {depth} depth"); // #DEBUG
            }

            return bestMove;
        }

        private int Negamax(Board board, int alpha, int beta, int depth)
        {
            if (depth <= 0) return Evaluate(board);

            // TODO transposition tables
            // TODO better move ordering
            Move[] moves = board
                .GetLegalMoves()
                .OrderByDescending(x => pieceValues[(int)x.CapturePieceType] - pieceValues[(int)x.MovePieceType]).ToArray();

            int bestEval = int.MinValue;
            foreach (Move aMove in moves)
            {
                board.MakeMove(aMove);
                int moveEval = -Negamax(board, -beta, -alpha, depth - 1);
                board.UndoMove(aMove);

                if (moveEval > bestEval)
                {
                    bestEval = moveEval;
                    if (depth == searchDepth) bestMove = aMove;

                    // TODO alpha/beta pruning
                    if (bestEval >= beta) break;
                    alpha = Math.Max(bestEval, alpha);
                }
            }

            return bestEval;
        }

        private int Evaluate(Board board)
        {
            numEvals++; // #DEBUG

            double phase = 0;
            int midgameEval = 0;
            int endgameEval = 0;

            // TODO: Optimise with bitmaps
            foreach (PieceList pieceList in board.GetAllPieceLists())
            {
                int listMidgameEval = pieceList.Sum((aPiece) => GetValueOfPiece(aPiece, pstMidgame));
                int listEndgameEval = pieceList.Sum((aPiece) => GetValueOfPiece(aPiece, pstEndgame));
                phase += phaseTransitions[(int)pieceList.TypeOfPieceInList] * pieceList.Count;

                int mul = (pieceList.IsWhitePieceList ? 1 : -1);
                midgameEval += listMidgameEval * mul;
                endgameEval += listEndgameEval * mul;
            }

            double midgame = phase / 24.0;
            return midgameEval * (board.IsWhiteToMove ? 1 : -1);
        }

        // TODO: Optimise this func with better bit operations
        private int GetValueOfPiece(Piece piece, ulong[] pstList)
        {
            int rank = piece.IsWhite ? piece.Square.Rank : (7 - piece.Square.Rank);

            int pieceIdx = rank * 8 + piece.Square.File;
            int pstIdx = (pieceIdx / 16) + ((int)piece.PieceType - 1) * 4;
            ulong pst = pstList[pstIdx];
            int bitmapOffset = 60 - (pieceIdx % 16) * 4;
            return pieceValues[(int)piece.PieceType] + (int)((pst >> bitmapOffset) & 15) * 23 - 167;
        }
    }

    public class EvilBot5 : IChessBot
    {
        //From https://www.chessprogramming.org/Simplified_Evaluation_Function#Piece-Square_Tables
        ulong[,] packedTables = {
                { 0x0000888822461121, 0x000419A0122B0000 }, //pawn
                { 0xEDCCDB00C043C132, 0xC032C143DB01EDCC }, //knight
                { 0xBAAAA000A012A112, 0xA042A222A100BAAA }, //bishop
                { 0x0000122290009000, 0x9000900090000001 }, //rook
                { 0xBAA9A000A0119011, 0x0011A111A010BAA9 }, //queen
                { 0xCDDECDDECDDECDDE, 0xBCCDABBB4400EEEE }, //king
            };
        int[] pieceValues = { 0, 100, 300, 320, 500, 900, 0 };

        int getPieceValue(PieceType piece) =>
                pieceValues[(int)piece];

        int extractBonusSquare(int table, int index)
        {
            if (table == 5 && index / 8 == 7)
            {
                if (index % 8 == 2 || index % 8 == 6)
                    return 30;
                else if (index % 8 == 4)
                    return 0;
            }
            int[] bonusValues = { 0, 5, 10, 15, 20, 25, 30, 40, 50, -5, -10, -20, -30, -40, -50 };
            int offset = index / 8 * 4 + ((index & 7) ^ ((index & 7) > 3 ? 0b111 : 0));
            return bonusValues[packedTables[table, index / 32] << (offset % 16 * 4) >> 60];
        }

        ulong fileMask = 0x0101010101010101;
        ulong getPawnMask(Square square, bool white) => fileMask << square.File + square.Rank * 8 * (white ? 1 : -1);
        int calculateDistanceFromSquare(Square square1, Square square2) => Math.Abs(square1.Rank - square2.Rank) + Math.Abs(square1.File - square2.File);
        public int EvaluateOneSide(Board board, bool white)
        {
            ulong bitboard = white ? board.WhitePiecesBitboard : board.BlackPiecesBitboard;

            Square kingSqaure = board.GetKingSquare(white);
            int mopUpScore = Math.Min(
                Math.Min(calculateDistanceFromSquare(kingSqaure, new Square(0))
                , calculateDistanceFromSquare(kingSqaure, new Square(7))),
                Math.Min(calculateDistanceFromSquare(kingSqaure, new Square(56))
                , calculateDistanceFromSquare(kingSqaure, new Square(63)))) - 6;

            //ulong pieceBitboard = board.WhitePiecesBitboard | board.BlackPiecesBitboard;

            float endgameWeight =
                1 - (Math.Clamp(BitboardHelper.GetNumberOfSetBits(board.WhitePiecesBitboard | board.BlackPiecesBitboard), 8, 24) - 8) / 4;
            float evaluation = 0;

            Square enemyKingSquare = board.GetKingSquare(!white);
            for (int i = 0; i < 6; i++)
            {
                foreach (Piece currentPiece in board.GetAllPieceLists()[i + (white ? 0 : 6)])
                {
                    Square currentSqaure = currentPiece.Square;
                    int[] endgamePawnBonusValues = { 0, -2, -2, 1, 2, 5, 8, 0 };
                    int squareIndex = currentPiece.Square.Index ^ (white ? 0b111000 : 0);
                    evaluation += getPieceValue(currentPiece.PieceType)
                        + (currentPiece.IsPawn || currentPiece.IsKing ? (1 - endgameWeight) : 1)
                        * pieceSquareValues[(int)currentPiece.PieceType - 1, squareIndex]
                        + (currentPiece.IsPawn && endgameWeight != 0 ? BitboardHelper.GetNumberOfSetBits(getPawnMask(currentSqaure, white) & BitboardHelper.GetKingAttacks(kingSqaure)) 
                        + endgamePawnBonusValues[7 - squareIndex / 8] : 0) * endgameWeight;


                }
            }

            evaluation += mopUpScore * endgameWeight * 10
                + (board.GetPieceList(PieceType.Bishop, white).Count >= 2 ? 50 : 0)
                + (bitboard & (fileMask << kingSqaure.File)) == 0 ? 50 * (1 - endgameWeight) : 0
                + calculateDistanceFromSquare(kingSqaure, enemyKingSquare) * 25 * endgameWeight;
            return (int)evaluation;

        }

        //List<Move> killerMoves = new List<Move>();
        //int scoreMove(Move move) => (move.IsCapture ? getPieceValue(move.CapturePieceType) - getPieceValue(move.MovePieceType) : 0)/* + (!move.IsCapture && killerMoves.Contains(move) ? 50 : 0)/* + (move.IsCastles ? 10 : 0)*/;
        Move bestMove;

        public struct Transposition
        {
            public ulong zobristHash = 0;
            public int evaluation, depth;
            public NodeType flag = NodeType.EXACT;

            public enum NodeType { EXACT = 0, LOWERBOUND = -1, UPPERBOUND = 1 }
            
            public Transposition(ulong zHash, int eval, int d, NodeType f)
            {
                zobristHash = zHash;
                evaluation = eval;
                depth = d;
                flag = f;
            }
        };

        //ulong TTMask = 0x7FFFFFF;
        Dictionary<ulong,Transposition> transpositionTable = new();
        //This code was from "Algorithms Explained – minimax and alpha-beta pruning" by Sebastian Lague
        //https://en.wikipedia.org/wiki/Negamax
        int[,] pieceSquareValues = new int[6, 64];
        public int Search(Board board, Timer timer, int depth, int plyFromRoot, int alpha, int beta)
        {
            int oldAlpha = alpha;
            Move[] moves = board.GetLegalMoves();
            moves = moves.OrderByDescending(move => move.IsCapture ? getPieceValue(move.CapturePieceType) - getPieceValue(move.MovePieceType) : 0).ToArray();

            if (plyFromRoot == 0)
                bestMove = moves[0];
            
            if (board.IsInCheckmate())
                return -5_000_000 + plyFromRoot;
            else if (board.IsDraw())
                return 0;
            
            if (transpositionTable.TryGetValue(board.ZobristKey, out var TTEntry) && TTEntry.depth >= depth)
            {
                switch (TTEntry.flag)
                {
                    case Transposition.NodeType.EXACT:
                        return TTEntry.evaluation;
                    case Transposition.NodeType.LOWERBOUND:
                        alpha = Math.Max(alpha, TTEntry.evaluation);
                        break;
                    case Transposition.NodeType.UPPERBOUND:
                        beta = Math.Min(beta, TTEntry.evaluation);
                        break;
                }
                if (beta <= alpha)
                    return TTEntry.evaluation;
            }
            if (depth == 0)
                return (EvaluateOneSide(board, true) - EvaluateOneSide(board, false)) * (board.IsWhiteToMove ? 1 : -1);
            int bestEval = -5_000_000;
            foreach (Move currentMove in moves)
            {
                board.MakeMove(currentMove);
                int evaluation = -Search(board, timer, depth - 1, plyFromRoot + 1, -beta, -alpha);
                board.UndoMove(currentMove);

                if (evaluation > bestEval)
                {
                    bestEval = evaluation;
                    if (plyFromRoot == 0)
                        bestMove = currentMove;
                }

                alpha = Math.Max(alpha, bestEval);

                if (beta <= alpha)
                {
                    //if (!currentMove.IsCapture)
                    //killerMoves.Add(currentMove);
                    break;
                }
            }
            transpositionTable[board.ZobristKey] = new Transposition(board.ZobristKey, bestEval, depth,
                bestEval <= oldAlpha 
                    ? Transposition.NodeType.UPPERBOUND 
                    : bestEval >= beta 
                        ? Transposition.NodeType.LOWERBOUND 
                        : Transposition.NodeType.EXACT);
            return bestEval;
        }
        public Move Think(Board board, Timer timer)
        {
            //BitboardHelper.VisualizeBitboard(getPawnMask(board.GetPieceList(PieceType.Pawn, true)[0].Square, true)) ;
            for (int i = 0; i < 384; i++)
                pieceSquareValues[i / 64, i % 64] = extractBonusSquare(i / 64, i % 64);
            //Console.WriteLine($"{bestMove} Eval: " + 
            Search(board, timer, 5, 0, -5_000_000, 5_000_000)
            // * (board.IsWhiteToMove ? 1 : -1))
            ;
        
            return bestMove;
        }
    }

    public class EvilBot6 : IChessBot
    {
        int[] PieceValues = { 0, 100, 320, 330, 500, 900, 10000 };
        int CheckmateScore = 9999;

        ulong[] pst = {
            0x04BE53FA54055052,0x30C0140E5C47C052,0x2D41E3FA46C97852,0x1DC0D42052090052,
            0x0943C438550C7052,0x1442D3CC50C78052,0x2642C3F85D0A1052,0x2BC2E41059473052,
            0x33BE93F054C840B4,0x24BDA3FA5F4940D8,0x1B3FC42E56CCC88F,0x21C02436580BA8B1,
            0x213F145A62CB4096,0x2343A4406A0C78D0,0x1241D3EE5FCAC074,0x16C374124F8A0047,
            0x20BF43B05749104C,0x313F03E0648C6859,0x264083EE660BB06C,0x1D409402654C9071,
            0x1B41E3DC640D2893,0x2843941467CE908A,0x30430434648CD06B,0x1A43A3DA5ACBE83E,
            0x1CBE638A5A4A4044,0x1B3E63A45C8B105F,0x1F3F13C8600B2058,0x17BF13EE67CC3067,
            0x164003EA648BB069,0x18C12400648CB05E,0x1E3FF3AA5D0B1863,0x134023925ACB383B,
            0x0CBF837259CA2037,0x24BE73865E8AA850,0x17BF83A25E8B084D,0x11BF73B861CAF05E,
            0x0E3FF3CC63CB6863,0x0F3FD3AC5E4B2058,0x14C043C65DCB305C,0x0BBFE38C5C4A4839,
            0x1E3F33605B49D038,0x1E4033885F0A404E,0x1A3F639A5F0AE84E,0x0E3FF3985F0AD848,
            0x0F3FC3C05ECB2055,0x164033BA620B1055,0x1DC0F3B05FCB5073,0x17C063785DCA0846,
            0x25BDE3625C49A02F,0x28BF939A5F08E051,0x2140C3925F4A283E,0x054033A85B4A703B,
            0x0FC093B85D0A8043,0x1D4103D0608B186A,0x29BFE3AE638A1878,0x2940232C5B89F03C,
            0x1DC0039453074052,0x373EF3A05A89E052,0x2B3F83BC57C8B852,0x0A40B3DC56098052,
            0x293F23DA580A0052,0x173E83C85849A852,0x313E23705189F052,0x2C3CF3865609D052,
            0x0039F41A46C6F85E,0x13BBE4144507985E,0x1C3BE4244788605E,0x1C3C341E4847E85E,
            0x1FBC34184887D05E,0x2CBBB4184807F05E,0x273B24104606D05E,0x1CBBC40A4445B05E,
            0x1F39741648480110,0x2DBBC41A4948890B,0x2C3C841A4C0800FC,0x2DBD14164748B8E4,
            0x2DBE23FA498880F1,0x383C1406470800E2,0x30BC641049480903,0x2ABA840646C72919,
            0x2A39440E4AC808BC,0x2DBAE40E484828C2,0x30BB140E4A4918B3,0x2CBD940A4A0910A1,
            0x2F3D740849C8C096,0x3BBCB3FA4BC88093,0x3B3BB3F64A4830B0,0x2BBB13FA4B4780B2,
            0x213AB4084988407E,0x303BE4064C88E076,0x313C041A4D49786B,0x32BD54024C897863,
            0x323E14044DC9785C,0x35BD04024CC92062,0x323E13FE4B09086F,0x26BCC4044AC8386F,
            0x1C39640648C8386B,0x233C440A4B089867,0x2FBBB4104D89485B,0x313D74084F099057,
            0x32BC73F64C094857,0x30BCA3F44CC95056,0x29BCF3F04988E861,0x1FBBF3EA4808385D,
            0x1BB983F847481062,0x23B8D4004988B065,0x2ABB73F64C48C058,0x2FBAE3FE4CC9405F,
            0x30BB13F24D89185E,0x2D3B93E84B08B059,0x28BB23F04888285D,0x20BAD3E046881856,
            0x17B923F446C7786B,0x1FB913F445C82866,0x2738A40048887866,0x2BB984044A08A068,
            0x2C3983EE4B48B86B,0x273913EE4808285E,0x22B843EA46881060,0x1CB883FA43876857,
            0x0AB873EE4487E05E,0x1438C4044807305E,0x1AB924064488105E,0x1FB7D3FE4908505E,
            0x173A33F64808185E,0x1E3883E64648385E,0x193944084907385E,0x0FB7F3D84606C85E
        };

        public Move Think(Board board, Timer timer)
        {
            Move bestMove = default;
            int alpha;
            int beta = CheckmateScore;
            int score;
            Move[] move = board.GetLegalMoves();
            for (int depth = 1; depth < 99; depth++)
            {
                //Console.WriteLine(depth);
                alpha = -CheckmateScore;
                bestMove = default;
                for (int i = 0; i < move.Length; i++)
                {
                    board.MakeMove(move[i]);
                    if (board.IsInCheckmate())
                    {
                        board.UndoMove(move[i]);
                        return move[i];
                    }
                    if (board.IsDraw())
                    {
                        score = 0;
                    }
                    else score = -NegaMax(-beta, -alpha, depth, board);
                    board.UndoMove(move[i]);

                    if (score > alpha)
                    {
                        bestMove = move[i];
                        move[i] = move[0];
                        move[0] = bestMove;
                        alpha = score;
                    }
                }
                if (timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining / 100) break;
            }
            return bestMove;
        }

        private int NegaMax(int alpha, int beta, int depth, Board board)
        {
            int score;
            bool q = depth <= 0;
            int eval = Eval(board);

            if (q)
            {
                if (eval >= beta) return beta;
                alpha = Math.Max(alpha, eval);
            }

            if (!q && depth > 1 && eval - 10 >= beta && board.TrySkipTurn())
            {
                if (-NegaMax(-beta, -beta + 1, depth - 3 - depth / 4, board) >= beta)
                {
                    board.UndoSkipTurn();
                    return beta;
                }
                board.UndoSkipTurn();
            }

            Move[] move = board.GetLegalMoves(q);
            for (int i = 0; i < move.Length; i++)
            {
                Move tempMove;
                Piece capturedPiece1 = board.GetPiece(move[i].TargetSquare);
                int capturedPieceValue1 = PieceValues[(int)capturedPiece1.PieceType];
                for (int j = i + 1; j < move.Length; j++)
                {
                    Piece capturedPiece2 = board.GetPiece(move[j].TargetSquare);
                    int capturedPieceValue2 = PieceValues[(int)capturedPiece2.PieceType];
                    if (capturedPieceValue2 > capturedPieceValue1)
                    {
                        tempMove = move[i];
                        move[i] = move[j];
                        move[j] = tempMove;
                    }
                }

                board.MakeMove(move[i]);
                if (board.IsInCheckmate())
                {
                    board.UndoMove(move[i]);
                    return CheckmateScore - board.PlyCount;
                }
                if (board.IsDraw())
                    score = 0;
                else
                    if (beta - alpha > 1)
                {
                    score = -NegaMax(-alpha - 1, -alpha, depth - 2, board);
                    if (score > alpha)
                        score = -NegaMax(-beta, -alpha, depth - 1 - i / 4, board);
                }
                else
                {
                    score = -NegaMax(-beta, -alpha, depth - 1, board);
                }
                board.UndoMove(move[i]);

                if (score > alpha)
                {
                    if (score >= beta)
                        return beta;
                    alpha = score;
                }
            }
            return alpha;
        }

        private int Eval(Board board)
        {
            int eval = 0, mg = 0, eg = 0;
            foreach (PieceList list in board.GetAllPieceLists())
            {
                foreach (Piece piece in list)
                {
                    int square = piece.Square.Index ^ (piece.IsWhite ? 0x38 : 0),
                        type = (int)piece.PieceType - 1,
                        mul = piece.IsWhite ? 1 : -1;
                    mg += mul * (int)(pst[square] >> type * 11 & 0b11111111111);
                    eg += mul * (int)(pst[square + 64] >> type * 11 & 0b11111111111);
                    eval += 0x00042110 >> type * 4 & 0x0F;
                }
            }
            eval = eval < 24 ? eval : 24;
            return (mg * eval + eg * (24 - eval)) / 24 * (board.IsWhiteToMove ? 1 : -1);
        }
    }

}