using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Node object, contains the board state, number of simulations, number of wins, a list of child nodes, the move from its parent node to reach it, the parent node and the last moved piece
public class Node
{
    public PieceObject[,] boardState;
    public int simulations;
    public int wins;
    public List<Node> childNodes;
    public Move moveFromParent;
    public Node parentNode;
    public PieceObject lastMovedPiece;
}

// Piece object, contains the position, colour and kingship of a piece
public class PieceObject
{
    public bool isWhite;
    public bool isKing;
    public int x;
    public int y;
}

// Move object, contains the starting and ending positions
public class Move
{
    public int xStart;
    public int xEnd;
    public int yStart;
    public int yEnd;
}

public class MCTS : MonoBehaviour
{
    public CheckersManager checkersManager;
    public Node rootNode;
    public bool algorithmRunning;
    public float calculationTime = 5f;
    private bool isAlgorithm = true;
    private int movesSinceLastTake = 0;

    // UI
    public Slider slider;
    public Text text;

    // Set the calculation time and the text under the slider to the value of the slider
    public void UpdateCalcTime(Slider s, Text t)
    {
        calculationTime = s.value;
        t.text = s.value.ToString();
    }

    private void Update()
    {
        UpdateCalcTime(slider, text);
        // If it is the algorithm's turn and it is running and the root node has been created, execute the algorithm
        if (!checkersManager.turn && algorithmRunning && rootNode != null)
            ExecuteAlgorithm();
    }

    // Main algorithm loop, performs the selection, expansion, simulation and backpropogation steps whenever called
    private void ExecuteAlgorithm()
    {
        movesSinceLastTake = 1;
        Node selectedNode = Selection();
        Node expandedNode = Expansion(selectedNode);
        Stack<Node> simulatedEndNode = Simulation(expandedNode);
        Backpropogation(simulatedEndNode);
    }

    // Selects the most promising node
    private Node Selection()
    {
        int totalSimulations = rootNode.childNodes.Sum(i => i.simulations);
        float currentMax = 0f;
        Node currentNode = new Node();

        // Calculate the most promising node and replace the current node with it
        foreach (Node node in rootNode.childNodes)
        {
            float x = ((float)node.wins / (float)node.simulations) + Mathf.Sqrt(Mathf.Log((float)totalSimulations / (float)node.simulations));
            if (x > currentMax || currentMax == 0)
            {
                currentMax = x;
                currentNode = node;
            }
        }
        return currentNode;
    }

    // Creates a new child node of the selected node using a random move
    private Node Expansion(Node currentNode)
    {
        Move randMove = RandomMove(currentNode.boardState);
        PieceObject[,] newState = CreateStateFromMove(currentNode.boardState, randMove);
        PieceObject movedPiece = newState[randMove.xEnd, randMove.yEnd];
        currentNode.childNodes.Add(new Node
        {
            boardState = newState,
            childNodes = new List<Node>(),
            moveFromParent = randMove,
            parentNode = currentNode,
            simulations = 1,
            wins = 0,
            lastMovedPiece = movedPiece
        });

        // If last moved piece cannot take again change which player's turn it is
        if (ForcedMovesFromPiece(newState, movedPiece).Count() == 0)
            isAlgorithm = !isAlgorithm;
        // Return the expanded node
        return currentNode.childNodes[currentNode.childNodes.Count() - 1];
    }

    // Simmulates a game from a given state with random moves made
    private Stack<Node> Simulation(Node currentNode)
    {
        Stack<Node> nodeStack = new Stack<Node>();
        nodeStack.Push(currentNode);

        // While the game is not over, simulate a random move based on the current player and board state in the simulation
        while (CheckEndGame(nodeStack.Peek().boardState, movesSinceLastTake) == 0)
        {
            Node peekNode = nodeStack.Peek();
            Node sn = SimulateMove(nodeStack.Peek());
            nodeStack.Push(sn);
        }

        return nodeStack;
    }

    // Makes a random move in the simulated game
    private Node SimulateMove(Node currentNode)
    {
        Move randMove = RandomMove(currentNode.boardState);
        PieceObject[,] newState = CreateStateFromMove(currentNode.boardState, randMove);
        PieceObject movedPiece = newState[randMove.xEnd, randMove.yEnd];

        // Creates a new node based on the simulated move, with the previous node as its parent
        Node simNode = new Node
        {
            boardState = newState,
            childNodes = new List<Node>(),
            moveFromParent = randMove,
            parentNode = currentNode,
            simulations = 1,
            wins = 0,
            lastMovedPiece = movedPiece
        };

        // If last moved piece cannot take again change which player's turn it is
        if (ForcedMovesFromPiece(newState, movedPiece).Count() == 0)
            isAlgorithm = !isAlgorithm;
        return simNode;
    }

