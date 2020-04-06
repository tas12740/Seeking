﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;

public class TileSeeker : MonoBehaviour
{
    private enum Map
    {
        Blank,
        MaybeWall,
        InnerWall,
        OuterWall,
        Seen,
        Unknown
    }
    private enum Mode
    {
        ReadyToScan,
        Scan,
        ReadyToMove,
        RotateToMove,
        Move,
        ScanWallStart,
        ScanWall,
        MoveToCorner,
        FindCorner
    }
    private Mode currMode;

    public int rows;
    public int cols;
    public Vector2 originPoint;

    private bool seesHider;

    private float moveRotation;
    private float currMoveRotation;
    private float rotationAmountBeforeMove;

    ////////////////////////////////////////
    // WALL SCANNING VARIABLES           //
    //////////////////////////////////////
    private List<(Vector2, Vector2)> walls = new List<(Vector2, Vector2)>();
    private Vector2 originalPoint = Vector2.positiveInfinity;
    private bool horizontalWall = false;
    private bool setCorner = false;
    private Vector2 otherCorner = 200 * Vector2.one;
    private double originalDirectionBeforeWallScan;
    private HashSet<Vector2> cornersSeen = new HashSet<Vector2>();
    private Vector2 cornerLocation = Vector2.zero;
    private bool wentOtherDirection = false;
    private float cornerUpdate = 0f;
    private float lastCornerDistance;
    private Vector2 lastCornerSearch;
    private float currCornerDistance;
    private Vector2 currCornerSearch;
    private const float CORNERUPDATENEGATIVE = -1f;
    private const float CORNERUPDATEPOSITIVE = 1f;
    private Stack<int> path = new Stack<int>();
    private int goalBlock = -1;
    private Collider2D innerWallCollider;

    private double lookingDirection = 0;
    private float currRotation = 0;
    private bool didRotate = false;
    public LineRenderer line;
    private Map[,] edges;
    private bool[,] blockGraph;
    private HashSet<int> removedBlocks = new HashSet<int>();
    public float timeStep = 1f;
    public float timeStepScanWall;
    private float timer = 0f;
    private System.Random random;
    private Dictionary<int, bool> locationMap;

