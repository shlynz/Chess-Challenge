
using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    Board board;
    int nodesChecked;

    // https://www.chessprogramming.org/Transposition_Table
    static ulong transpositionTableSize = 999999;
    struct TranspositionTableEntry
    {
      public ulong key;
      public Move move;
      public int depth, score;
      public TranspositionTableEntry(ulong key, Move move, int depth, int score)
      {
        this.key = key;
        this.move = move;
        this.depth = depth;
        this.score = score;
      }
    }
    TranspositionTableEntry[] transpositionTable = new TranspositionTableEntry[transpositionTableSize];

    // Piece-Square Tables from https://www.chessprogramming.org/Piece-Square_Tables
    // Need to be compressed/encoded somehow
    int[] mg_pawn_table = {
        0,   0,   0,   0,   0,   0,  0,   0,
      98, 134,  61,  95,  68, 126, 34, -11,
      -6,   7,  26,  31,  65,  56, 25, -20,
      -14,  13,   6,  21,  23,  12, 17, -23,
      -27,  -2,  -5,  12,  17,   6, 10, -25,
      -26,  -4,  -4, -10,   3,   3, 33, -12,
      -35,  -1, -20, -23, -15,  24, 38, -22,
        0,   0,   0,   0,   0,   0,  0,   0
    };

    int[] mg_knight_table = {
      -167, -89, -34, -49,  61, -97, -15, -107,
      -73, -41,  72,  36,  23,  62,   7,  -17,
      -47,  60,  37,  65,  84, 129,  73,   44,
        -9,  17,  19,  53,  37,  69,  18,   22,
      -13,   4,  16,  13,  28,  19,  21,   -8,
      -23,  -9,  12,  10,  19,  17,  25,  -16,
      -29, -53, -12,  -3,  -1,  18, -14,  -19,
      -105, -21, -58, -33, -17, -28, -19,  -23,
    };

    int[] mg_bishop_table = {
      -29,   4, -82, -37, -25, -42,   7,  -8,
      -26,  16, -18, -13,  30,  59,  18, -47,
      -16,  37,  43,  40,  35,  50,  37,  -2,
      -4,   5,  19,  50,  37,  37,   7,  -2,
      -6,  13,  13,  26,  34,  12,  10,   4,
        0,  15,  15,  15,  14,  27,  18,  10,
        4,  15,  16,   0,   7,  21,  33,   1,
      -33,  -3, -14, -21, -13, -12, -39, -21,
    };

    int[] mg_rook_table = {
        32,  42,  32,  51, 63,  9,  31,  43,
        27,  32,  58,  62, 80, 67,  26,  44,
        -5,  19,  26,  36, 17, 45,  61,  16,
        -24, -11,   7,  26, 24, 35,  -8, -20,
        -36, -26, -12,  -1,  9, -7,   6, -23,
        -45, -25, -16, -17,  3,  0,  -5, -33,
        -44, -16, -20,  -9, -1, 11,  -6, -71,
        -19, -13,   1,  17, 16,  7, -37, -26,
    };

    int[] mg_queen_table = {
        -28,   0,  29,  12,  59,  44,  43,  45,
        -24, -39,  -5,   1, -16,  57,  28,  54,
        -13, -17,   7,   8,  29,  56,  47,  57,
        -27, -27, -16, -16,  -1,  17,  -2,   1,
        -9, -26,  -9, -10,  -2,  -4,   3,  -3,
        -14,   2, -11,  -2,  -5,   2,  14,   5,
        -35,  -8,  11,   2,   8,  15,  -3,   1,
        -1, -18,  -9,  10, -15, -25, -31, -50,
    };

    int[] mg_king_table = {
        -65,  23,  16, -15, -56, -34,   2,  13,
        29,  -1, -20,  -7,  -8,  -4, -38, -29,
        -9,  24,   2, -16, -20,   6,  22, -22,
        -17, -20, -12, -27, -30, -25, -14, -36,
        -49,  -1, -27, -39, -46, -44, -33, -51,
        -14, -14, -22, -46, -44, -30, -15, -27,
          1,   7,  -8, -64, -43, -16,   9,   8,
        -15,  36,  12, -54,   8, -28,  24,  14,
    };

    public Move Think(Board _board, Timer timer)
    {
      board = _board;
      Console.WriteLine(eval(!board.IsWhiteToMove));
      return search();
    }

    private int eval(bool isWhite)
    {
      var vals = new Dictionary<char, int>()
      {
        {'P', 82},
        {'B', 365},
        {'N', 337},
        {'R', 477},
        {'Q', 1025},
        {'K', 10000}
      };
      int total = 0, val = 0, square = 0;
      // very crude evalutaion based on piece value and position according to Piece-Square Tables
      foreach (char fenChar in board.GetFenString().Split(' ')[0])
      {
        bool isPiece = vals.TryGetValue(Char.ToUpper(fenChar), out val);
        if(isPiece){
          bool isWhitePiece = (int)fenChar < 97;
          int adjSquare = !isWhitePiece ? square : square ^ 56;
          if(Char.ToUpper(fenChar) == 'P')
          {
            val += mg_pawn_table[adjSquare];
          }
          if(Char.ToUpper(fenChar) == 'N')
          {
            val += mg_knight_table[adjSquare];
          }
          if(Char.ToUpper(fenChar) == 'B'){
            val += mg_bishop_table[adjSquare];
          }
          if(Char.ToUpper(fenChar) == 'R'){
            val += mg_rook_table[adjSquare];
          }
          if(Char.ToUpper(fenChar) == 'Q'){
            val += mg_queen_table[adjSquare];
          }
          total += isWhitePiece ? val : -val;
        }
        else
        {
          if(Char.IsDigit(fenChar)) square += fenChar - '1';
        }
        square += fenChar == 47 ? 0 : 1;
        // Original idea about evaluating from the FEN-String
        //total += vals.TryGetValue(Char.ToUpper(fenChar), out val) ? ((int)fenChar < 97 ? val : -val) : 0;
      }
      return isWhite ? total : -total;
    }

    // Root of the Negamax-Search
    private Move search()
    {
      nodesChecked = 0;
      Move bestMove = Move.NullMove;
      int bestEval = -999999999;

      foreach (Move move in order(board.GetLegalMoves()))
      {
        board.MakeMove(move);
        int result = -rsearch(3, -999999999, 999999999);
        board.UndoMove(move);
        if(result > bestEval)
        {
          bestEval = result;
          bestMove = move;
        }
      }
      Console.WriteLine($"Nodes: {nodesChecked}, Move: {bestMove}, Eval: {bestEval}");
      return bestMove;
    }

    // Recursive part of the Negamax-Search
    private int rsearch(int depth, int alpha, int beta)
    {
      if(depth == 0) return eval(board.IsWhiteToMove);

      ulong currKey = board.ZobristKey;
      TranspositionTableEntry entry = transpositionTable[currKey % transpositionTableSize];

      if(entry.key == currKey)
      {
        return entry.score;
      }
      nodesChecked++;

      Move bestMove = Move.NullMove;
      int bestEval = -999999999;
      foreach (Move move in order(board.GetLegalMoves()))
      {
        board.MakeMove(move);
        int result = -rsearch(depth-1, -beta, -alpha);
        board.UndoMove(move);
        if(result > bestEval)
        {
          bestEval = result;
          bestMove = move;
          if(result >= beta)
          {
            return beta;
          }
          alpha = Math.Max(alpha, result);
        }
      }

      transpositionTable[currKey % transpositionTableSize] = new TranspositionTableEntry(currKey, bestMove, depth, bestEval);
      return alpha;
    }

    // Crude attempt to order moves, basically just puts captures infront, ordered by MVV/LVA idea
    private Move[] order(Move[] moves)
    {
      int[] moveScores = new int[moves.Length];
      for (int index = 0; index < moves.Length; index++)
      {
        Move move = moves[index];
        moveScores[index] = move.IsCapture ? (move.CapturePieceType - move.MovePieceType) * 100 : 0;
      }
      Array.Sort(moveScores, moves);
      return moves;
    }
}
