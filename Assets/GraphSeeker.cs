using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GraphSeeker : MonoBehaviour
{
    private enum Mode
    {
        Plan,
        Move,
        Failed
    }

    public LineRenderer line;
    public float timeStep = 1f;
    private bool[,] blockGraph;
    public GameObject hider;
    private int goalBlock;
    private Stack path = new Stack();
    private Mode currMode = Mode.Plan;
    private float timer = 0f;
    private Collider2D innerWallCollider;

    // Start is called before the first frame update
    void Start()
    {
        this.blockGraph = new bool[25, 25];
        for (int i = 0; i <= 24; i++)
        {
            if (!(i == 4 || i == 9 || i == 14 || i == 19 || i == 24))
            {
                blockGraph[i, i + 1] = true;
                blockGraph[i + 1, i] = true;

                if (i < 20)
                {
                    blockGraph[i, i + 6] = true;
                    blockGraph[i + 6, i] = true;
                }

                if (i > 4)
                {
                    blockGraph[i, i - 4] = true;
                    blockGraph[i - 4, i] = true;
                }
            }
            if (i < 20)
            {
                blockGraph[i, i + 5] = true;
                blockGraph[i + 5, i] = true;
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

        bool[] visited = new bool[25];
        for (int i = 0; i < visited.Length; i++)
        {
            visited[i] = false;
        }

        int[] distances = new int[25];
        for (int i = 0; i < distances.Length; i++)
        {
            distances[i] = int.MaxValue;
        }
        distances[currBlock] = 0;

        int[] previous = new int[25];
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
        if (!(block == 0 || block == 5 || block == 10 || block == 15 || block == 20))
        {
            // W
            this.blockGraph[block, block - 1] = false;
            this.blockGraph[block - 1, block] = false;

            // NW
            this.blockGraph[block, block - 6] = false;
            this.blockGraph[block - 6, block] = false;

            // SW
            this.blockGraph[block, block + 4] = false;
            this.blockGraph[block + 4, block] = false;
        }

        if (!(block >= 0 && block <= 4))
        {
            // N
            this.blockGraph[block, block - 5] = false;
            this.blockGraph[block - 5, block] = false;
        }

        if (!(block >= 20 && block <= 24))
        {
            // S
            this.blockGraph[block, block + 5] = false;
            this.blockGraph[block + 5, block] = false;
        }

        if (!(block == 4 || block == 9 || block == 14 || block == 19 || block == 24))
        {
            // E
            this.blockGraph[block, block + 1] = false;
            this.blockGraph[block + 1, block] = false;

            // NE
            this.blockGraph[block, block - 4] = false;
            this.blockGraph[block - 4, block] = false;

            // SE
            this.blockGraph[block, block + 6] = false;
            this.blockGraph[block + 6, block] = false;
        }
    }

    private Vector2 mapBlockToCoordinates(int blockNumber)
    {
        int row = blockNumber / 5;
        int col = blockNumber % 5;
        return new Vector2(-4f + col + 0.5f, 3 - row - 0.5f);
    }

    private int mapCoordinatesToBlock(Vector2 coord)
    {
        float x = coord.x;
        float y = coord.y;
        int col = Mathf.RoundToInt(x + 4f - 0.5f);
        int row = Mathf.RoundToInt(-y + 3f - 0.5f);
        return row * 5 + col;
    }
}
