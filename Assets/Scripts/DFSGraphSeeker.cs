using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Text;
using System.Diagnostics;

public class DFSGraphSeeker : MonoBehaviour
{
    private enum Mode
    {
        Scan,
        Plan,
        Move,
        Failed,
        FoundHider
    }
    private enum Direction
    {
        N,
        NE,
        E,
        SE,
        S,
        SW,
        W,
        NW
    }

    public int rows = 10;
    public int cols = 10;
    public Vector2 originPoint = new Vector2(4f, 8f);

    public float timeStep = 1f;
    private bool[,] blockGraph;
    private int nextBlock = -1;
    private Mode currMode = Mode.Scan;
    private float timer = 0f;
    private Collider2D innerWallCollider;
    private Collider2D hiderCollider;
    private HashSet<int> removedBlocks = new HashSet<int>();
    private HashSet<int> exploredBlocks = new HashSet<int>();

    private Stack<int> blocksToExplore = new Stack<int>();
    private Stack<int> path = new Stack<int>();

    public bool debug;
    private bool running;
    public GameObject experimentObject;
    private Experiment experiment;

    private Stopwatch watch;

    // Start is called before the first frame update
    void Start()
    {
        this.experiment = this.experimentObject.GetComponent<Experiment>();
        this.innerWallCollider = GameObject.FindGameObjectsWithTag("Inner")[0].GetComponent<Collider2D>() as Collider2D;
        this.hiderCollider = GameObject.FindGameObjectsWithTag("Hider")[0].GetComponent<Collider2D>() as Collider2D;

        Reset();
    }

    public void Reset()
    {
        this.removedBlocks = new HashSet<int>();
        this.exploredBlocks = new HashSet<int>();
        this.blocksToExplore = new Stack<int>();

        this.currMode = Mode.Scan;

        int finalBlock = rows * cols - 1;
        // UnityEngine.Debug.Log($"Final block: {finalBlock}");

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
    }

    // Update is called once per frame
    void Update()
    {
        if (!this.running)
        {
            return;
        }

        if (this.watch.ElapsedMilliseconds > 1000 * 60 * 1)
        {
            // if game has been running for more than a minute, it's probably stuck!
            this.currMode = Mode.Failed;
        }

        Vector2 position = this.transform.position;
        float newX = Mathf.Round(position.x * 2) / 2;
        float newY = Mathf.Round(position.y * 2) / 2;

        float xAbs = Mathf.Abs(position.x - newX);
        float yAbs = Mathf.Abs(position.y - newY);
        if (xAbs >= Mathf.Pow(10, -3) || yAbs >= Mathf.Pow(10, -3))
        {
            // UnityEngine.Debug.Log($"({position.x}, {xSub}, {xAbs}) - ({position.y}, {ySub}, {yAbs})");
            this.transform.position = new Vector3(newX, newY, 0);
        }

        float negativeY = this.originPoint.y - rows;
        float positiveY = this.originPoint.y;
        float positiveX = cols - this.originPoint.x;
        float negativeX = -this.originPoint.x;

        position = this.transform.position;
        newY = position.y;
        newX = position.x;
        if (position.y < negativeY)
        {
            newY = negativeY + 0.5f;
        }
        if (position.y > positiveY)
        {
            newY = positiveY - 0.5f;
        }
        if (position.x < negativeX)
        {
            newX = negativeX + 0.5f;
        }
        if (position.x > positiveX)
        {
            newX = positiveX - 0.5f;
        }
        this.transform.position = new Vector3(newX, newY, 0);

        if (this.isPointWithinHiderCollider(this.transform.position))
        {
            this.currMode = Mode.FoundHider;
        }

        if (!debug)
        {
            doUpdate();
        }
        else
        {
            this.timer += Time.deltaTime;
            if (this.timer > this.timeStep)
            {
                doUpdate();
                this.timer = 0f;
            }
        }
    }

