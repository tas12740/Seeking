using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class GraphSeeker : MonoBehaviour
{
    private enum Mode
    {
        Plan,
        Move,
        Failed
    }

    public int rows = 10;
    public int cols = 10;
    public Vector2 originPoint = new Vector2(4f, 8f);

    public LineRenderer line;
    public float timeStep = 1f;
    private bool[,] blockGraph;
    public GameObject hider;
    private int goalBlock;
    private Stack path = new Stack();
    private Mode currMode = Mode.Plan;
    private float timer = 0f;
    private Collider2D innerWallCollider;
    private HashSet<int> removedBlocks = new HashSet<int>();

    // Start is called before the first frame update
    void Start()
    {
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

        this.goalBlock = this.mapCoordinatesToBlock(this.hider.transform.position);

        this.innerWallCollider = GameObject.FindGameObjectsWithTag("Inner")[0].GetComponent<Collider2D>() as Collider2D;
    }

    // Update is called once per frame
    void Update()
    {
        this.timer += Time.deltaTime;
        if (this.timer > this.timeStep)
        {
            doUpdate();
            this.timer = 0f;
        }
    }

    private void doUpdate()
    {
        if (this.currMode == Mode.Plan)
        {
            this.generatePath(this.mapCoordinatesToBlock(this.transform.position), this.goalBlock);
            this.currMode = Mode.Move;
        }
        else if (this.currMode == Mode.Move)
        {
            if (this.path.Count == 0)
            {
                this.currMode = Mode.Failed;
                return;
            }
            int nextBlock = (int)this.path.Pop();
            Vector2 nextLocation = this.mapBlockToCoordinates(nextBlock);

            int playerMask = ~(1 << 8);

            Vector2 currPosition = this.transform.position;
            Vector2 direction = nextLocation - currPosition;

            RaycastHit2D testCanMove = Physics2D.Raycast(this.transform.position, direction, direction.magnitude, playerMask);

            if (!this.isPointWithinCollider(this.innerWallCollider, nextLocation))
            {
                this.transform.position = new Vector3(nextLocation.x, nextLocation.y, 0);
            }
            else
            {
                Debug.Log($"Removing block {nextBlock}");
                this.removeBlockFromGraph(nextBlock);
                this.currMode = Mode.Plan;
            }
        }
        else if (this.currMode == Mode.Failed)
        {
            Debug.Log("No path to hider!");
            Debug.Break();
        }
    }

    private bool isPointWithinCollider(Collider2D collider, Vector2 point)
    {
        return (collider.ClosestPoint(point) - point).sqrMagnitude < Mathf.Epsilon * Mathf.Epsilon;
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