    // Updates the tree to reflect the result of the simulated game
    private void Backpropogation(Stack<Node> nodeStack)
    {
        int gameResult = CheckEndGame(nodeStack.Peek().boardState, movesSinceLastTake);
        // If draw or loss, update node to reflect 1 simulation but no win
        if (gameResult == 1 || gameResult == 3)
        {
            while (nodeStack.Count() > 1)
            {
                nodeStack.Pop();
            }
            Node n = nodeStack.Pop();
            n.parentNode.simulations += 1;
            n.parentNode.childNodes.Clear();
        }
        // If win, update node to reflect 1 simulation and 1 win
        else if (gameResult == 2)
        {
            while (nodeStack.Count() > 1)
            {
                nodeStack.Pop();
            }
            Node n = nodeStack.Pop();
            n.parentNode.simulations += 1;
            n.parentNode.wins += 1;
            n.parentNode.childNodes.Clear();
        }
        isAlgorithm = true;
    }

    // Iterates up the tree until it finds a child of the root node
    private Node GetParentNode(Node currentNode)
    {
        if (currentNode.parentNode != rootNode)
        {
            return GetParentNode(currentNode.parentNode);
        }
        else
        {
            return currentNode;
        }
    }

    // Returns all possible moves for the player whose turn it is in a given game state
    private List<Move> PossibleMoves(PieceObject[,] gameState)
    {
        List<PieceObject> piecesToMove = new List<PieceObject>();

        for (int x = 0; x <= 7; x++)
        {
            for (int y = 0; y <= 7; y++)
            {
                if (gameState[x, y] != null)
                    piecesToMove.Add(gameState[x, y]);
            }
        }

        // Removes all pieces that are not the colour being simulated. E.g. removes white pieces on algorithm's sim turn and black pieces on player's sim turn
        piecesToMove = piecesToMove.Where(i => i.isWhite != isAlgorithm).ToList();

        List<Move> possibleMoves = new List<Move>();
        // Adds all forced move to list of moves
        foreach (PieceObject pieceToMove in piecesToMove)
        {
            List<Move> movesForPiece = ForcedMovesFromPiece(gameState, pieceToMove);
            if (movesForPiece.Count() > 0)
                foreach (Move move in movesForPiece)
                    possibleMoves.Add(move);
        }
        // If there are no forced moves, adds all of the other possible moves
        if (possibleMoves.Count() == 0)
            foreach (PieceObject pieceToMove in piecesToMove)
            {
                List<Move> movesForPiece = OtherMovesFromPiece(gameState, pieceToMove);
                if (movesForPiece.Count() > 0)
                    foreach (Move move in movesForPiece)
                        possibleMoves.Add(move);
            }
        return possibleMoves;
    }

    // Returns a list of all forced moves for a piece
    private List<Move> ForcedMovesFromPiece(PieceObject[,] gameState, PieceObject piece)
    {
        List<Move> movesForPiece = new List<Move>();

        int x = piece.x;
        int y = piece.y;
            // Piece can move up and left 2
            if (x > 1 && y < 6)
            {
                // If the piece is white or a king and there is no piece 2 up and left
                if ((piece.isWhite || piece.isKing) && gameState[x - 2, y + 2] == null && gameState[x - 1, y + 1] != null)
                {
                    // If there is a piece of the opposite colour 1 up and left, the move is added to the list
                    if (gameState[x - 1, y + 1].isWhite == isAlgorithm)
                    {
                        movesForPiece.Add(new Move
                        {
                            xStart = x,
                            yStart = y,
                            xEnd = x - 2,
                            yEnd = y + 2
                        });
                    }
                }
            }
            
            // Piece can move up and right 2
            if (x < 6 && y < 6)
            {
                // If the piece is white or a king and there is no piece 2 up and right and there is a piece 1 up and right
                if ((piece.isWhite || piece.isKing) && gameState[x + 2, y + 2] == null && gameState[x + 1, y + 1] != null)
                {
                    // If there is a piece of the opposite colour 1 up and right, the move is added to the list
                    if (gameState[x + 1, y + 1].isWhite == isAlgorithm)
                    {
                        movesForPiece.Add(new Move
                        {
                            xStart = x,
                            yStart = y,
                            xEnd = x + 2,
                            yEnd = y + 2
                        });
                    }
                }
            }
            
            // Piece can move down and left 2
            if (x > 1 && y > 1)
            {
                // If the piece is black or a king and there is no piece 2 up and left
                if ((!piece.isWhite || piece.isKing) && gameState[x - 2, y - 2] == null && gameState[x - 1, y - 1] != null)
                {
                    // If there is a piece of the opposite colour 1 down and left, the move is added to the list
                    if (gameState[x - 1, y - 1].isWhite == isAlgorithm)
                    {
                        movesForPiece.Add(new Move
                        {
                            xStart = x,
                            yStart = y,
                            xEnd = x - 2,
                            yEnd = y - 2
                        });
                    }
                }
            }
            
            // Piece can move down and right 2
            if (x < 6 && y > 1)
            {
                // If the piece is black or a king and there is no piece 2 down and right and there is a piece 1 down and right
                if ((!piece.isWhite || piece.isKing) && gameState[x + 2, y - 2] == null && gameState[x + 1, y - 1] != null)
                {
                    // If there is a piece of the opposite colour 1 down and right, the move is added to the list
                    if (gameState[x + 1, y - 1].isWhite == isAlgorithm)
                    {
                        movesForPiece.Add(new Move
                        {
                            xStart = x,
                            yStart = y,
                            xEnd = x + 2,
                            yEnd = y - 2
                        });
                    }
                }
            }

        return movesForPiece;
    }