    private void doUpdate()
    {
        this.exploredBlocks.Add(this.mapCoordinatesToBlock(this.transform.position));
        // UnityEngine.Debug.Log($"Current mode: {this.currMode}");
        if (this.currMode == Mode.Scan)
        {
            this.scan();
            this.currMode = Mode.Plan;
        }
        else if (this.currMode == Mode.Plan)
        {
            this.getNextMove();
        }
        else if (this.currMode == Mode.Move)
        {
            if (this.path.Count == 0)
            {
                if (this.nextBlock == this.mapCoordinatesToBlock(this.transform.position))
                {
                    this.currMode = Mode.Scan;
                    this.nextBlock = -1;
                    return;
                }
                else if (this.blocksToExplore.Count == 0)
                {
                    this.currMode = Mode.Failed;
                    return;
                }
                this.nextBlock = this.blocksToExplore.Pop();
                this.generatePath(this.mapCoordinatesToBlock(this.transform.position), this.nextBlock);
                return;
            }
            int nextBlockLocation = this.path.Pop();
            Vector2 nextLocation = this.mapBlockToCoordinates(nextBlockLocation);

            if (!this.isPointWithinInnerWallCollider(nextLocation))
            {
                this.transform.position = new Vector3(nextLocation.x, nextLocation.y, 0);
            }
            else
            {
                this.removeBlockFromGraph(nextBlockLocation);
                this.generatePath(this.mapCoordinatesToBlock(this.transform.position), this.nextBlock);
            }
        }
        else if (this.currMode == Mode.Failed)
        {
            // UnityEngine.Debug.Log("No path to hider!");
            // UnityEngine.Debug.Break();
            done(false);
        }
        else if (this.currMode == Mode.FoundHider)
        {
            done(true);
        }
    }

    public void Run()
    {
        this.running = true;
        this.Reset();
        this.watch = Stopwatch.StartNew();
    }

    public void Stop()
    {
        this.running = false;
        this.watch.Stop();
    }

    private void done(bool found)
    {
        this.Stop();
        this.experiment.NotifyDone(found, this.watch.ElapsedMilliseconds);
    }

    private void scan()
    {
        foreach (Vector3 dir in allDirections())
        {
            int playerMask = ~(1 << 8);
            RaycastHit2D hit = Physics2D.Raycast(this.transform.position, dir, Mathf.Infinity, playerMask);

            if (hit.collider.gameObject.tag.Equals("Hider"))
            {
                Vector2 position = hit.collider.gameObject.transform.position;
                this.nextBlock = this.mapCoordinatesToBlock(position);
                return;
            }
        }
        this.nextBlock = -1;
    }

    private List<Vector3> allDirections()
    {
        List<Vector3> res = new List<Vector3>();
        foreach (int i in EnumerableUtility.Range(-1, 2))
        {
            foreach (int j in EnumerableUtility.Range(-1, 2))
            {
                if (!(i == 0 && j == 0))
                {
                    res.Add(new Vector3(i, j, 0));
                }
            }
        }
        return res;
    }

    private bool isPointWithinHiderCollider(Vector2 point)
    {
        return this.isPointWithinCollider(this.hiderCollider, point);
    }

    private bool isPointWithinInnerWallCollider(Vector2 point)
    {
        return this.isPointWithinCollider(this.innerWallCollider, point);
    }

    private bool isPointWithinCollider(Collider2D collider, Vector2 point)
    {
        return (collider.ClosestPoint(point) - point).sqrMagnitude < Mathf.Epsilon * Mathf.Epsilon;
    }

