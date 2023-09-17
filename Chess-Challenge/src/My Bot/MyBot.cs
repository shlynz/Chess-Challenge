
using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    Board board;
    Timer timer;
    Move searchResult;
    int searchResultEval;
    int searchDepth = 3;
    int nodesChecked;
    int evalsRun;
    double averageTime = 0;

    // https://www.chessprogramming.org/Transposition_Table
    static ulong transpositionTableSize = Convert.ToUInt64(Math.Pow(2, 22));
    // Details: Key, Move, Depth, Score, Bound
    struct TranspositionTableEntry
    {
      public ulong key;
      public int depth, score, bound;
      public TranspositionTableEntry(ulong key, int depth, int score, int bound)
      {
        this.key = key;
        this.depth = depth;
        this.score = score;
        this.bound = bound;
      }
    }
    TranspositionTableEntry[] transpositionTable = new TranspositionTableEntry[transpositionTableSize];

    // Piece-Square Tables from https://www.chessprogramming.org/Piece-Square_Tables
    sbyte[,,] decodedTables = new sbyte[2,7,64];
    // Pawn, Queen, Rook, King, Bishop, Nothing, Knight
    int[] pieceValues = {100, 900, 500, 1200, 300, 0, 300};
    ulong[] encodedTableLines = {
      0x0000000000000000,
      0x435C2A412F5617F8,
      0xFC0512152C2611F2,
      0xF609040E10080CF0,
      0xEEFFFD080C0407EF,
      0xEEFDFDF9020217F8,
      0xE8FFF2F0F6101AF1,
      0x0000000000000000,
      0x0000000000000000,
      0x7A766C5C655A717F,
      0x40443A2E26243839,
      0x16100903FF030C0C,
      0x0906FEFBFBFB02FF,
      0x0305FC0100FDFFFB,
      0x09050507090001FB,
      0x0000000000000000,
      0xED001408281E1D1F,
      0xF0E5FD01F5271325,
      0xF7F4050514262027,
      0xEEEEF5F5FF0CFF01,
      0xFAEEFAF9FFFD02FE,
      0xF601F8FFFD010A03,
      0xE8FB0801050AFE01,
      0xFFF4FA07F6EFEBDE,
      0xFA0F0F12120D070E,
      0xF40E161C28111500,
      0xF204062220180D06,
      0x020F101F271B2719,
      0xF4130D2015171B10,
      0xF5EE0A04060C0703,
      0xF1F0EBF5F5F0E7EA,
      0xE9EDF1E3FDEAF2E4,
      0x161D16232B06151D,
      0x1216282A372E121E,
      0xFD0D12190C1F2A0B,
      0xF0F805121018FBF2,
      0xE7EEF8FF06FB04F0,
      0xE1EFF5F40200FDE9,
      0xE2F5F2FAFF08FCCF,
      0xF3F7010C0B05E7EE,
      0x09070C0A08080503,
      0x08090908FE020502,
      0x0505050303FEFDFE,
      0x030209010101FF01,
      0x02030503FDFCFBF8,
      0xFD00FDFFFBF8FBF5,
      0xFCFC0001FAFAF8FE,
      0xFA0102FFFDF703F2,
      0xD4100BF6DAE90109,
      0x14FFF2FBFBFDE6EC,
      0xFA1001F5F2040FF1,
      0xF4F2F8EEEBEFF6E7,
      0xDEFFEEE5E1E2E9DD,
      0xF6F6F1E1E2EBF6EE,
      0x0105FBD4E3F50605,
      0xF61908DB05ED100A,
      0xCDE8F4F4F80A03F4,
      0xF80C0A0C0C1A1008,
      0x070C100A0E1F1E09,
      0xFB0F101212171202,
      0xF4FD0E10121006F8,
      0xF3FE080E100B05FA,
      0xEEF803090A03FDF4,
      0xDCE9F2F8EDF6F0E3,
      0xEC03C8E7EFE305FB,
      0xEE0BF4F715280CE0,
      0xF5191D1B182219FF,
      0xFD030D22191905FF,
      0xFC09091217080703,
      0x000A0A0A0A120C07,
      0x030A0B00050E1701,
      0xE9FEF6F2F7F8E5F2,
      0xF6F2F8FBFBFAF4F0,
      0xFBFD05F8FEF7FDF6,
      0x01FB00FFFF040003,
      0xFE0608060A070201,
      0xFC02090D0507FEFA,
      0xF8FE05070902FBF6,
      0xF6F4FBFF03FAF6EE,
      0xF0FAF0FDFAF5FDF4,
      0x8EC3E9DE2ABEF6B7,
      0xCEE43119102A05F4,
      0xE029192C3958321E,
      0xFA0C0D24192F0C0F,
      0xF7030B09130D0EFB,
      0xF0FA08070D0C11F5,
      0xECDCF8FEFF0CF6F3,
      0xB8F2D8E9F4EDF3F0,
      0xD8E6F7EDEBEED5BC,
      0xEFFBEFFFFAEFF0DC,
      0xF0F20706FFFAF3E4,
      0xF4020F0F0F0805F4,
      0xF4FC0B110B0C03F4,
      0xF0FEFF0A07FEF2F1,
      0xE3F2F9FDFFF2F0E2,
      0xECDDF0F6F1F4DED4
    };

    public MyBot(){
      var watch = System.Diagnostics.Stopwatch.StartNew();
      // initialize the PST from the compacted data
      for (int index = 0, offset = 0; index < 96;  index++)
      {
        if(index == 80)
        {
          offset = 1;
        }
        byte[] bytes = BitConverter.GetBytes(encodedTableLines[index]);
        int gamephase = (index / 8) % 2,
            pieceNumber = index / 16,
            rankNr = index % 8;
        for (int byteIndex = 0; byteIndex < bytes.Length; byteIndex++)
        {
          decodedTables[gamephase, pieceNumber + offset, byteIndex + 8 * rankNr] = (sbyte)bytes[7-byteIndex];
        }
      }
      watch.Stop();
      Console.WriteLine($"Time to init: {watch.ElapsedTicks * (1000L*1000L*1000L) / System.Diagnostics.Stopwatch.Frequency}");
    }

    public Move Think(Board _board, Timer _timer)
    {
      var watch = System.Diagnostics.Stopwatch.StartNew();
      board = _board;
      timer = _timer;
      searchResult = Move.NullMove;
      nodesChecked = 0;
      Console.WriteLine($"Eval of opponent move:\t{eval(!board.IsWhiteToMove)}");
      evalsRun = 0;
      searchDepth = 3;
      long memoryUsage = System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 1000L / 1000L;
      int previousBest = -999999999;
      foreach (Move move in board.GetLegalMoves())
      {
        board.MakeMove(move);
        int result = -search(searchDepth, -999999999, 999999999);
        board.UndoMove(move);
        if (result > previousBest)
        {
          previousBest = result;
          searchResult = move;
        }
      }
      Console.WriteLine($"Searched Depth:\t\t{searchDepth}");
      Console.WriteLine($"Found move:\t\t{searchResult}");
      Console.WriteLine($"Nodes searched:\t\t{nodesChecked}");
      Console.WriteLine($"Evals run:\t\t{evalsRun}");
      Console.WriteLine($"Avg Eval Nanoseconds:\t{averageTime}");
      double searchTimeTaken = watch.ElapsedMilliseconds / 1000.0;
      Console.WriteLine($"Search time:\t\t{searchTimeTaken}s");
      Console.WriteLine($"Nodes per Second:\t{nodesChecked/searchTimeTaken}nps");
      Console.WriteLine($"Evals per Second:\t{evalsRun/searchTimeTaken}eps");
      Console.WriteLine($"Immediate TT-Results:\t{nodesChecked-evalsRun}");
      Console.WriteLine($"Shortcut percent:\t{100-((double)evalsRun/(double)nodesChecked*100)}%");
      Console.WriteLine($"Memory usage:\t\t{memoryUsage}");
      return searchResult;
      // return search();
    }

    private int eval(bool isWhite)
    {
      evalsRun++;
      var watch = System.Diagnostics.Stopwatch.StartNew();
      int total = 0, square = 0;
      // very crude evalutaion based on piece value and position according to Piece-Square Tables
      foreach (char fenChar in board.GetFenString().Split(' ')[0])
      {
        int fenCharNr = (int)fenChar;
        //Console.WriteLine(square);
        //bool isPiece = vals.TryGetValue(Char.ToUpper(fenChar), out val);
        if(fenCharNr < 65)
        {
          if(fenCharNr == 47) continue;
          square += (fenCharNr-1) & 7;
        }
        else
        {
          // result = value of piece + value of the square for specific piece
          int pieceNumber = Math.Max(fenCharNr | 32, 100) & 7,
              gamephase = 0,
              adjustedSquare = fenCharNr < 97 ? square ^ 56 : square;
          int result = pieceValues[pieceNumber] + decodedTables[gamephase,pieceNumber,adjustedSquare];
          // if it's a black piece, negate the result
          total += fenChar < 97 ? result : -result;
        }
        square++;
      }
      watch.Stop();
      approxRollingAverage(watch.ElapsedTicks * (1000L*1000L*1000L) / System.Diagnostics.Stopwatch.Frequency);
      // since the result favors white, negate it if evaluating for blacks position
      return isWhite ? total : -total;
    }

    // rewriting the search again 'cause I somehow borked it again
    // currently doesn't care about check or checkmate
    private int search(int depth, int alpha, int beta)
    {
      nodesChecked++;
      int originalAlpha = alpha;
      ulong zobristKey = board.ZobristKey;

      TranspositionTableEntry transpositionTableEntry = transpositionTable[zobristKey % transpositionTableSize];
      if (transpositionTableEntry.key == zobristKey && transpositionTableEntry.depth >= depth)
      {
        if (transpositionTableEntry.bound == 0)
        {
          return transpositionTableEntry.score;
        }
        else if (transpositionTableEntry.bound == -1)
        {
          alpha = Math.Max(alpha, transpositionTableEntry.score);
        }
        else
        {
          beta = Math.Min(beta, transpositionTableEntry.score);
        }

        if (alpha >= beta)
        {
          return transpositionTableEntry.score;
        }
      }

      Move[] moves = board.GetLegalMoves();
      if (depth == 0 || moves.Length == 0)
      {
        return eval(board.IsWhiteToMove);
      }

      int val = -999999999;
      foreach (Move move in moves)
      {
        board.MakeMove(move);
        val = Math.Max(val, -search(depth-1, -beta, -alpha));
        board.UndoMove(move);
        alpha = Math.Max(alpha, val);
        if(alpha >= beta)
        {
          break;
        }
      }

      int bound;
      if (val <= originalAlpha)
      {
        bound = 1;
      }
      else if (val >= beta)
      {
        bound = -1;
      }
      else
      {
        bound = 0;
      }
      transpositionTable[zobristKey % transpositionTableSize] = new TranspositionTableEntry(zobristKey, depth, val, bound);

      return val;
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

    private bool turnTimeElapsed()
    {
      return timer.MillisecondsElapsedThisTurn >= 1000;
    }

    private void approxRollingAverage(long newValue)
    {
      averageTime -= averageTime / 1000;
      averageTime += (double) newValue / 1000;
    }
}