    // Start is called before the first frame update
    void Start()
    {
        this.random = new System.Random();

        this.currMode = Mode.ReadyToScan;
        this.seesHider = false;

        line.positionCount = 2;
        line.material.color = Color.black;

        this.innerWallCollider = GameObject.FindGameObjectsWithTag("Inner")[0].GetComponent<Collider2D>() as Collider2D;

        int finalVertex = (this.rows + 1) * (this.cols + 1);
        // Debug.Log($"Final vertex: {finalVertex}");
        edges = new Map[finalVertex + 1, finalVertex + 1];

        for (int r = 0; r < edges.GetLength(0); r++)
        {
            for (int c = 0; c < edges.GetLength(1); c++)
            {
                edges[r, c] = Map.Blank;
                edges[c, r] = Map.Blank;
            }
        }

        for (int i = 0; i < finalVertex; i++)
        {
            edges[i, i + 1] = Map.Unknown;
            edges[i + 1, i] = Map.Unknown;
            if (i + (cols + 1) < finalVertex)
            {
                edges[i, i + (cols + 1)] = Map.Unknown;
                edges[i + (cols + 1), i] = Map.Unknown;
            }
        }

        // generate the outer walls
        for (int i = 0; i <= this.cols; i++)
        {
            edges[i, i + 1] = Map.OuterWall;
            edges[i + 1, i] = Map.OuterWall;
        }
        for (int i = this.cols; i < finalVertex - 1; i += (this.cols + 1))
        {
            edges[i, i + (cols + 1)] = Map.OuterWall;
            edges[i + (cols + 1), i] = Map.OuterWall;
        }
        for (int i = 0; i <= (finalVertex - (this.cols + 1)); i += (this.cols + 1))
        {
            edges[i, i + (this.cols + 1)] = Map.OuterWall;
            edges[i + (this.cols + 1), i] = Map.OuterWall;
        }
        for (int i = (finalVertex - (this.cols + 1)); i < finalVertex; i++)
        {
            edges[i, i + 1] = Map.OuterWall;
            edges[i + 1, i] = Map.OuterWall;
        }

        int finalBlock = rows * cols - 1;
        // Debug.Log($"Final block: {finalBlock}");

        this.blockGraph = new bool[finalBlock + 1, finalBlock + 1];
        for (int i = 0; i < finalBlock; i++)
        {
            // not in the rightmost column
            if (!EnumerableUtility.Range(cols - 1, finalBlock + 1, cols).Contains(i))
            {
                // E
                blockGraph[i, i + 1] = true;
                blockGraph[i + 1, i] = true;

                if (i < finalBlock - cols)
                {
                    // SE
                    blockGraph[i, i + (cols + 1)] = true;
                    blockGraph[i + (cols + 1), i] = true;
                }
            }
            if (i < (finalBlock + 1 - cols))
            {
                // S
                blockGraph[i, i + cols] = true;
                blockGraph[i + cols, i] = true;
            }
            if (i >= cols)
            {
                // N
                blockGraph[i, i - cols] = true;
                blockGraph[i - cols, i] = true;
            }
            if (!EnumerableUtility.Range(0, finalBlock + 2 - cols, cols).Contains(i))
            {
                // W
                blockGraph[i, i - 1] = true;
                blockGraph[i - 1, i] = true;

                if (i < (finalBlock + 1 - cols))
                {
                    // SW
                    blockGraph[i, i + (cols - 1)] = true;
                    blockGraph[i + (cols - 1), i] = true;
                }
            }
        }

        this.locationMap = new Dictionary<int, bool>();
        for (int i = 0; i < rows * cols; i++)
        {
            this.locationMap.Add(i, false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        this.locationMap[this.mapCoordinatesToBlock(new Vector2(this.transform.position.x, this.transform.position.y))] = true;
        float xDir = (float)Math.Cos(this.lookingDirection * Math.PI / 180);
        float yDir = (float)Math.Sin(this.lookingDirection * Math.PI / 180);

        Vector3 dir = new Vector3(xDir, yDir, 0);
        Ray ray = new Ray(this.transform.position, dir);

        line.SetPosition(0, ray.origin);
        line.SetPosition(1, ray.origin + ray.direction);

        this.timer += Time.deltaTime;
        if (this.currMode == Mode.ScanWall)
        {
            if (this.timer > this.timeStepScanWall)
            {
                doUpdate();
                this.timer = 0;
            }
        }
        else
        {
            if (this.timer > this.timeStep)
            {
                doUpdate();
                this.timer = 0;
            }
        }
    }

    private void doUpdate()
    {
        if (this.currMode == Mode.RotateToMove)
        {
            this.rotateToMove();
        }
        else if (this.currMode == Mode.Move)
        {
            this.move();

            if (this.currMode == Mode.FindCorner)
            {
                return;
            }

            if (this.path.Count == 0)
            {
                this.currMode = Mode.ReadyToScan;
                this.goalBlock = -1;
            }
            else
            {
                // move again
                this.currMode = Mode.ReadyToMove;
            }
        }
        else if (this.currMode == Mode.ReadyToMove)
        {
            this.readyToMove();
            this.currMode = Mode.RotateToMove;
            this.doUpdate();
        }
        else if (this.currMode == Mode.Scan)
        {
            if (this.currRotation == 360)
            {
                this.currMode = Mode.ReadyToMove;

                this.didRotate = false;
                this.currRotation = 0;
            }
            else
            {
                if (!this.seesHider)
                {
                    if (!this.didRotate && this.currRotation == 0)
                    {
                        this.didRotate = true;
                    }
                    else
                    {
                        this.currRotation += 45f;
                        this.lookingDirection -= 45;
                        this.transform.Rotate(new Vector3(0, 0, -45f));
                    }
                }

                float xDir = (float)Math.Cos(this.lookingDirection * Mathf.Deg2Rad);
                float yDir = (float)Math.Sin(this.lookingDirection * Mathf.Deg2Rad);

                Vector3 dir = new Vector3(xDir, yDir, 0);

                int playerMask = 1 << 8;
                playerMask = ~playerMask;

                RaycastHit2D hit = Physics2D.Raycast(this.transform.position, dir, Mathf.Infinity, playerMask);
                if (hit.collider.gameObject.tag.Equals("Hider"))
                {
                    this.transform.position = this.transform.position + this.movement();
                    this.seesHider = true;
                    return;
                }
                else
                {
                    this.seesHider = false;
                }

                // Debug.Log(hit.point);

                bool seesInnerWall = markEdges(this.transform.position, hit.point, new Vector2(dir.x, dir.y));
                // Debug.Log($"Sees Inner Wall: {seesInnerWall}, Is On Wall: {isOnSeenWall(hit.point)}");
                if (seesInnerWall && !this.isOnSeenWall(hit.point))
                {
                    this.currMode = Mode.ScanWallStart;
                }
            }
        }
        else if (this.currMode == Mode.ReadyToScan)
        {
            this.didRotate = false;
            this.currRotation = 0;
            this.currMode = Mode.Scan;
        }
        else if (this.currMode == Mode.ScanWallStart)
        {
            this.scanWallStart();
            this.currMode = Mode.ScanWall;
        }
        else if (this.currMode == Mode.ScanWall)
        {
            this.scanWall();
        }
        else if (this.currMode == Mode.FindCorner)
        {
            this.findCorner();
        }
    }

    private bool isOnSeenWall(Vector2 point)
    {
        foreach ((Vector2, Vector2) tup in this.walls)
        {
            Vector2 one = tup.Item1;
            Vector2 two = tup.Item2;
            bool horizontal = Mathf.Abs(one.y - two.y) < Mathf.Pow(10, -3);
            if (horizontal)
            {
                float minX = Mathf.Min(one.x, two.x);
                float maxX = Mathf.Max(one.x, two.x);
                bool onWall = Mathf.Abs(point.y - one.y) < Mathf.Pow(10, -3) && point.x <= maxX && point.x >= minX;
                if (onWall)
                {
                    return true;
                }
            }
            else
            {
                float minY = Mathf.Min(one.y, two.y);
                float maxY = Mathf.Max(one.y, two.y);
                bool onWall = Mathf.Abs(point.x - one.x) < Mathf.Pow(10, -3) && point.y <= maxY && point.y >= minY;
                if (onWall)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void findCorner()
    {
        if (this.goalBlock == -1)
        {
            Vector2 nodeLocation = this.closestNode(this.cornerLocation);
            // Debug.Log($"Node location: {nodeLocation}");
            float node = Mathf.Round(this.mapNode(nodeLocation.y, nodeLocation.x));
            // Debug.Log($"Node: {node}");
            List<Vector2> edges = this.edgesForNode(node);

            Vector2 top = edges[0];
            Vector2 right = edges[1];
            Vector2 left = edges[2];
            Vector2 down = edges[3];
            // Debug.Log($"{top}, {right}, {down}, {left}");

            Map topMap = this.edges[Mathf.RoundToInt(top.x), Mathf.RoundToInt(top.y)];
            Map rightMap = this.edges[Mathf.RoundToInt(right.x), Mathf.RoundToInt(right.y)];
            Map leftMap = this.edges[Mathf.RoundToInt(left.x), Mathf.RoundToInt(left.y)];
            Map downMap = this.edges[Mathf.RoundToInt(down.x), Mathf.RoundToInt(down.y)];

            List<int> blocks = this.openBlocks(cornerLocation, topMap, leftMap, rightMap, downMap);

            if (blocks.Count == 0)
            {
                Debug.Log($"Found no blocks! {this.cornerLocation}");
                Debug.Log($"{topMap}-{rightMap}-{downMap}-{leftMap}");
                Debug.Break();
            }

            int blockChoice = (int)blocks[this.random.Next(blocks.Count)];
            // Debug.Log($"Block choice: {blockChoice}");

            this.goalBlock = blockChoice;
        }


        this.generatePath(this.mapCoordinatesToBlock(this.transform.position), this.goalBlock);

        StringBuilder sb = new StringBuilder();
        int firstBlock = this.mapCoordinatesToBlock(this.transform.position);
        sb.Append($"Path: {firstBlock}");
        foreach (int block in this.path)
        {
            sb.Append($" -> {block}");
        }
        Debug.Log(sb.ToString());

        this.currMode = Mode.ReadyToMove;
    }

    private void generatePath(int currBlock, int endBlock)
    {
        this.path.Clear();

        Queue<int> searchQueue = new Queue<int>();

        int arrLength = cols * rows;
        bool[] visited = new bool[arrLength];
        for (int i = 0; i < visited.Length; i++)
        {
            visited[i] = false;
        }

        int[] distances = new int[arrLength];
        for (int i = 0; i < distances.Length; i++)
        {
            distances[i] = int.MaxValue;
        }
        distances[currBlock] = 0;

        int[] previous = new int[arrLength];
        for (int i = 0; i < previous.Length; i++)
        {
            previous[i] = int.MaxValue;
        }

        searchQueue.Enqueue(currBlock);
        while (searchQueue.Count != 0)
        {
            int curr = searchQueue.Dequeue();
            // Debug.Log($"Searching: {curr}");
            visited[curr] = true;
            for (int i = 0; i < this.blockGraph.GetLength(0); i++)
            {
                if (this.blockGraph[curr, i])
                {
                    if (!visited[i])
                    {
                        distances[i] = distances[curr] + 1;
                        previous[i] = curr;
                        visited[i] = true;
                        searchQueue.Enqueue(i);
                    }
                }
            }
        }

        int currItem = previous[endBlock];
        this.path.Push(endBlock);
        while (currItem != int.MaxValue)
        {
            this.path.Push(currItem);
            currItem = previous[currItem];
        }
        this.path.Pop();
    }

    private List<int> openBlocks(Vector2 cornerLocation, Map topMap, Map leftMap, Map rightMap, Map downMap)
    {
        HashSet<int> blocks = new HashSet<int>();

        int countUnknown = (new List<Map> { topMap, leftMap, rightMap, downMap }).FindAll(item => item == Map.Unknown).Count;

        float x = Mathf.Round(cornerLocation.x);
        float y = Mathf.Round(cornerLocation.y);
        if (countUnknown == 1)
        {
            if (leftMap == Map.InnerWall)
            {
                if (topMap == Map.Seen && rightMap == Map.Seen)
                {
                    this.classifyEdge(this.mapPointToEdge(new Vector2(x, y - 0.5f)), Vector2.one, Vector2.left, false, true);
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x + 0.5f, y - 0.5f)));
                }
                else if (downMap == Map.Seen && rightMap == Map.Seen)
                {
                    this.classifyEdge(this.mapPointToEdge(new Vector2(x, y + 0.5f)), Vector2.one, Vector2.left, false, true);
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x + 0.5f, y + 0.5f)));
                }
                else if (downMap == Map.Seen && rightMap == Map.InnerWall)
                {
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x - 0.5f, y - 0.5f)));
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x + 0.5f, y - 0.5f)));
                }
                else if (topMap == Map.Seen && rightMap == Map.InnerWall)
                {
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x - 0.5f, y + 0.5f)));
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x + 0.5f, y + 0.5f)));
                }
            }
            if (rightMap == Map.InnerWall)
            {
                if (topMap == Map.Seen && leftMap == Map.Seen)
                {
                    Vector2 edge = this.mapPointToEdge(new Vector2(x, y - 0.5f));
                    this.classifyEdge(edge, Vector2.one, Vector2.right, false, true);
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x - 0.5f, y - 0.5f)));

                }
                else if (downMap == Map.Seen && leftMap == Map.Seen)
                {
                    Vector2 edge = this.mapPointToEdge(new Vector2(x, y + 0.5f));
                    this.classifyEdge(edge, Vector2.one, Vector2.right, false, true);
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x - 0.5f, y + 0.5f)));
                }
                else if (downMap == Map.Seen && leftMap == Map.InnerWall)
                {
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x - 0.5f, y - 0.5f)));
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x + 0.5f, y - 0.5f)));
                }
                else if (topMap == Map.Seen && leftMap == Map.InnerWall)
                {
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x - 0.5f, y + 0.5f)));
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x + 0.5f, y + 0.5f)));
                }
            }
            if (topMap == Map.InnerWall)
            {
                if (downMap == Map.Seen && leftMap == Map.Seen)
                {
                    Vector2 edge = this.mapPointToEdge(new Vector2(x + 0.5f, y));
                    this.classifyEdge(edge, Vector2.one, Vector2.up, false, true);
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x + 0.5f, y - 0.5f)));
                }
                else if (downMap == Map.Seen && rightMap == Map.Seen)
                {
                    Vector2 edge = this.mapPointToEdge(new Vector2(x - 0.5f, y));
                    this.classifyEdge(edge, Vector2.one, Vector2.up, false, true);
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x - 0.5f, y - 0.5f)));
                }
                else if (downMap == Map.InnerWall && rightMap == Map.Seen)
                {
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x + 0.5f, y + 0.5f)));
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x + 0.5f, y - 0.5f)));
                }
                else if (downMap == Map.InnerWall && leftMap == Map.Seen)
                {
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x - 0.5f, y + 0.5f)));
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x - 0.5f, y - 0.5f)));
                }
            }
            if (downMap == Map.InnerWall)
            {
                if (leftMap == Map.Seen && topMap == Map.Seen)
                {
                    Vector2 edge = this.mapPointToEdge(new Vector2(x + 0.5f, y));
                    this.classifyEdge(edge, Vector2.one, Vector2.down, false, true);
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x + 0.5f, y + 0.5f)));
                }
                else if (rightMap == Map.Seen && topMap == Map.Seen)
                {
                    Vector2 edge = this.mapPointToEdge(new Vector2(x - 0.5f, y));
                    this.classifyEdge(edge, Vector2.one, Vector2.down, false, true);
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x - 0.5f, y + 0.5f)));
                }
                else if (topMap == Map.InnerWall && leftMap == Map.Seen)
                {
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x - 0.5f, y + 0.5f)));
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x - 0.5f, y - 0.5f)));
                }
                else if (topMap == Map.InnerWall && rightMap == Map.Seen)
                {
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x + 0.5f, y + 0.5f)));
                    blocks.Add(this.mapCoordinatesToBlock(new Vector2(x + 0.5f, y - 0.5f)));
                }
            }
        }
        else if (countUnknown == 0)
        {
            if (leftMap == Map.InnerWall && downMap == Map.InnerWall)
            {
                blocks.Add(this.mapCoordinatesToBlock(new Vector2(x + 0.5f, y - 0.5f)));
                blocks.Add(this.mapCoordinatesToBlock(new Vector2(x - 0.5f, y + 0.5f)));
            }
            else if (leftMap == Map.InnerWall && topMap == Map.InnerWall)
            {
                blocks.Add(this.mapCoordinatesToBlock(new Vector2(x + 0.5f, y + 0.5f)));
                blocks.Add(this.mapCoordinatesToBlock(new Vector2(x - 0.5f, y - 0.5f)));
            }
            else if (rightMap == Map.InnerWall && topMap == Map.InnerWall)
            {
                blocks.Add(this.mapCoordinatesToBlock(new Vector2(x - 0.5f, y + 0.5f)));
                blocks.Add(this.mapCoordinatesToBlock(new Vector2(x + 0.5f, y - 0.5f)));
            }
            else if (rightMap == Map.InnerWall && downMap == Map.InnerWall)
            {
                blocks.Add(this.mapCoordinatesToBlock(new Vector2(x - 0.5f, y - 0.5f)));
                blocks.Add(this.mapCoordinatesToBlock(new Vector2(x + 0.5f, y + 0.5f)));
            }
        }
        else if (countUnknown == 3)
        {
            Vector2 newLocation;
            if (leftMap == Map.InnerWall)
            {
                newLocation = cornerLocation + Vector2.left;
            }
            else if (rightMap == Map.InnerWall)
            {
                newLocation = cornerLocation + Vector2.right;
            }
            else if (topMap == Map.InnerWall)
            {
                newLocation = cornerLocation + Vector2.up;
            }
            else
            {
                newLocation = cornerLocation + Vector2.down;
            }

            Vector2 nodeLocation = this.closestNode(newLocation);
            float node = Mathf.Round(this.mapNode(nodeLocation.y, nodeLocation.x));
            List<Vector2> edges = this.edgesForNode(node);

            Vector2 top = edges[0];
            Vector2 right = edges[1];
            Vector2 left = edges[2];
            Vector2 down = edges[3];

            topMap = this.edges[Mathf.RoundToInt(top.x), Mathf.RoundToInt(top.y)];
            rightMap = this.edges[Mathf.RoundToInt(right.x), Mathf.RoundToInt(right.y)];
            leftMap = this.edges[Mathf.RoundToInt(left.x), Mathf.RoundToInt(left.y)];
            downMap = this.edges[Mathf.RoundToInt(down.x), Mathf.RoundToInt(down.y)];

            return this.openBlocks(newLocation, topMap, leftMap, rightMap, downMap);
        }
        else if (countUnknown == 2)
        {
            if (leftMap == Map.InnerWall && downMap == Map.InnerWall)
            {
                blocks.Add(this.mapCoordinatesToBlock(new Vector2(x - 0.5f, y - 0.5f)));
            }
            else if (leftMap == Map.InnerWall && topMap == Map.InnerWall)
            {
                blocks.Add(this.mapCoordinatesToBlock(new Vector2(x - 0.5f, y + 0.5f)));
            }
            else if (rightMap == Map.InnerWall && topMap == Map.InnerWall)
            {
                blocks.Add(this.mapCoordinatesToBlock(new Vector2(x + 0.5f, y + 0.5f)));
            }
            else if (rightMap == Map.InnerWall && downMap == Map.InnerWall)
            {
                blocks.Add(this.mapCoordinatesToBlock(new Vector2(x + 0.5f, y - 0.5f)));
            }

        }

        List<int> res = new List<int>();
        StringBuilder sb = new StringBuilder();
        sb.Append("Blocks: ");
        foreach (int i in blocks)
        {
            sb.Append($"{i},");
            res.Add(i);
        }
        Debug.Log(sb.ToString());

        return res;
    }

    private List<Vector2> edgesForNode(float node)
    {
        List<Vector2> edges = new List<Vector2>();
        int intNode = Mathf.RoundToInt(node);
        // up
        if (!(intNode >= 0 && intNode <= cols))
        {
            edges.Add(new Vector2(node - (cols + 1), node));
        }
        else
        {
            edges.Add(Vector2.zero);
        }
        //right 
        if (!(EnumerableUtility.Range(cols, (cols + 1) * (rows + 1) + 1, cols + 1).Contains(intNode)))
        {
            edges.Add(new Vector2(node, node + 1));
        }
        else
        {
            edges.Add(Vector2.zero);
        }
        //left
        if (!(EnumerableUtility.Range(0, (cols + 1) * (rows + 1) - cols + 1, cols + 1).Contains(intNode)))
        {
            edges.Add(new Vector2(node - 1, node));
        }
        else
        {
            edges.Add(Vector2.zero);
        }
        //down
        if (!(intNode >= (cols + 1) * (rows + 1) - cols && intNode <= (rows + 1) * (cols + 1)))
        {
            edges.Add(new Vector2(node, node + (cols + 1)));
        }
        else
        {
            edges.Add(Vector2.zero);
        }
        return edges;
    }

    private Vector2 closestNode(Vector2 location)
    {
        float xLocation = location.x + originPoint.x;
        float yLocation = -location.y + originPoint.y;
        return new Vector2(xLocation, yLocation);
    }

    private void scanWallStart()
    {
        int playerMask = ~(1 << 8);

        float xDir = (float)Math.Cos(this.lookingDirection * Mathf.Deg2Rad);
        float yDir = (float)Math.Sin(this.lookingDirection * Mathf.Deg2Rad);
        Vector3 dir = new Vector3(xDir, yDir, 0);
        RaycastHit2D hit = Physics2D.Raycast(this.transform.position, dir, Mathf.Infinity, playerMask);
        this.originalPoint = hit.point;

        float xDirLeft = (float)Math.Cos((this.lookingDirection + CORNERUPDATENEGATIVE) * Mathf.Deg2Rad);
        float yDirLeft = (float)Math.Sin((this.lookingDirection + CORNERUPDATENEGATIVE) * Mathf.Deg2Rad);
        Vector3 left = new Vector3(xDirLeft, yDirLeft, 0);

        float xDirRight = (float)Math.Cos((this.lookingDirection + CORNERUPDATEPOSITIVE) * Mathf.Deg2Rad);
        float yDirRight = (float)Math.Sin((this.lookingDirection + CORNERUPDATEPOSITIVE) * Mathf.Deg2Rad);
        Vector3 right = new Vector3(xDirRight, yDirRight, 0);

        RaycastHit2D leftHit = Physics2D.Raycast(this.transform.position, left, Mathf.Infinity, playerMask);
        RaycastHit2D rightHit = Physics2D.Raycast(this.transform.position, right, Mathf.Infinity, playerMask);

        string tagLeft = leftHit.collider.tag;
        string tagRight = rightHit.collider.tag;

        RaycastHit2D choice;

        if (tagLeft.Equals("Inner") && tagRight.Equals("Inner"))
        {
            this.cornerUpdate = (leftHit.distance < rightHit.distance) ? CORNERUPDATENEGATIVE : CORNERUPDATEPOSITIVE;
            choice = (leftHit.distance < rightHit.distance) ? leftHit : rightHit;
        }
        else if (tagLeft.Equals("Inner"))
        {
            this.cornerUpdate = CORNERUPDATENEGATIVE;
            choice = leftHit;
        }
        else if (tagRight.Equals("Inner"))
        {
            this.cornerUpdate = CORNERUPDATEPOSITIVE;
            choice = rightHit;
        }
        else
        {
            choice = leftHit;
            Debug.Break();
            Application.Quit();
        }

        this.originalDirectionBeforeWallScan = this.lookingDirection;

        this.lastCornerDistance = choice.distance;
        this.currCornerDistance = choice.distance;

        this.lastCornerSearch = choice.point;
        this.currCornerSearch = choice.point;

        this.wentOtherDirection = false;

        this.otherCorner = 200 * Vector2.one;

        this.horizontalWall = Mathf.Abs(this.originalPoint.y - choice.point.y) < Mathf.Pow(10, -3);
        // Debug.Log($"Horizontal Wall? {horizontalWall}");

        this.setCorner = false;
    }

    private bool leftWall(Vector2 point)
    {
        if (this.horizontalWall)
        {
            return Mathf.Abs(point.y - this.originalPoint.y) > Mathf.Pow(10, -3);
        }
        else
        {
            return Mathf.Abs(point.x - this.originalPoint.x) > Mathf.Pow(10, -3);
        }
    }

    private void scanWall()
    {
        int playerMask = ~(1 << 8);

        this.lookingDirection += this.cornerUpdate;

        this.transform.Rotate(new Vector3(0, 0, this.cornerUpdate));

        float xDir = (float)Math.Cos(this.lookingDirection * Mathf.Deg2Rad);
        float yDir = (float)Math.Sin(this.lookingDirection * Mathf.Deg2Rad);

        Vector3 dir = new Vector3(xDir, yDir, 0);

        RaycastHit2D hit = Physics2D.Raycast(this.transform.position, dir, Mathf.Infinity, playerMask);
        this.markEdges(this.transform.position, hit.point, dir);

        this.lastCornerSearch = this.currCornerSearch;
        this.currCornerSearch = hit.point;

        this.lastCornerDistance = this.currCornerDistance;
        this.currCornerDistance = hit.distance;

        if (hit.collider.tag.Equals("Hider"))
        {
            float rotationAmount = Vector2.SignedAngle(hit.collider.transform.position - this.transform.position, new Vector2((float)Math.Cos(this.lookingDirection * Mathf.Deg2Rad), (float)Math.Sin(this.lookingDirection * Mathf.Deg2Rad)));
            this.lookingDirection -= rotationAmount;
            this.transform.Rotate(new Vector3(0, 0, -rotationAmount));

            this.transform.position = this.transform.position + this.movement();

            this.currMode = Mode.ReadyToScan;

            return;
        }

        if (hit.collider.tag.Equals("Outer") || Mathf.Abs(this.currCornerDistance - this.lastCornerDistance) > 0.5f || this.leftWall(this.currCornerSearch) || this.isOnSeenWall(currCornerSearch))
        {
            Vector2 coordinates = new Vector2(Mathf.Round(this.lastCornerSearch.x), Mathf.Round(this.lastCornerSearch.y));
            Debug.Log($"Coordinates of a corner: {coordinates}");
            // Debug.Log(this.lastCornerSearch);

            // Debug.Log($"Contains coordinates {coordinates}? {this.cornersSeen.Contains(coordinates)}");

            if (!this.cornersSeen.Contains(coordinates) && this.isOnInnerWall(coordinates) && !this.isOnSeenWall(coordinates))
            {
                if (!this.setCorner)
                {
                    this.cornerLocation = coordinates;
                    this.setCorner = true;
                }
                this.cornersSeen.Add(coordinates);

                float rotationAmount = (float)(this.originalDirectionBeforeWallScan - this.lookingDirection);
                this.lookingDirection = this.originalDirectionBeforeWallScan;
                this.transform.Rotate(new Vector3(0, 0, rotationAmount));

                this.lastCornerSearch = this.originalPoint;
                this.lastCornerDistance = Vector2.Distance(this.originalPoint, this.transform.position);
                this.currCornerSearch = this.originalPoint;
                this.currCornerDistance = Vector2.Distance(this.originalPoint, this.transform.position);
            }
            else
            {
                if (this.wentOtherDirection)
                {
                    float rotationAmount = (float)(this.originalDirectionBeforeWallScan - this.lookingDirection);
                    this.lookingDirection = this.originalDirectionBeforeWallScan;
                    this.transform.Rotate(new Vector3(0, 0, rotationAmount));

                    this.currMode = Mode.Scan;
                }
                else
                {
                    float rotationAmount = (float)(this.originalDirectionBeforeWallScan - this.lookingDirection);
                    this.lookingDirection = this.originalDirectionBeforeWallScan;
                    this.transform.Rotate(new Vector3(0, 0, rotationAmount));

                    this.lastCornerSearch = this.originalPoint;
                    this.lastCornerDistance = Vector2.Distance(this.originalPoint, this.transform.position);
                    this.currCornerSearch = this.originalPoint;
                    this.currCornerDistance = Vector2.Distance(this.originalPoint, this.transform.position);
                }
            }

            if (this.otherCorner == 200 * Vector2.one)
            {
                // Debug.Log("Changing other corner!");
                this.otherCorner = coordinates;
                this.wentOtherDirection = true;
                this.cornerUpdate = (Mathf.Abs(this.cornerUpdate - CORNERUPDATEPOSITIVE) < Mathf.Pow(10, -3)) ? CORNERUPDATENEGATIVE : CORNERUPDATEPOSITIVE;
            }
            else
            {
                // Debug.Log($"Wall: ({this.otherCorner}, {coordinates})");
                this.addWall((this.otherCorner, coordinates));
                // Debug.Log("Walls:");
                // foreach ((Vector2, Vector2) wall in this.walls)
                // {
                //     Debug.Log(wall);
                // }
                this.otherCorner = 200 * Vector2.one;

                float rotationAmount = (float)(this.originalDirectionBeforeWallScan - this.lookingDirection);
                this.lookingDirection = this.originalDirectionBeforeWallScan;
                this.transform.Rotate(new Vector3(0, 0, rotationAmount));

                this.currMode = Mode.FindCorner;
                return;
            }
        }
    }

    private void addWall((Vector2, Vector2) newWall)
    {
        for (int i = this.walls.Count - 1; i >= 0; i--)
        {
            if (this.wallIntersect(newWall, this.walls[i]))
            {
                (Vector2, Vector2) currWall = this.walls[i];
                this.walls[i] = this.mergeWalls(currWall, newWall);
                return;
            }
        }
        this.walls.Add(newWall);
    }

    private bool wallIntersect((Vector2, Vector2) wallOne, (Vector2, Vector2) wallTwo)
    {
        bool wallOneHorizontal = Mathf.Abs(wallOne.Item1.y - wallOne.Item2.y) < Mathf.Pow(10, -3);
        bool wallTwoHorizontal = Mathf.Abs(wallTwo.Item1.y - wallTwo.Item2.y) < Mathf.Pow(10, -3);
        if (wallOneHorizontal != wallTwoHorizontal)
        {
            return false;
        }

        if (wallOneHorizontal)
        {
            if (Mathf.Abs(wallOne.Item1.y - wallTwo.Item1.y) >= Mathf.Pow(10, -3))
            {
                return false;
            }
            float minXOne = Mathf.Min(wallOne.Item1.x, wallOne.Item2.x);
            float maxXOne = Mathf.Max(wallOne.Item1.x, wallOne.Item2.x);

            float minXTwo = Mathf.Min(wallTwo.Item1.x, wallTwo.Item2.x);
            float maxXTwo = Mathf.Max(wallTwo.Item1.x, wallTwo.Item2.x);
            return (!(minXTwo > maxXOne || minXOne > maxXTwo));
        }
        else
        {
            if (Mathf.Abs(wallOne.Item1.x - wallTwo.Item1.x) >= Mathf.Pow(10, -3))
            {
                return false;
            }
            float minYOne = Mathf.Min(wallOne.Item1.y, wallOne.Item2.y);
            float maxYOne = Mathf.Max(wallOne.Item1.y, wallOne.Item2.y);

            float minYTwo = Mathf.Min(wallTwo.Item1.y, wallTwo.Item2.y);
            float maxYTwo = Mathf.Max(wallTwo.Item1.y, wallTwo.Item2.y);

            return (!(minYTwo > maxYOne || minYOne > maxYTwo));
        }
    }

    private (Vector2, Vector2) mergeWalls((Vector2, Vector2) wallOne, (Vector2, Vector2) wallTwo)
    {
        // we assume that the walls are aligned correctly
        bool horizontal = Mathf.Abs(wallOne.Item1.y - wallOne.Item2.y) < Mathf.Pow(10, -3);
        if (horizontal)
        {
            float[] x = new float[] { wallOne.Item1.x, wallOne.Item2.x, wallTwo.Item1.x, wallTwo.Item2.x };

            float minX = x.Min();
            float maxX = x.Max();

            float y = Mathf.Round(wallOne.Item1.y);
            Vector2 one = new Vector2(minX, y);
            Vector2 two = new Vector2(maxX, y);
            return (one, two);
        }
        else
        {
            float[] y = new float[] { wallOne.Item1.y, wallOne.Item2.y, wallTwo.Item1.y, wallTwo.Item2.y };

            float minY = y.Min();
            float maxY = y.Max();

            float x = Mathf.Round(wallOne.Item1.x);
            Vector2 one = new Vector2(x, minY);
            Vector2 two = new Vector2(x, maxY);
            return (one, two);
        }
    }

    private bool isOnLine(Vector2 point)
    {
        return (Mathf.Abs(point.x - this.originalPoint.x) < Mathf.Pow(10, -3)) || (Mathf.Abs(point.y - this.originalPoint.y) < Mathf.Pow(10, -3));
    }

    private void readyToMove()
    {
        if (this.path.Count == 0)
        {
            int playerMask = ~(1 << 8);

            ArrayList options = new ArrayList();
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (canMove(this.transform.position, new Vector3((float)x, (float)y, 0), playerMask))
                    {
                        options.Add((x, y));
                    }
                }
            }

            if (options.Count == 0)
            {
                Debug.Break();
                Application.Quit();
            }

            (int, int) tup = ((int, int))options[this.random.Next(0, options.Count)];

            float xDiff = (float)tup.Item1;
            float yDiff = (float)tup.Item2;
            Vector3 dir = new Vector3(xDiff, yDiff, 0);

            float xDir = (float)Math.Cos(this.lookingDirection * Mathf.Deg2Rad);
            float yDir = (float)Math.Sin(this.lookingDirection * Mathf.Deg2Rad);
            Vector3 angle = new Vector3(xDir, yDir, 0);

            this.rotationAmountBeforeMove = Vector2.SignedAngle(angle, dir);
            this.moveRotation = (this.rotationAmountBeforeMove > 0) ? 45 : -45;
            this.currMoveRotation = 0f;
        }
        else
        {
            Vector2 location = this.mapBlockToCoordinates(this.path.Peek()) - new Vector2(this.transform.position.x, this.transform.position.y);

            float xDir = (float)Math.Cos(this.lookingDirection * Mathf.Deg2Rad);
            float yDir = (float)Math.Sin(this.lookingDirection * Mathf.Deg2Rad);
            Vector3 angle = new Vector3(xDir, yDir, 0);

            this.rotationAmountBeforeMove = Vector2.SignedAngle(angle, location);

            // Debug.Log($"{this.lookingDirection} {angle} {location} {this.rotationAmountBeforeMove}");
            this.moveRotation = (this.rotationAmountBeforeMove > 0) ? 45 : -45;
            this.currMoveRotation = 0f;
        }
    }

    private bool canMove(Vector3 currPosition, Vector3 dir, int mask)
    {
        RaycastHit2D hit = Physics2D.Raycast(currPosition, dir, dir.magnitude, mask);
        bool bothZero = Vector3.Distance(dir, Vector3.zero) < Mathf.Pow(10, -3);
        bool hasMoved;
        Vector2 newPos = new Vector2(currPosition.x, currPosition.y) + new Vector2(dir.x, dir.y);
        bool res = this.locationMap.TryGetValue(this.mapCoordinatesToBlock(newPos), out hasMoved);
        if (res)
        {
            return (hit.collider == null && !bothZero && !hasMoved);
        }
        return false;
    }

    private void move()
    {
        this.markInMyDirection();
        if (this.path.Count == 0)
        {
            this.transform.position = this.transform.position + this.movement();
        }
        else
        {
            int nextBlock = this.path.Pop();
            Vector2 location = this.mapBlockToCoordinates(nextBlock);
            if (this.removedBlocks.Contains(nextBlock))
            {
                Debug.Log("Editing path ...");
                this.removeBlockFromGraph(nextBlock);
                this.currMode = Mode.FindCorner;
                return;
            }


            this.transform.position = new Vector3(location.x, location.y, 0);
        }
    }

    private void markInMyDirection()
    {
        int playerMask = ~(1 << 8);
        Vector2 dir = new Vector2(Mathf.Cos((float)this.lookingDirection * Mathf.Deg2Rad), Mathf.Sin((float)this.lookingDirection * Mathf.Deg2Rad));
        RaycastHit2D hit = Physics2D.Raycast(this.transform.position, dir, Mathf.Infinity, playerMask);
        this.markEdges(this.transform.position, hit.point, dir);
    }

    private void rotateToMove()
    {
        if (Mathf.Round(Mathf.Abs(this.currMoveRotation - this.rotationAmountBeforeMove)) < Math.Pow(10, -3))
        {
            this.markInMyDirection();
            this.currMode = Mode.Move;
            this.moveRotation = 0;
            this.currMoveRotation = 0;
            this.rotationAmountBeforeMove = 0;
            return;
        }

        this.markInMyDirection();

        this.lookingDirection += this.moveRotation;
        this.transform.Rotate(new Vector3(0, 0, this.moveRotation));
        this.currMoveRotation += this.moveRotation;

        this.markInMyDirection();
    }

    private Vector3 movement()
    {
        float xDir = (float)Math.Cos(this.lookingDirection * Mathf.Deg2Rad);
        float yDir = (float)Math.Sin(this.lookingDirection * Mathf.Deg2Rad);

        float xMove = (Mathf.Abs(xDir) < Mathf.Pow(10, -3) ? 0 : ((xDir > 0) ? 1 : -1));
        float yMove = (Mathf.Abs(yDir) < Mathf.Pow(10, -3) ? 0 : ((yDir > 0) ? 1 : -1));
        return new Vector3(xMove, yMove, 0);
    }

    private bool markEdges(Vector2 position, Vector2 hit, Vector2 direction)
    {
        bool res = false;

        ArrayList points = this.generatePointsOnPath(position, hit, direction);

        // Debug.Log("Points:");
        // foreach (Vector2 vec in points)
        // {
        //     Debug.Log($"{vec} {this.mapPointToEdge(vec)}");
        // }

        if (points.Count == 0)
        {
            return res;
        }

        int i = 0;

        Vector2 point0 = (Vector2)points[0];

        Vector2 first = this.mapPointToEdge(point0);
        int firstX = Mathf.RoundToInt(first.x);
        int firstY = Mathf.RoundToInt(first.y);

        if (this.isOnInnerWall(point0))
        {
            // Debug.Log("Wall!");
            // first one is a wall!
            if (firstX == firstY)
            {
                // oh boy
                Vector2 vec = new Vector2(firstX, firstY);

                this.classifyEdge(vec, Vector2.one, direction, true, true);
            }
            else
            {
                this.classifyEdge(first, Vector2.one, direction, false, true);
            }
            i = 1;
            res = true;
        }

        if (firstX == firstY)
        {
            // we got a diagonal
            int size = points.Count - 1;
            for (i = 0; i < size; i++)
            {
                Vector2 mapOne = this.mapPointToEdge((Vector2)points[i]);
                Vector2 mapTwo = this.mapPointToEdge((Vector2)points[i + 1]);
                this.classifyEdge(mapOne, mapTwo, direction, true, false);
            }
        }
        else
        {
            int size = points.Count;
            for (; i < size; i++)
            {
                Vector2 vec = (Vector2)points[i];
                // Debug.Log(vec);
                Vector2 mapped = this.mapPointToEdge(vec);
                // Debug.Log(mapped);
                this.classifyEdge(mapped, Vector2.one, direction, false, false);
            }
        }

        return res;
    }

    private bool isOnInnerWall(Vector2 point)
    {
        float negativeY = rows - this.originPoint.y;
        float positiveY = this.originPoint.y;

        float positiveX = cols - this.originPoint.x;
        float negativeX = this.originPoint.x;
        bool yOnOuterWall = (Mathf.Abs(point.y - positiveY) < Mathf.Pow(10, -3)) || (Mathf.Abs(point.y + negativeY) < Mathf.Pow(10, -3));
        bool xOnOuterWall = (Mathf.Abs(point.x - positiveX) < Mathf.Pow(10, -3)) || (Mathf.Abs(point.x + negativeX) < Mathf.Pow(10, -3));

        return !xOnOuterWall && !yOnOuterWall;
    }

    private ArrayList generatePointsOnPath(Vector2 position, Vector2 hit, Vector2 direction)
    {
        // Debug.Log($"({position.x}, {position.y}) -> ({hit.x}, {hit.y}), {direction}");
        ArrayList points = new ArrayList();
        if (Mathf.Abs(direction.y) < Mathf.Pow(10, -3))
        {
            if (direction.x > 0)
            {
                float currX = hit.x;
                float y = hit.y;
                while (currX > position.x)
                {
                    points.Add(new Vector2(currX, y));
                    currX -= 1;
                }
            }
            else
            {
                float currX = hit.x;
                float y = hit.y;
                while (currX < position.x)
                {
                    points.Add(new Vector2(currX, y));
                    currX += 1;
                }
            }
        }
        else if (Mathf.Abs(direction.x) < Mathf.Pow(10, -3))
        {
            if (direction.y > 0)
            {
                float currY = hit.y;
                float x = hit.x;
                while (currY > position.y)
                {
                    points.Add(new Vector2(x, currY));
                    currY -= 1;
                }
            }
            else
            {
                float currY = hit.y;
                float x = hit.x;
                while (currY < position.y)
                {
                    points.Add(new Vector2(x, currY));
                    currY += 1;
                }
            }
        }
        else
        {
            // find the equation for the line
            float slope = (hit.y - position.y) / (hit.x - position.x);
            float intercept = ((hit.y - slope * hit.x) + (position.y - slope * position.x)) / 2;

            float epsilon = Mathf.Pow(10, -3);

            float xStart = -this.originPoint.x;
            float xEnd = cols - this.originPoint.x;

            float yStart = -(rows - this.originPoint.y);
            float yEnd = this.originPoint.y;
            for (float x = xStart; x <= xEnd; x++)
            {
                float currY = slope * x + intercept;
                if (currY > yEnd || currY < yStart)
                {
                    continue;
                }
                Vector2 point = new Vector2(x, currY);
                if (pointIsOnLineSegment(position, hit, point))
                {
                    bool add = true;

                    int n = points.Count;
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 curr = (Vector2)points[i];
                        if (Mathf.Abs(curr.x - point.x) < epsilon && Mathf.Abs(curr.y - point.y) < epsilon)
                        {
                            add = false;
                            break;
                        }
                    }

                    if (add)
                    {
                        points.Add(point);
                    }
                }
            }
            for (float y = yStart; y <= yEnd; y++)
            {
                float currX = (y - intercept) / slope;
                if (currX < xStart || currX > xEnd)
                {
                    continue;
                }
                Vector2 point = new Vector2(currX, y);
                if (pointIsOnLineSegment(position, hit, point))
                {
                    bool add = true;

                    int n = points.Count;
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 curr = (Vector2)points[i];
                        if (Mathf.Abs(curr.x - point.x) < epsilon && Mathf.Abs(curr.y - point.y) < epsilon)
                        {
                            add = false;
                            break;
                        }
                    }

                    if (add)
                    {
                        points.Add(point);
                    }
                }
            }

            this.sortPoints(points, direction);
        }
        return points;
    }

    private void sortPoints(ArrayList points, Vector2 direction)
    {
        // not actually too complex since all y's have distinct x's
        if (direction.y > 0)
        {
            // going up sort in decreasing order by y position
            int n = points.Count;
            for (int i = 0; i < n - 1; i++)
            {
                int maxIdx = i;
                for (int j = i + 1; j < n; j++)
                {
                    if (((Vector2)points[j]).y > ((Vector2)points[maxIdx]).y)
                    {
                        maxIdx = j;
                    }
                }

                Vector2 temp = (Vector2)points[maxIdx];
                points[maxIdx] = points[i];
                points[i] = temp;
            }
        }
        else
        {
            int n = points.Count;
            for (int i = 0; i < n - 1; i++)
            {
                int minIdx = i;
                for (int j = i + 1; j < n; j++)
                {
                    if (((Vector2)points[j]).y < ((Vector2)points[minIdx]).y)
                    {
                        minIdx = j;
                    }
                }

                Vector2 temp = (Vector2)points[minIdx];
                points[minIdx] = points[i];
                points[i] = temp;
            }
        }
    }

    private bool pointIsOnLineSegment(Vector2 x1y1, Vector2 x2y2, Vector2 point)
    {
        float epsilon = Mathf.Pow(10, -3);

        float xMin = Mathf.Min(x1y1.x, x2y2.x) - epsilon;
        float xMax = Mathf.Max(x1y1.x, x2y2.x) + epsilon;

        float yMin = Mathf.Min(x1y1.y, x2y2.y) - epsilon;
        float yMax = Mathf.Max(x1y1.y, x2y2.y) + epsilon;

        return (point.x >= xMin && point.x <= xMax && point.y >= yMin && point.y <= yMax);
    }

    private (int, int, bool) mapEdgeToBlocks(int nodeOne, int nodeTwo)
    {
        int minNode = Mathf.RoundToInt(Mathf.Min(nodeOne, nodeTwo));
        int maxNode = Mathf.RoundToInt(Mathf.Max(nodeOne, nodeTwo));

        // see if horizontal or vertical
        bool horizontal = (maxNode - minNode) == (cols + 1);

        if (horizontal)
        {
            int colBarrier = minNode % (cols + 1);
            int row = (maxNode / (cols + 1)) - 1;

            return ((row * cols + (colBarrier - 1)), row * cols + colBarrier, horizontal);
        }
        else
        {
            int row = (minNode / (cols + 1)) - 1;
            int col = (maxNode % (cols + 1)) - 1;
            return ((row * cols + col), (row * cols + col + cols), horizontal);
        }
    }

    private void classifyEdge(Vector2 edgeOne, Vector2 edgeTwo, Vector2 dir, bool diag, bool isWall)
    {
        if (isWall)
        {
            int x = Mathf.RoundToInt(edgeOne.x);
            int y = Mathf.RoundToInt(edgeOne.y);


            float epsilon = 45f + Mathf.Pow(10, -3);
            if (diag)
            {
                int playerMask = ~(1 << 8);

                Map curr = this.edges[x, y];

                float node = Mathf.Round(x);
                List<Vector2> edges = this.edgesForNode(node);

                Vector2 top = edges[0];
                Vector2 right = edges[1];
                Vector2 left = edges[2];
                Vector2 down = edges[3];

                Map topMap = this.edges[Mathf.RoundToInt(top.x), Mathf.RoundToInt(top.y)];
                Map rightMap = this.edges[Mathf.RoundToInt(right.x), Mathf.RoundToInt(right.y)];
                Map leftMap = this.edges[Mathf.RoundToInt(left.x), Mathf.RoundToInt(left.y)];
                Map downMap = this.edges[Mathf.RoundToInt(down.x), Mathf.RoundToInt(down.y)];

                if (topMap == Map.InnerWall && downMap == Map.Unknown)
                {
                    float xDir = (float)Math.Cos((this.lookingDirection + CORNERUPDATEPOSITIVE) * Mathf.Deg2Rad);
                    float yDir = (float)Math.Sin((this.lookingDirection + CORNERUPDATEPOSITIVE) * Mathf.Deg2Rad);
                    Vector3 direction = new Vector3(xDir, yDir, 0);

                    RaycastHit2D hit = Physics2D.Raycast(this.transform.position, direction, Mathf.Infinity, playerMask);

                    this.markEdges(this.transform.position, hit.point, direction);
                }
                if (leftMap == Map.InnerWall && rightMap == Map.Unknown)
                {
                    float xDir = (float)Math.Cos((this.lookingDirection + CORNERUPDATENEGATIVE) * Mathf.Deg2Rad);
                    float yDir = (float)Math.Sin((this.lookingDirection + CORNERUPDATENEGATIVE) * Mathf.Deg2Rad);
                    Vector3 direction = new Vector3(xDir, yDir, 0);

                    RaycastHit2D hit = Physics2D.Raycast(this.transform.position, direction, Mathf.Infinity, playerMask);

                    this.markEdges(this.transform.position, hit.point, direction);
                }
                if (rightMap == Map.InnerWall && leftMap == Map.Unknown)
                {
                    float xDir = (float)Math.Cos((this.lookingDirection + CORNERUPDATEPOSITIVE) * Mathf.Deg2Rad);
                    float yDir = (float)Math.Sin((this.lookingDirection + CORNERUPDATEPOSITIVE) * Mathf.Deg2Rad);
                    Vector3 direction = new Vector3(xDir, yDir, 0);

                    RaycastHit2D hit = Physics2D.Raycast(this.transform.position, direction, Mathf.Infinity, playerMask);

                    this.markEdges(this.transform.position, hit.point, direction);
                }
                if (downMap == Map.InnerWall && topMap == Map.Unknown)
                {
                    float xDir = (float)Math.Cos((this.lookingDirection + CORNERUPDATENEGATIVE) * Mathf.Deg2Rad);
                    float yDir = (float)Math.Sin((this.lookingDirection + CORNERUPDATENEGATIVE) * Mathf.Deg2Rad);
                    Vector3 direction = new Vector3(xDir, yDir, 0);

                    RaycastHit2D hit = Physics2D.Raycast(this.transform.position, direction, Mathf.Infinity, playerMask);

                    this.markEdges(this.transform.position, hit.point, direction);
                }

                foreach (Vector2 vec in new Vector2[] { top, right, left, down })
                {
                    if (this.edges[Mathf.RoundToInt(vec.x), Mathf.RoundToInt(vec.y)] == Map.OuterWall)
                    {
                        this.drawEdge(this.mapEdge(vec.x, vec.y), Color.red);
                    }
                }
            }
            else
            {
                this.drawEdge(this.mapEdge(x, y), Color.red);
                // Debug.Log($"X: {x}, Y: {y}");
                this.edges[x, y] = Map.InnerWall;
                this.edges[y, x] = Map.InnerWall;

                (int nodeOne, int nodeTwo, bool horizontal) = this.mapEdgeToBlocks(x, y);

                if (horizontal)
                {
                    if (Vector2.Angle(dir, Vector2.right) < Vector2.Angle(dir, Vector2.left))
                    {
                        this.removeBlockFromGraph(nodeTwo);
                    }
                    else
                    {
                        this.removeBlockFromGraph(nodeOne);
                    }
                }
                else
                {
                    if (Vector2.Angle(dir, Vector2.up) < Vector2.Angle(dir, Vector2.down))
                    {
                        this.removeBlockFromGraph(nodeOne);
                    }
                    else
                    {
                        this.removeBlockFromGraph(nodeTwo);
                    }
                }
            }
            return;
        }

        if (diag)
        {
            ArrayList edges = this.edgesOnDiag(edgeOne.x, edgeTwo.x);
            foreach (Vector2 vec in edges)
            {
                int x = Mathf.RoundToInt(vec.x);
                int y = Mathf.RoundToInt(vec.y);

                Map curr = this.edges[x, y];

                switch (curr)
                {
                    case Map.Blank:
                    case Map.InnerWall:
                    case Map.MaybeWall:
                    case Map.Seen:
                        break;
                    case Map.OuterWall:
                        this.drawEdge(this.mapEdge(x, y), Color.red);
                        break;
                    case Map.Unknown:
                        (Vector2 one, Vector2 two) = this.mapEdge(vec.x, vec.y);
                        if (Mathf.Abs(one.y - two.y) < Mathf.Pow(10, -3))
                        {
                            // on the same horizontal line
                            float xCurr = (one.x + two.x) / 2;
                            float yCurr = (one.y + two.y) / 2;
                            Vector3 faceDirection = new Vector3(xCurr, yCurr, 0) - this.transform.position;

                            int playerMask = ~(1 << 8);
                            RaycastHit2D hit = Physics2D.Raycast(this.transform.position, faceDirection, Mathf.Infinity, playerMask);
                            this.markEdges(this.transform.position, hit.point, faceDirection);
                        }
                        // this.drawEdge(this.mapEdge(x, y), Color.green);
                        // this.edges[x, y] = Map.Seen;
                        // this.edges[y, x] = Map.Seen;
                        break;
                    default:
                        break;
                }
            }
        }
        else
        {
            int nodeOne = Mathf.RoundToInt(edgeOne.x);
            int nodeTwo = Mathf.RoundToInt(edgeOne.y);

            Map curr = this.edges[nodeOne, nodeTwo];

            switch (curr)
            {
                case Map.Blank:
                case Map.InnerWall:
                    break;
                case Map.OuterWall:
                    this.drawEdge(this.mapEdge(nodeOne, nodeTwo), Color.red);
                    break;
                case Map.MaybeWall:
                case Map.Unknown:
                    this.edges[nodeOne, nodeTwo] = Map.Seen;
                    this.edges[nodeTwo, nodeOne] = Map.Seen;
                    this.drawEdge(this.mapEdge(nodeOne, nodeTwo), Color.green);
                    break;
                case Map.Seen:
                    break;
                default:
                    break;
            }
        }
    }

    private void removeBlockFromGraph(int block)
    {
        if (this.removedBlocks.Contains(block))
        {
            return;
        }
        // Debug.Log($"Removing {block}");
        this.removedBlocks.Add(block);
        if (!(EnumerableUtility.Range(0, rows * cols - cols + 1, cols).Contains(block)))
        {
            // W
            this.blockGraph[block, block - 1] = false;
            this.blockGraph[block - 1, block] = false;

            if (block >= cols)
            {
                // NW
                this.blockGraph[block, block - (cols + 1)] = false;
                this.blockGraph[block - (cols + 1), block] = false;
            }

            if (block < rows * (cols - 1))
            {
                // SW
                this.blockGraph[block, block + (cols - 1)] = false;
                this.blockGraph[block + (cols - 1), block] = false;
            }
        }

        if (!(block >= 0 && block <= cols - 1))
        {
            // N
            this.blockGraph[block, block - cols] = false;
            this.blockGraph[block - cols, block] = false;
        }

        if (!(block >= rows * cols - cols && block <= rows * cols - 1))
        {
            // S
            this.blockGraph[block, block + cols] = false;
            this.blockGraph[block + cols, block] = false;
        }

        if (!EnumerableUtility.Range(cols - 1, cols * rows, cols).Contains(block))
        {
            // E
            this.blockGraph[block, block + 1] = false;
            this.blockGraph[block + 1, block] = false;

            if (block >= cols)
            {
                // NE
                this.blockGraph[block, block - (cols - 1)] = false;
                this.blockGraph[block - (cols - 1), block] = false;
            }

            if (block < rows * (cols - 1))
            {
                // SE
                this.blockGraph[block, block + (cols + 1)] = false;
                this.blockGraph[block + (cols + 1), block] = false;
            }
        }
    }

    private ArrayList edgesOnDiag(float nodeOne, float nodeTwo)
    {
        ArrayList res = new ArrayList();
        int lowNode = Mathf.RoundToInt(Mathf.Min(nodeOne, nodeTwo));
        int highNode = Mathf.RoundToInt(Mathf.Max(nodeOne, nodeTwo));
        if (Mathf.Abs(highNode - lowNode - (cols + 2)) < Mathf.Pow(10, -3))
        {
            // NW => SE
            if (lowNode + (cols + 1) <= (rows + 1) * (cols + 1))
            {
                res.Add(new Vector2(lowNode, lowNode + (cols + 1)));
            }
            if (lowNode + 1 <= (rows + 1) * (cols + 1))
            {
                res.Add(new Vector2(lowNode, lowNode + 1));
            }
            if (highNode - (cols + 1) >= 0)
            {
                res.Add(new Vector2(highNode - (cols + 1), highNode));
            }
            if (highNode - 1 >= 0)
            {
                res.Add(new Vector2(highNode - 1, highNode));
            }
        }
        else
        {
            // NE => SW
            if (lowNode + (cols + 1) <= (rows + 1) * (cols + 1))
            {
                res.Add(new Vector2(lowNode, lowNode + (cols + 1)));
            }
            if (lowNode - 1 >= 0)
            {
                res.Add(new Vector2(lowNode - 1, lowNode));
            }
            if (highNode - (cols + 1) >= 0)
            {
                res.Add(new Vector2(highNode - (cols + 1), highNode));
            }
            if (highNode + 1 <= (rows + 1) * (cols + 1))
            {
                res.Add(new Vector2(highNode, highNode + 1));
            }
        }
        return res;
    }

    private Vector2 mapPointToEdge(Vector2 point)
    {
        bool xInt = Mathf.Abs(point.x - Mathf.Round(point.x)) < Mathf.Pow(10, -3);
        bool yInt = Mathf.Abs(point.y - Mathf.Round(point.y)) < Mathf.Pow(10, -3);

        if (xInt && yInt)
        {
            // Debug.Log("Both zero");
            // we have a corner situation here
            float col = point.x + this.originPoint.x;
            float row = -point.y + this.originPoint.y;
            float node = mapNode(row, col);
            return new Vector2(node, node);
        }
        else if (xInt)
        {
            // Debug.Log("X Zero");
            float col = point.x + this.originPoint.x;
            float lowRow = Mathf.Min(-Mathf.Floor(point.y) + this.originPoint.y, -Mathf.Ceil(point.y) + this.originPoint.y);
            float highRow = Mathf.Max(-Mathf.Floor(point.y) + this.originPoint.y, -Mathf.Ceil(point.y) + this.originPoint.y);
            float lowNode = this.mapNode(lowRow, col);
            float highNode = this.mapNode(highRow, col);
            return new Vector2(lowNode, highNode);
        }
        else if (yInt)
        {
            // Debug.Log("Y Zero");
            float row = -point.y + this.originPoint.y;
            float lowCol = Mathf.Min(Mathf.Floor(point.x) + this.originPoint.x, Mathf.Ceil(point.x) + this.originPoint.x);
            float highCol = Mathf.Max(Mathf.Floor(point.x) + this.originPoint.x, Mathf.Ceil(point.x) + this.originPoint.x);
            float lowNode = this.mapNode(row, lowCol);
            float highNode = this.mapNode(row, highCol);
            return new Vector2(lowNode, highNode);
        }
        else
        {
            // edge case
            return Vector2.zero;
        }
    }

    private float mapNode(float row, float col)
    {
        return row * (cols + 1) + col;
    }

    private (Vector2, Vector2) mapEdge(float nodeOne, float nodeTwo)
    {
        float rowOne = 0, colOne = 0, rowTwo = 0, colTwo = 0;
        for (float r = 0; r <= rows; r++)
        {
            for (float c = 0; c <= cols; c++)
            {
                if (Mathf.Approximately(nodeOne, r * (cols + 1) + c))
                {
                    rowOne = r;
                    colOne = c;
                }
                if (Mathf.Approximately(nodeTwo, r * (cols + 1) + c))
                {
                    rowTwo = r;
                    colTwo = c;
                }
            }
        }
        rowOne = -1 * (rowOne - this.originPoint.y);
        rowTwo = -1 * (rowTwo - this.originPoint.y);
        colOne = colOne - this.originPoint.x;
        colTwo = colTwo - this.originPoint.x;
        if (Mathf.Abs(rowOne - rowTwo) > 1 || Mathf.Abs(colOne - colTwo) > 1)
        {
            return (new Vector2(0, 0), new Vector2(0, 0));
        }
        return (new Vector2(colOne, rowOne), new Vector2(colTwo, rowTwo));
    }

    private void drawEdge((Vector2, Vector2) points, Color color)
    {
        (Vector2 one, Vector2 two) = points;
        Vector3 start = new Vector3(one.x, one.y, 100);
        Vector3 end = new Vector3(two.x, two.y, 100);

        bool startX0 = Mathf.Abs(start.x) < Mathf.Pow(10, -3);
        bool startY0 = Mathf.Abs(start.y) < Mathf.Pow(10, -3);
        bool endX0 = Mathf.Abs(end.x) < Mathf.Pow(10, -3);
        bool endY0 = Mathf.Abs(end.y) < Mathf.Pow(10, -3);
        if (startX0 && startY0 && endX0 && endY0)
        {
            return;
        }
        Debug.DrawLine(start, end, color, 1000f);
    }

    private Vector2 mapBlockToCoordinates(int blockNumber)
    {
        int row = blockNumber / cols;
        int col = blockNumber % cols;
        return new Vector2(-(this.originPoint.x) + col + 0.5f, this.originPoint.y - row - 0.5f);
    }

    private int mapCoordinatesToBlock(Vector2 coord)
    {
        float x = coord.x;
        float y = coord.y;
        int col = Mathf.RoundToInt(x + this.originPoint.x - 0.5f);
        int row = Mathf.RoundToInt(-y + this.originPoint.y - 0.5f);
        return row * cols + col;
    }
}