    private int getBlockInDirection(int block, Direction dir)
    {
        switch (dir)
        {
            case Direction.N:
                if (block >= cols)
                {
                    return block - cols;
                }
                break;
            case Direction.NW:
                if (block >= cols && !EnumerableUtility.Range(0, rows * (cols - 1) + 1, cols).Contains(block))
                {
                    return block - (cols + 1);
                }
                break;
            case Direction.W:
                if (!EnumerableUtility.Range(0, rows * (cols - 1) + 1, cols).Contains(block))
                {
                    return block - 1;
                }
                break;
            case Direction.SW:
                if (!EnumerableUtility.Range(0, rows * (cols - 1) + 1, cols).Contains(block) && block < rows * (cols - 1))
                {
                    return block + (cols - 1);
                }
                break;
            case Direction.S:
                if (block < rows * (cols - 1))
                {
                    return block + cols;
                }
                break;
            case Direction.SE:
                if (block < rows * (cols - 1) && !EnumerableUtility.Range(cols - 1, cols * rows, cols).Contains(block))
                {
                    return block + (cols + 1);
                }
                break;
            case Direction.E:
                if (!EnumerableUtility.Range(cols - 1, cols * rows, cols).Contains(block))
                {
                    return block + 1;
                }
                break;
            case Direction.NE:
                if (!EnumerableUtility.Range(cols - 1, cols * rows, cols).Contains(block) && block >= cols)
                {
                    return block - (cols - 1);
                }
                break;
            default:
                break;
        }
        return -1;
    }

    private void generatePath(int currBlock, int endBlock)
    {
        this.path.Clear();

        Queue<int> searchQueue = new Queue<int>();

        int arrSize = rows * cols;
        bool[] visited = new bool[arrSize];
        for (int i = 0; i < visited.Length; i++)
        {
            visited[i] = false;
        }

        int[] distances = new int[arrSize];
        for (int i = 0; i < distances.Length; i++)
        {
            distances[i] = int.MaxValue;
        }
        distances[currBlock] = 0;

        int[] previous = new int[arrSize];
        for (int i = 0; i < previous.Length; i++)
        {
            previous[i] = int.MaxValue;
        }

        searchQueue.Enqueue(currBlock);
        while (searchQueue.Count != 0)
        {
            int curr = searchQueue.Dequeue();
            // UnityEngine.Debug.Log($"Searching: {curr}");
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

    private Direction[] allDirectionEnums()
    {
        Direction[] dirs = new Direction[] { Direction.N, Direction.NE, Direction.E, Direction.SE, Direction.S, Direction.SW, Direction.W, Direction.NW };
        System.Random rand = new System.Random();
        return dirs.OrderBy(x => rand.Next()).ToArray();
    }

    private void getNextMove()
    {
        int currBlock = this.mapCoordinatesToBlock(this.transform.position);
        // UnityEngine.Debug.Log($"Current block: {currBlock}");
        if (this.nextBlock == -1)
        {
            // StringBuilder sb = new StringBuilder();
            // foreach (int i in this.blocksToExplore)
            // {
            //     sb.Append($"{i} ->");
            // }
            // UnityEngine.Debug.Log(sb.ToString());

            foreach (Direction dir in this.allDirectionEnums())
            {
                int blockInDirection = this.getBlockInDirection(currBlock, dir);
                if (blockInDirection != -1 && this.isPointWithinInnerWallCollider(this.mapBlockToCoordinates(blockInDirection)))
                {
                    this.removeBlockFromGraph(blockInDirection);
                    continue;
                }
                if (blockInDirection != -1 && !this.removedBlocks.Contains(blockInDirection) && !this.exploredBlocks.Contains(blockInDirection) && !this.blocksToExplore.Contains(blockInDirection))
                {
                    // UnityEngine.Debug.Log($"Adding {nextBlock}");
                    this.blocksToExplore.Push(blockInDirection);
                }
            }
            if (this.blocksToExplore.Count == 0)
            {
                this.currMode = Mode.Failed;
                return;
            }
            this.nextBlock = this.blocksToExplore.Pop();
            this.generatePath(currBlock, this.nextBlock);
            this.currMode = Mode.Move;
        }
        else
        {
            this.generatePath(currBlock, this.nextBlock);
            this.currMode = Mode.Move;
        }
    }

    private void removeBlockFromGraph(int block)
    {
        if (this.removedBlocks.Contains(block))
        {
            return;
        }
        // UnityEngine.Debug.Log($"Removing {block}");
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