    // Returns a list of all regular, non-taking moves for a piece
    private List<Move> OtherMovesFromPiece(PieceObject[,] gameState, PieceObject piece)
    {
        List<Move> movesForPiece = new List<Move>();

        int x = piece.x;
        int y = piece.y;

        // Piece can move up and left 1
        if (x > 0 && y < 7)
        {
            // If the piece is white or a king and there is no piece up and left, the move is added to the list
            if ((piece.isWhite || piece.isKing) && gameState[x - 1, y + 1] == null)
            {
                movesForPiece.Add(new Move
                {
                    xStart = x,
                    yStart = y,
                    xEnd = x - 1,
                    yEnd = y + 1
                });
            }
        }

        // Piece can move up and right 1
        if (x < 7 && y < 7)
        {
            // If the piece is white or a king and there is no piece up and right, the move is added to the list
            if ((piece.isWhite || piece.isKing) && gameState[x + 1, y + 1] == null)
            {
                movesForPiece.Add(new Move
                {
                    xStart = x,
                    yStart = y,
                    xEnd = x + 1,
                    yEnd = y + 1
                });
            }
        }

        // Piece can move down and left 1
        if (x > 0 && y > 0)
        {
            // If the piece is black or a king and there is no piece down and left, the move is added to the list
            if ((!piece.isWhite || piece.isKing) && gameState[x - 1, y - 1] == null)
            {
                movesForPiece.Add(new Move
                {
                    xStart = x,
                    yStart = y,
                    xEnd = x - 1,
                    yEnd = y - 1
                });
            }
        }

        // Piece can move down and right 1
        if (x < 7 && y > 0)
        {
            // If the piece is black or a king and there is no piece down and right, the move is added to the list
            if ((!piece.isWhite || piece.isKing) && gameState[x + 1, y - 1] == null)
            {
                movesForPiece.Add(new Move
                {
                    xStart = x,
                    yStart = y,
                    xEnd = x + 1,
                    yEnd = y - 1
                });
            }
        }

        return movesForPiece;
    }

    // Creates a game state (PieceObject[,]) from an existing game state and a move. The result is the game state after the move has been made.
    public PieceObject[,] CreateStateFromMove(PieceObject[,] gameState, Move move)
    {
        // Creates a duplicate of the game state
        PieceObject[,] gameStateNew = new PieceObject[8, 8];
        for (int x = 0; x <= 7; x++)
            for (int y = 0; y <= 7; y++)
            {
                if (gameState[x, y] != null)
                    gameStateNew[x, y] = new PieceObject
                    {
                        isKing = gameState[x, y].isKing,
                        isWhite = gameState[x, y].isWhite,
                        x = x,
                        y = y
                    };
            }

        // Makes the move in the new game state
        PieceObject pieceToMove = gameStateNew[move.xStart, move.yStart];
        gameStateNew[move.xEnd, move.yEnd] = pieceToMove;
        gameStateNew[move.xStart, move.yStart] = null;
        gameStateNew[move.xEnd, move.yEnd].x = move.xEnd;
        gameStateNew[move.xEnd, move.yEnd].y = move.yEnd;

        // Removes any taken piece
        if (Math.Abs(move.yStart - move.yEnd) == 2)
        {
            int xTaken = (move.xEnd + move.xStart) / 2;
            int yTaken = (move.yEnd + move.yStart) / 2;

            gameStateNew[xTaken, yTaken] = null;
            movesSinceLastTake = 0;
        }
        else
            movesSinceLastTake += 1;

        // If a piece reaches the opposite end of the board it becomes a king
        if ((move.yEnd == 7 && gameStateNew[move.xEnd, move.yEnd].isWhite) || (move.yEnd == 0 && !gameStateNew[move.xEnd, move.yEnd].isWhite))
            gameStateNew[move.xEnd, move.yEnd].isKing = true;
        return gameStateNew;
    }

