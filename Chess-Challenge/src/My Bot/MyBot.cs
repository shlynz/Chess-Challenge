using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    Board board;
    Timer timer;
    Move searchResult;
    int searchDepth,
        nodesChecked,
        evalsRun;
    double averageTime = 0;

    // https://www.chessprogramming.org/Transposition_Table
    static ulong transpositionTableSize = Convert.ToUInt64(Math.Pow(2, 22));
    // ulong key, int depth, int score, int bound, Move move
    (ulong, int, int, int, Move)[] transpositionTable = new (ulong, int, int, int, Move)[transpositionTableSize];

    // Piece-Square Tables from https://www.chessprogramming.org/Piece-Square_Tables
    sbyte[,,] decodedTables = new sbyte[2,7,64];
    // Pawn, Queen, Rook, King, Bishop, Nothing, Knight
    int[] pieceValues = {88, 981, 494, 1200, 331, 0, 309};
    ulong[] encodedTableLines = {
      // pawn late
      0x0404040404040404,0x7A776D5D655C717F,0x42463C3029273A3C,0x19140D0703070F0F,0x0D0A02FFFFFF0603,0x07090005040103FF,0x0D09090B0D0405FF,0x0404040404040404,
      // pawn early
      0xFCFCFCFCFCFCFCFC,0x3D55243B295013F5,0xF8010D1127210DEF,0xF305000A0B0407ED,0xEAFBF904070003EB,0xEBF9F9F5FEFE12F4,0xE5FBEFEDF20C15ED,0xFCFCFCFCFCFCFCFC,
      // queen late
      0xDDF1F1F5F5EFE9F0,0xD8F0F8FE09F3F7E3,0xD6E7E90302FAEFE9,0xE5F1F30109FD09FB,0xD7F5EF02F7F9FDF2,0xD8D1EDE7E9EEE9E6,0xD4D4CFD8D8D4CBCE,0xCDD0D4C6E0CED6C8,
      // queen early
      0x0B1D3025443A3A3B,0x0D031A1E13433041,0x1512222230423C43,0x0B0B13131D281C1E,0x170C17171C1B1F1B,0x141F161C1A1F2620,0x0618241F22271B1E,0x1D111724130D09FC,
      // rook late
      0x141217151313110F,0x13141413090D110D,0x1010100F0E090809,0x0E0D140C0D0C0B0D,0x0D0F110E08070604,0x090B080B07030601,0x07070B0D05050409,0x050D0D0B08030EFE,
      // rook early
      0x0A110A171FFB0911,0x070A1B1E2A210612,0xF101060D00131DFF,0xE5EDF906050CEFE7,0xDDE3EDF4FBF0F9E5,0xD7E4EAE9F7F5F1DF,0xD8EAE7EFF4FCF1C6,0xE8ECF500FFF9DCE3,
      // king late
      0xCFE9F4F4F90A03F5,0xF80B090B0B190F07,0x070B0F0A0D1E1D09,0xFB0F101211161102,0xF4FD0E10120F06F9,0xF3FE070E0F0B05FA,0xEEF903090903FDF5,0xDDE9F2F9EDF7F0E3,
      // king early
      0xD50F0BF6DBE90109,0x13FFF3FBFBFDE7ED,0xFA1001F5F3040FF1,0xF5F3F8EEECEFF7E8,0xE0FFEEE6E1E3EADE,0xF7F7F1E1E3ECF6EE,0x0105FBD6E3F50605,0xF61808DC05ED1009,
      // bishop late
      0xE0DCE2E4E5E3DEDA,0xE4E7EEE1E7E1E7E0,0xEBE4E9E9E8EDE9EC,0xE7EFF1EFF3F0EBEB,0xE5EBF2F6EEF0E7E3,0xE1E7EFF0F2EBE5E0,0xE0DEE5E9ECE3E0D8,0xDAE3DAE6E3DFE6DE,
      // bishop early
      0x0319E0FE06FB1B11,0x05210B0E2A3E22F7,0x0C2F33312E382F15,0x141A23382F2F1B15,0x131F1F282D1F1D19,0x172020202028221D,0x192021171B242C17,0x01150D090E0FFD09,
      // knight late
      0xC7D4E5DBD9DCC4AC,0xDDE8DDECE7DDDECB,0xDEE0F4F3EDE7E1D2,0xE2EFFCFCFCF5F3E1,0xE1E9F8FEF8F9F0E1,0xDEEBEDF7F4EBE0DF,0xD2E0E7EAECE0DED0,0xDACCDEE3DFE1CCC3,
      // knight early
      0xA4D8FCF23BD209CC,0xE2F7422A223C1707,0xF33A2B3E4A684330,0x0D1E1F362B401F21,0x0A151D1B251F200D,0x030D1B191F1E2308,0xFFEF0B11121F0906,0xCD05ECFD07000603
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
        for (int byteIndex = 0; byteIndex < bytes.Length; byteIndex++)
        {
          decodedTables[(index / 8) % 2, index / 16 + offset, byteIndex + 8 * (index%8)] = (sbyte)bytes[7-byteIndex];
        }
      }
      watch.Stop();
      Console.WriteLine($"Time to init: {watch.ElapsedTicks * (1000L*1000L*1000L) / System.Diagnostics.Stopwatch.Frequency}");
    }

    public Move Think(Board _board, Timer _timer)
    {
      var watch = System.Diagnostics.Stopwatch.StartNew();
      // transfer important stuff to global scope since it's fewer tokens
      board = _board;
      timer = _timer;
      searchResult = Move.NullMove;
      Console.WriteLine($"Eval of opponent move:\t{eval(!board.IsWhiteToMove)}");
      long memoryUsage = System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 1000L / 1000L;
      // as long as there is still time left in the current move, go one iteration deeper
      int totalEvalsRun = 0;
      int totalNodesChecked = 0;
      Move bestFullSearchResult = searchResult;
      for (searchDepth = 0; searchDepth < 25;)
      {
        nodesChecked = 0;
        evalsRun = 0;
        int _ = -search(++searchDepth, -99999, 99999);
          // keep note of the best root move
        Console.WriteLine($"Depth: {searchDepth}, Nodes: {nodesChecked}");
        totalEvalsRun += evalsRun;
        totalNodesChecked += nodesChecked;
        if (isTurnTimeOver()) break;
        bestFullSearchResult = searchResult;
      }
      Console.WriteLine($"Searched Depth:\t\t{searchDepth}");
      Console.WriteLine($"Found move:\t\t{searchResult}");
      Console.WriteLine($"Nodes searched:\t\t{totalNodesChecked}");
      Console.WriteLine($"Evals run:\t\t{totalEvalsRun}");
      Console.WriteLine($"Avg Eval Nanoseconds:\t{averageTime}");
      double searchTimeTaken = watch.ElapsedMilliseconds / 1000.0;
      Console.WriteLine($"Search time:\t\t{searchTimeTaken}s");
      Console.WriteLine($"Nodes per Second:\t{totalNodesChecked/searchTimeTaken}nps");
      Console.WriteLine($"Evals per Second:\t{totalEvalsRun/searchTimeTaken}eps");
      Console.WriteLine($"Immediate TT-Results:\t{totalNodesChecked-totalEvalsRun}");
      Console.WriteLine($"Shortcut percent:\t{100-((double)totalEvalsRun/(double)totalNodesChecked*100)}%");
      Console.WriteLine($"Memory usage:\t\t{memoryUsage}");
      return bestFullSearchResult;
    }

    private int eval(bool isWhite)
    {
      evalsRun++;
      var watch = System.Diagnostics.Stopwatch.StartNew();
      string fenString = board.GetFenString().Split(' ')[0];
      int total = 0,
          square = 0,
          pieceCount = fenString.Count(char.IsLetter)/17,
          pieceNumber,
          result;
      foreach (char fenChar in board.GetFenString().Split(' ')[0])
      {
        if(fenChar < 65)
        {
          if(fenChar == 47) continue;
          square += (fenChar-1) & 7;
        }
        else
        {
          // result = value of piece + value of the square for specific piece
          pieceNumber = Math.Max(fenChar | 32, 100) & 7;
          result = pieceValues[pieceNumber] + decodedTables[pieceCount,pieceNumber,fenChar < 97 ? square ^ 56 : square];
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

    private int search(int depth, int alpha, int beta)
    {
      nodesChecked++;
      int originalAlpha = alpha,
          bestScore = -99999;
      bool isRoot = searchDepth - depth == 0,
        depthReached = depth <= 0;
      ulong zobristKey = board.ZobristKey;
      Move bestMove = Move.NullMove;

      // Repeated position is worse than advantage, but better than disadvantage
      if (!isRoot && board.IsRepeatedPosition()) return 0;

      // check if the position has been evaluated already
      var (entryKey, entryDepth, entryScore, entryBound, entryMove) = transpositionTable[zobristKey % transpositionTableSize];
      if (!isRoot && entryKey == zobristKey && entryDepth >= depth)
      {
        if (entryBound == -1) alpha = Math.Max(alpha, entryScore);
        if (entryBound == 1) beta = Math.Min(beta, entryScore);
        if (entryBound == 0 || alpha >= beta) return entryScore;
      }

      Move[] moves = board.GetLegalMoves(depthReached);
      // only check captures after depth has been reached
      if (depthReached)
      {
        bestScore = eval(board.IsWhiteToMove);
        alpha = Math.Max(alpha, bestScore);
        if (beta < bestScore) return bestScore;
      }
      else if (moves.Length == 0)
      {
        // accept (latest) defeat instead of refusing all moves
        // accept stalemate if otherwise would result in losing
        return board.IsInCheck() ? searchDepth - depth - 99999 : 0;
      }

      int[] moveScores = new int[moves.Length];
      for (int index = 0; index < moves.Length; index++)
      {
        Move move = moves[index];
        moveScores[index] = -(
          entryMove == move ? 25000 :
          move.IsCapture ? ((int)move.CapturePieceType * 10) - (int)move.MovePieceType :
          0);
      }
      Array.Sort(moveScores, moves);

      foreach (Move move in moves)
      {
        // abort if the time cutoff has been reached
        // return something greater than max value to force future cutoffs
        if(isTurnTimeOver()) return 999999;

        board.MakeMove(move);
        int score = -search(depth-1, -beta, -alpha);
        board.UndoMove(move);
        if (bestScore < score)
        {
          bestScore = score;
          bestMove = move;
          if (isRoot)
          {
            searchResult = move;
          }
          alpha = Math.Max(alpha, bestScore);
          if (alpha >= beta)
          {
            break;
          }
        }
      }

      transpositionTable[zobristKey % transpositionTableSize] = (zobristKey, depth, bestScore, bestScore <= originalAlpha ? 1 : bestScore >= beta ? -1 : 0, bestMove);
      return bestScore;
    }

    // utility/debug function
    private void approxRollingAverage(long newValue)
    {
      averageTime -= averageTime / 1000;
      averageTime += (double) newValue / 1000;
    }

    private bool isTurnTimeOver()
    {
      // The average amount of turns per chess game, plus some leeway seems to be slightly above 40
      // thus the bot get's the time of the increment and around 1/40 of the remaining time on
      // it's clock for the current turn
      // (source: https://chess.stackexchange.com/a/2507)
      return timer.MillisecondsElapsedThisTurn - timer.IncrementMilliseconds > timer.MillisecondsRemaining / 40;
    }
}
