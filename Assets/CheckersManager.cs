using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CheckersManager : MonoBehaviour
{
    // Game state information
    public Piece[,] pieces = new Piece[8, 8];
    public GameObject whitePiece;
    public GameObject blackPiece;
    private Vector3 boardOffset = new Vector3(0.5f, 0, 0.5f);
    public MCTS monteCarlo;

    // True = white's turn, false = black's turn
    public bool turn = true;
    private Piece selectedPiece;
    private Vector3 startMove;
    private Vector3 endMove;

    private Vector2 mouseOver;
    private bool gameOver = false;
    private bool reloading = false;
    public Text text;
    private int movesSinceLastTake = 0;

    // Start is called before the first frame update
    void Start()
    {
        Generate();
    }

    // Update is called once per frame
    void Update()
    {
        if (!gameOver)
        {
            // Calculate hovered over square
            GetMousePos();
            int x = (int)mouseOver.x;
            int y = (int)mouseOver.y;

            // If white's turn and player clicks, let player select a piece
            if (Input.GetMouseButtonDown(0) && selectedPiece == null && turn)
                SelectPiece(x, y);
            // If they have already selected a piece and they click again, move the piece if it is a valid move
            else if (Input.GetMouseButtonDown(0) && selectedPiece != null && turn)
                if (selectedPiece.ValidMove(pieces, (int)startMove.x, x, (int)startMove.z, y))
                {
                    bool pieceTook = false;
                    MovePiece(selectedPiece, x, y);
                    // If the piece took, remove the taken piece
                    if (Math.Abs(x - (int)startMove.x) == 2)
                    {
                        movesSinceLastTake = 0;
                        Piece p = pieces[((int)startMove.x + x) / 2, ((int)startMove.z + y) / 2];
                        pieces[((int)startMove.x + x) / 2, ((int)startMove.z + y) / 2] = null;
                        Destroy(p.gameObject);
                        pieceTook = true;
                    }
                    EndTurn(y, pieceTook);
                }
                else
                    selectedPiece = null;
            // If it is black's turn and the algorithm isn't already running, start the algorithm
            else if (!turn && !monteCarlo.algorithmRunning)
                monteCarlo.StartMCTS(pieces);
        }
        // If the game is over, reload the scene to start a new game
        else if (!reloading)
            StartCoroutine(ReloadScene());
    }

    // Wait 5 seconds and restart the game
    private IEnumerator ReloadScene()
    {
        reloading = true;
        yield return new WaitForSeconds(5f);
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    // Called when the algorithm has finished calculating. Makes the best move the algorithm has calculated
    public void MonteCarloMove(PieceObject p, Move m)
    {
        bool pieceTook = false;
        MovePiece(pieces[p.x, p.y], m.xEnd, m.yEnd);
        movesSinceLastTake += 1;
        if (Math.Abs(m.xEnd - m.xStart) == 2)
        {
            Piece ptr = pieces[(m.xStart + m.xEnd) / 2, (m.yStart + m.yEnd) / 2];
            pieces[(m.xStart + m.xEnd) / 2, (m.yStart + m.yEnd) / 2] = null;
            Destroy(ptr.gameObject);
            pieceTook = true;
            movesSinceLastTake = 0;
        }
        selectedPiece = pieces[m.xEnd, m.yEnd];
        EndTurn(m.yEnd, pieceTook);
    }

    // Ends turn if piece that moved cannot take another piece
    private void EndTurn(int y, bool pieceTook)
    {
        // If piece reaches the other end of the board it becomes a king
        if (selectedPiece.isWhite && !selectedPiece.isKing && y == 7)
        {
            selectedPiece.isKing = true;
            selectedPiece.transform.Rotate(Vector3.right * 180);
        }
        else if (!selectedPiece.isWhite && !selectedPiece.isKing && y == 0)
        {
            selectedPiece.isKing = true;
            selectedPiece.transform.Rotate(Vector3.right * 180);
        }
        // If the piece that moved has any forced moves and it took this turn, it must take again
        if (selectedPiece.ForcedMove(pieces, (int)(selectedPiece.transform.position.x - 0.5f), y) && pieceTook)
            return;
        // Otherwise, change turn and unselect the piece
        turn = !turn;
        selectedPiece = null;

        // Convert board state to PieceObject array and check if the game is over
        PieceObject[,] boardState = monteCarlo.ConvertPieceArrayToPieceObjectArray(pieces);
        int gameResult = monteCarlo.CheckEndGame(boardState, movesSinceLastTake);
        // If the game is a win
        if (gameResult == 1)
        {
            gameOver = true;
            text.text = "WIN :)";
        }
        // If the game is a loss
        else if (gameResult == 2)
        {
            gameOver = true;
            text.text = "LOSS :(";
        }
        // If the game is a draw
        else if (gameResult == 3)
        {
            gameOver = true;
            text.text = "DRAW :|";
        }
    }

    // Select piece if there is one at the selected location
    private void SelectPiece(int x, int y)
    {
        // Out of bounds
        if (x < 0 || y < 0)
            return;
        Piece p = pieces[x, y];
        List<Piece> forcedPieces = CheckForcedMoves(pieces);
        // If the clicked square has a piece and either it has a forced move or there are no forced moves, select the piece
        if (p != null && forcedPieces.Where(i => i.isWhite == turn).ToList().Count == 0 || forcedPieces.Contains(p))
            if (p.isWhite == turn)
            {
                selectedPiece = p;
                startMove = new Vector3(x, 1.05f, y);
            }
    }

    // Return currently hovered over board square
    private void GetMousePos()
    {
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, float.MaxValue, LayerMask.GetMask("Board")))
        {
            mouseOver = new Vector2((int)hit.point.x, (int)hit.point.z);
        }
        else
        {
            mouseOver = new Vector2(-1, -1);
        }
    }

    // Generate pieces at starting locations
    private void Generate()
    {
        bool oddRow = false;

        // Init white pieces
        for (int y = 0; y <= 2; y++)
        {
            for (int x = 0; x <= 7; x += 2)
            {
                GeneratePiece(whitePiece, oddRow ? x + 1 : x, y);
            }
            oddRow = !oddRow;
        }

        // Init black pieces
        for (int y = 5; y <= 7; y++)
        {
            for (int x = 0; x <= 7; x += 2)
            {
                GeneratePiece(blackPiece, oddRow ? x + 1 : x, y);
            }
            oddRow = !oddRow;
        }
    }

    // Generate piece at given x and y coordinates
    private void GeneratePiece(GameObject piece, int x, int y)
    {
        GameObject newPiece = Instantiate(piece);
        newPiece.transform.SetParent(transform);

        Piece p = newPiece.GetComponent<Piece>();
        pieces[x, y] = p;
        InitPiece(p, x, y);
    }

    // Place piece at starting location
    private void InitPiece(Piece p, int x, int y)
    {
        // Out of bounds
        if (x < 0 || y < 0)
            return;
        p.transform.position = new Vector3(x + boardOffset.x, 1.05f, y + boardOffset.z);
    }

    // Move piece to position
    public void MovePiece(Piece piece, int x, int y)
    {
        // Out of bounds
        if (x < 0 || y < 0)
            return;

        // Remove the piece from its old square and add it to its new one, updating its position in both the game state and on the board
        pieces[(int)piece.transform.position.x, (int)piece.transform.position.z] = null;
        piece.transform.position = new Vector3(x + boardOffset.x, 1.05f, y + boardOffset.z);
        pieces[x, y] = piece;
        movesSinceLastTake += 1;
    }

    // Returns list of pieces that are currently forced to move
    public List<Piece> CheckForcedMoves(Piece[,] gameState)
    {
        List<Piece> forcedPieces = new List<Piece>();

        //Check all pieces, if a piece has any forced moves it will be added to the list
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
                if (gameState[i, j] != null)
                    if (gameState[i, j].ForcedMove(gameState, i, j))
                        forcedPieces.Add(gameState[i, j]);
        return forcedPieces;
    }
}