    // Generates a random move from the list of possible moves
    private Move RandomMove(PieceObject[,] gameState)
    {
        List<Move> possibleMoves = PossibleMoves(gameState);
        return possibleMoves[UnityEngine.Random.Range(0, possibleMoves.Count() - 1)];
    }

    // Returns an int. 0 if game not over, 1 if white win, 2 if black win, 3 if draw
    public int CheckEndGame(PieceObject[,] gameState, int mslt)
    {
        bool whiteRemaining = false;
        bool blackRemaining = false;

        //Check all pieces
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
                if (gameState[i, j] != null)
                    if (gameState[i, j].isWhite)
                        whiteRemaining = true;
                    else
                        blackRemaining = true;
        // If either no white pieces or no black pieces remain, return a win for the other colour
        if (!whiteRemaining)
            return 2;
        else if (!blackRemaining)
            return 1;
        else
        {
            // If there are no possible moves, return a draw
            if (PossibleMoves(gameState).Count() == 0 || mslt > 40)
            {
                return 3;
            }
        }
        return 0;
    }

    // Makes the best move calculated by the algorithm in the time given
    public void MakeBestMove()
    {
        // The most simulated move is likeliest to be the best move
        Move bestMove = rootNode.childNodes.OrderBy(i => i.simulations).First().moveFromParent;
        PieceObject p = rootNode.boardState[bestMove.xStart, bestMove.yStart];
        // Sends the piece and the move to the CheckersManager for it to make
        checkersManager.MonteCarloMove(p, bestMove);
    }

    // Converts a Piece[,] to a PieceObject[,]
    public PieceObject[,] ConvertPieceArrayToPieceObjectArray(Piece[,] boardState)
    {
        PieceObject[,] pieces = new PieceObject[8, 8];

        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
                if (boardState[i, j] != null)
                {
                    PieceObject p = new PieceObject
                    {
                        isWhite = boardState[i, j].isWhite,
                        isKing = boardState[i, j].isKing,
                        x = (int)(boardState[i, j].transform.position.x - 0.5f),
                        y = (int)(boardState[i, j].transform.position.z - 0.5f)
                    };
                    pieces[i, j] = p;
                }
        return pieces;
    }

    // Initialise the algorithm
    public void StartMCTS(Piece[,] boardState)
    {
        // Convert board state into PieceObject array
        PieceObject[,] pieces = ConvertPieceArrayToPieceObjectArray(boardState);
        
        // Create root node from board state
        rootNode = new Node
        {
            boardState = pieces,
            simulations = 0,
            wins = 0,
            childNodes = new List<Node>()
        };

        // For each possible move, create a new board state and a child node of the root node with a corresponding board state
        List<PieceObject[,]> boardStates = new List<PieceObject[,]>();
        int x = 0;
        List<Move> possibleMoves = PossibleMoves(pieces);
        foreach (Move move in possibleMoves)
        {
            boardStates.Add(CreateStateFromMove(pieces, move));
            rootNode.childNodes.Add(new Node
            {
                boardState = boardStates[x],
                simulations = 1,
                wins = 0,
                childNodes = new List<Node>(),
                moveFromParent = move,
                parentNode = rootNode
            });
            x++;
        }
        movesSinceLastTake = 0;
        isAlgorithm = false;

        // Start calculating the best move
        algorithmRunning = true;
        StartCoroutine(CalculateBestMove());
    }

    // Coroutine is called when the algorithm starts. Waits a given number of seconds and then stops the algorithm and makes the best move it has found
    public IEnumerator CalculateBestMove()
    {
        yield return new WaitForSeconds(calculationTime);
        algorithmRunning = false;
        MakeBestMove();
    }
}
