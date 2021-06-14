using System;
using UnityEngine;

public class Piece : MonoBehaviour
{
    // Piece information
    public bool isWhite;
    public bool isKing;

    // Checks whether the piece can move from a position to another position in a given board state
    public bool ValidMove(Piece[,] board, int xStart, int xEnd, int yStart, int yEnd)
    {
        if (board[xEnd, yEnd] != null)
            return false;

        int deltaMoveX = Math.Abs(xEnd - xStart);
        int deltaMoveY = yEnd - yStart;

        // White or king movement
        if (isWhite || isKing)
        {
            if (deltaMoveX == 1 && deltaMoveY == 1 && !ForcedMove(board, xStart, yStart))
                return true;
            else if (deltaMoveX == 2 && deltaMoveY == 2)
            {
                Piece p = board[(xStart + xEnd) / 2, (yStart + yEnd)/ 2];
                if (p != null && p.isWhite != isWhite)
                    return true;
            }
        }

        // Black or king movement
        if (!isWhite || isKing)
        {
            if (deltaMoveX == 1 && deltaMoveY == -1)
                return true;
            else if (deltaMoveX == 2 && deltaMoveY == -2)
            {
                Piece p = board[(xStart + xEnd) / 2, (yStart + yEnd) / 2];
                if (p != null && p.isWhite != isWhite)
                    return true;
            }
        }

        return false;
    }

    // Checks if the piece has any forced moves
    public bool ForcedMove(Piece[,] board, int x, int y)
    {
        if (isWhite || isKing)
        {
            if (x >= 2 && y <= 5)
            {
                Piece p = board[x - 1, y + 1];
                if (p != null && p.isWhite != isWhite)
                {
                    if (board[x - 2, y + 2] == null)
                        return true;
                }
            }
            if (x <= 5 && y <= 5)
            {
                Piece p = board[x + 1, y + 1];
                if (p != null && p.isWhite != isWhite)
                {
                    if (board[x + 2, y + 2] == null)
                        return true;
                }
            }
        }
        if (!isWhite || isKing)
        {
            if (x >= 2 && y >= 2)
            {
                Piece p = board[x - 1, y - 1];
                if (p != null && p.isWhite != isWhite)
                {
                    if (board[x - 2, y - 2] == null)
                        return true;
                }
            }
            if (x <= 5 && y >= 2)
            {
                Piece p = board[x + 1, y - 1];
                if (p != null && p.isWhite != isWhite)
                {
                    if (board[x + 2, y - 2] == null)
                        return true;
                }
            }
        }
        return false;
    }
}
