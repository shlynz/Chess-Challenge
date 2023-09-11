
using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    Board board;
    Move searchResult;
    int searchDepth = 3;
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
    sbyte[,,] decodedTables = new sbyte[2,6,64];
    ulong[] encodedTableLines = {
      0x0000000000000000,
      0x435C2A412F5617F8,
      0xFC0512152C2611F2,
      0xF609040E10080CF0,
      0xEEFFFD080C0407EF,
      0xEEFDFDF9020217F8,
      0xE8FFF2F0F6101AF1,
      0x0000000000000000,
      0x8EC3E9DE2ABEF6B7,
      0xCEE43119102A05F4,
      0xE029192C3958321E,
      0xFA0C0D24192F0C0F,
      0xF7030B09130D0EFB,
      0xF0FA08070D0C11F5,
      0xECDCF8FEFF0CF6F3,
      0xB8F2D8E9F4EDF3F0,
      0xEC03C8E7EFE305FB,
      0xEE0BF4F715280CE0,
      0xF5191D1B182219FF,
      0xFD030D22191905FF,
      0xFC09091217080703,
      0x000A0A0A0A120C07,
      0x030A0B00050E1701,
      0xE9FEF6F2F7F8E5F2,
      0x161D16232B06151D,
      0x1216282A372E121E,
      0xFD0D12190C1F2A0B,
      0xF0F805121018FBF2,
      0xE7EEF8FF06FB04F0,
      0xE1EFF5F40200FDE9,
      0xE2F5F2FAFF08FCCF,
      0xF3F7010C0B05E7EE,
      0xED001408281E1D1F,
      0xF0E5FD01F5271325,
      0xF7F4050514262027,
      0xEEEEF5F5FF0CFF01,
      0xFAEEFAF9FFFD02FE,
      0xF601F8FFFD010A03,
      0xE8FB0801050AFE01,
      0xFFF4FA07F6EFEBDE,
      0xD4100BF6DAE90109,
      0x14FFF2FBFBFDE6EC,
      0xFA1001F5F2040FF1,
      0xF4F2F8EEEBEFF6E7,
      0xDEFFEEE5E1E2E9DD,
      0xF6F6F1E1E2EBF6EE,
      0x0105FBD4E3F50605,
      0xF61908DB05ED100A,
      0x0000000000000000,
      0x7A766C5C655A717F,
      0x40443A2E26243839,
      0x16100903FF030C0C,
      0x0906FEFBFBFB02FF,
      0x0305FC0100FDFFFB,
      0x09050507090001FB,
      0x0000000000000000,
      0xD8E6F7EDEBEED5BC,
      0xEFFBEFFFFAEFF0DC,
      0xF0F20706FFFAF3E4,
      0xF4020F0F0F0805F4,
      0xF4FC0B110B0C03F4,
      0xF0FEFF0A07FEF2F1,
      0xE3F2F9FDFFF2F0E2,
      0xECDDF0F6F1F4DED4,
      0xF6F2F8FBFBFAF4F0,
      0xFBFD05F8FEF7FDF6,
      0x01FB00FFFF040003,
      0xFE0608060A070201,
      0xFC02090D0507FEFA,
      0xF8FE05070902FBF6,
      0xF6F4FBFF03FAF6EE,
      0xF0FAF0FDFAF5FDF4,
      0x09070C0A08080503,
      0x08090908FE020502,
      0x0505050303FEFDFE,
      0x030209010101FF01,
      0x02030503FDFCFBF8,
      0xFD00FDFFFBF8FBF5,
      0xFCFC0001FAFAF8FE,
      0xFA0102FFFDF703F2,
      0xFA0F0F12120D070E,
      0xF40E161C28111500,
      0xF204062220180D06,
      0x020F101F271B2719,
      0xF4130D2015171B10,
      0xF5EE0A04060C0703,
      0xF1F0EBF5F5F0E7EA,
      0xE9EDF1E3FDEAF2E4,
      0xCDE8F4F4F80A03F4,
      0xF80C0A0C0C1A1008,
      0x070C100A0E1F1E09,
      0xFB0F101212171202,
      0xF4FD0E10121006F8,
      0xF3FE080E100B05FA,
      0xEEF803090A03FDF4,
      0xDCE9F2F8EDF6F0E3
    };

    public MyBot(){
      // initialize the PST from the compacted data
      for (int index = 0;  index < encodedTableLines.Length;  index++)
      {
        byte[] bytes = BitConverter.GetBytes(encodedTableLines[index]);
        for (int byteIndex = 0; byteIndex < bytes.Length; byteIndex++)
        {
          // Gamephase = index/48
          // Piecenumber = (index%48)/8
          // Linenumber = index%8
          // Squarenumber = byteIndex
          // Indexing: Gamephase 0..1, Piece 0..5, Square 0..63
          decodedTables[index/48,(index%48)/8,byteIndex + 8 * (index%8)] = (sbyte)bytes[7-byteIndex];
        }
      }
    }

    public Move Think(Board _board, Timer timer)
    {
      board = _board;
      searchResult = Move.NullMove;
      nodesChecked = 0;
      Console.WriteLine($"Eval of opponent move:\t{eval(!board.IsWhiteToMove)}");
      Console.WriteLine($"Search eval:\t\t{singleSearchFunction(searchDepth, -999999999, 999999999)}");
      Console.WriteLine($"Found move:\t\t{searchResult}");
      Console.WriteLine($"Nodes searched:\t\t{nodesChecked}");
      return searchResult;
      // return search();
    }

    private int eval(bool isWhite)
    {
      int total = 0, square = 0;
      (sbyte, int) val;
      var vals = new Dictionary<char, (sbyte, int)>()
      {
        {'P', (0, 100)},
        {'R', (3, 500)},
        {'N', (1, 300)},
        {'B', (2, 300)},
        {'Q', (4, 900)},
        {'K', (5, 1200)},
      };
      // very crude evalutaion based on piece value and position according to Piece-Square Tables
      foreach (char fenChar in board.GetFenString().Split(' ')[0])
      {
        char upperFenChar = Char.ToUpper(fenChar);
        bool isPiece = vals.TryGetValue(upperFenChar, out val);
        if(isPiece){
          bool isWhitePiece = fenChar < 97;
          int adjSquare = isWhitePiece ? square ^ 56 : square;
          // result = value of piece + value of the square for specific piece
          int result = val.Item2 + decodedTables[0,val.Item1,adjSquare];
          // if it's a black piece, negate the result
          total += isWhitePiece ? result : -result;
        }
        else
        {
          // increment square counter by one less than the digit, since it'll increment by one later
          if(Char.IsDigit(fenChar)) square += fenChar - '1';
        }
        // if it's a '/', don't increment the square counter, otherwise do
        square += fenChar == 47 ? 0 : 1;
        // Original idea about evaluating from the FEN-String
        //total += vals.TryGetValue(Char.ToUpper(fenChar), out val) ? ((int)fenChar < 97 ? val : -val) : 0;
      }
      // since the result favors white, negate it if evaluating for blacks position
      return isWhite ? total : -total;
    }

    // attempt to compact the search into a single function
    private int singleSearchFunction(int depth, int alpha, int beta)
    {
      if(depth == 0) return eval(board.IsWhiteToMove);
      if(board.IsRepeatedPosition()) return 0;

      // check if the current position has been reached already
      ulong currentKey = board.ZobristKey;
      TranspositionTableEntry transpositionTableEntry = transpositionTable[currentKey % transpositionTableSize];
      // if it wasn't on the first iteration, return that score instead of evaluating again
      if(depth != searchDepth && transpositionTableEntry.key == currentKey)
      {
        return transpositionTableEntry.score;
      }
      nodesChecked++;

      Move bestMove = Move.NullMove;
      int bestEval = -999999999;
      foreach (Move move in order(board.GetLegalMoves()))
      {
        board.MakeMove(move);
        int result = -singleSearchFunction(depth-1, -beta, -alpha);
        board.UndoMove(move);

        if(result > bestEval)
        {
          bestEval = result;
          bestMove = move;
          if(depth == searchDepth)
          {
            searchResult = bestMove;
          }
          if(result >= beta)
          {
            break;
          }
          alpha = Math.Max(alpha, result);
        }
      }

      if(board.GetLegalMoves().Length == 0) return board.IsInCheck() ? -999999999 : 0;
      // add evaluated position to the transposition table
      transpositionTable[currentKey % transpositionTableSize] = new TranspositionTableEntry(currentKey, bestMove, depth, alpha);
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
