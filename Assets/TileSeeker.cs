using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

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
        Scan,
        ReadyToMove,
        RotateToMove,
        Move,
        ScanWall,
        MoveToCorner
    }
    private Mode currMode;

    private bool seesHider;

    private float moveRotation;
    private float currMoveRotation;
    private float rotationAmountBeforeMove;

    private bool foundCorner = false;
    private Vector2 cornerLocation = Vector2.zero;
    private bool wentOtherDirection = false;
    private float cornerUpdate = 0f;

    private double lookingDirection = 0;
    private float currRotation = 0;
    private bool didRotate = false;
    public LineRenderer line;
    private Map[,] edges;
    public float timeStep = 1f;
    private float timer = 0f;
    private System.Random random;
    private Dictionary<int, bool> locationMap;

    // Start is called before the first frame update
    void Start()
    {
        this.random = new System.Random();

        this.currMode = Mode.Scan;
        this.seesHider = false;

        line.positionCount = 2;
        line.material.color = Color.black;

        edges = new Map[36, 36];

        for (int r = 0; r < edges.GetLength(0); r++)
        {
            for (int c = 0; c < edges.GetLength(1); c++)
            {
                edges[r, c] = Map.Blank;
                edges[c, r] = Map.Blank;
            }
        }

        for (int i = 0; i < 35; i++)
        {
            edges[i, i + 1] = Map.Unknown;
            edges[i + 1, i] = Map.Unknown;
            if (i + 6 < 36)
            {
                edges[i, i + 6] = Map.Unknown;
                edges[i + 6, i] = Map.Unknown;
            }
        }

        // generate the outer walls
        for (int i = 0; i <= 4; i++)
        {
            edges[i, i + 1] = Map.OuterWall;
            edges[i + 1, i] = Map.OuterWall;
        }
        for (int i = 5; i <= 29; i += 6)
        {
            edges[i, i + 6] = Map.OuterWall;
            edges[i + 6, i] = Map.OuterWall;
        }
        for (int i = 0; i <= 24; i += 6)
        {
            edges[i, i + 6] = Map.OuterWall;
            edges[i + 6, i] = Map.OuterWall;
        }
        for (int i = 30; i < 35; i++)
        {
            edges[i, i + 1] = Map.OuterWall;
            edges[i + 1, i] = Map.OuterWall;
        }

        this.locationMap = new Dictionary<int, bool>();
        for (int i = 0; i < 25; i++)
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
        if (this.timer > this.timeStep)
        {
            doUpdate();
            this.timer = 0;
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
            this.currMode = Mode.Scan;
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

                bool seesInnerWall = markEdges(new Vector2(this.transform.position.x, this.transform.position.y), hit.point, new Vector2(dir.x, dir.y));
                if (seesInnerWall)
                {
                    this.currMode = Mode.ScanWall;
                }
            }
        }
        else if (this.currMode == Mode.ScanWall)
        {
            int playerMask = ~(1 << 8);

            float xDirLeft = (float)Math.Cos((this.lookingDirection - 0.1f) * Mathf.Deg2Rad);
            float yDirLeft = (float)Math.Sin((this.lookingDirection - 0.1f) * Mathf.Deg2Rad);
            Vector3 left = new Vector3(xDirLeft, yDirLeft, 0);

            float xDirRight = (float)Math.Cos((this.lookingDirection + 0.1f) * Mathf.Deg2Rad);
            float yDirRight = (float)Math.Sin((this.lookingDirection + 0.1f) * Mathf.Deg2Rad);
            Vector3 right = new Vector3(xDirRight, yDirRight, 0);

            RaycastHit2D leftHit = Physics2D.Raycast(this.transform.position, left, Mathf.Infinity, playerMask);
            RaycastHit2D rightHit = Physics2D.Raycast(this.transform.position, right, Mathf.Infinity, playerMask);

            string tagLeft = leftHit.collider.tag;
            string tagRight = rightHit.collider.tag;

            RaycastHit2D choice;

            if (tagLeft.Equals("Inner") && tagRight.Equals("Inner"))
            {
                this.cornerUpdate = (leftHit.distance < rightHit.distance) ? -0.1f : 0.1f;
                choice = (leftHit.distance < rightHit.distance) ? leftHit : rightHit;
            }
            else if (tagLeft.Equals("Inner"))
            {
                this.cornerUpdate = -0.1f;
                choice = leftHit;
            }
            else if (tagRight.Equals("Inner"))
            {
                this.cornerUpdate = 0.1f;
                choice = rightHit;
            }
            else
            {
                choice = leftHit;
                Debug.Break();
                Application.Quit();
            }

            double originalDirection = this.lookingDirection;
            double currDirection = this.lookingDirection;
            bool hasCorner = false;
            float lastDistance = choice.distance, currDistance = choice.distance;
            Vector2 last = choice.point, curr = choice.point;

            while (!hasCorner)
            {
                currDirection += this.cornerUpdate;
                this.transform.Rotate(new Vector3(0, 0, this.cornerUpdate));

                float xDir = (float)Math.Cos(currDirection * Mathf.Deg2Rad);
                float yDir = (float)Math.Sin(currDirection * Mathf.Deg2Rad);

                Vector3 dir = new Vector3(xDir, yDir, 0);

                RaycastHit2D hit = Physics2D.Raycast(this.transform.position, dir, Mathf.Infinity, playerMask);
                this.markEdges(this.transform.position, hit.point, dir);
                last = curr;
                curr = hit.point;

                lastDistance = currDistance;
                currDistance = hit.distance;
                if (hit.collider.tag.Equals("Outer"))
                {
                    Debug.Log($"{currDistance}, {lastDistance}");
                    if (Mathf.Abs(currDistance - lastDistance) > 1f)
                    {
                        hasCorner = true;
                        this.cornerLocation = last;
                        Debug.Log(this.cornerLocation);
                        Debug.Break();
                    }
                    else
                    {
                        if (this.wentOtherDirection)
                        {
                            this.transform.Rotate(new Vector3(0, 0, (float)(originalDirection - currDirection)));
                            Debug.Break();
                            return;
                        }
                        this.cornerUpdate = (Mathf.Abs(this.cornerUpdate - 0.1f) < Mathf.Pow(10, -3)) ? -0.1f : 0.1f;
                        this.wentOtherDirection = true;
                    }
                }
            }
        }
    }

    private void readyToMove()
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
        this.transform.position = this.transform.position + this.movement();
    }

    private void rotateToMove()
    {
        if (Mathf.Round(Mathf.Abs(this.currMoveRotation - this.rotationAmountBeforeMove)) < Math.Pow(10, -3))
        {
            this.currMode = Mode.Move;
            this.moveRotation = 0;
            this.currMoveRotation = 0;
            this.rotationAmountBeforeMove = 0;
            return;
        }
        this.lookingDirection += this.moveRotation;
        this.transform.Rotate(new Vector3(0, 0, this.moveRotation));
        this.currMoveRotation += this.moveRotation;
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
                ArrayList choices = new ArrayList();
                if (first.x + 6 <= 35)
                {
                    choices.Add(new Vector2(first.x, first.x + 6));
                }
                if (first.x + 1 <= 35)
                {
                    choices.Add(new Vector2(first.x, first.x + 1));
                }
                if (first.x - 1 >= 0)
                {
                    choices.Add(new Vector2(first.x - 1, first.x));
                }
                if (first.x - 6 >= 0)
                {
                    choices.Add(new Vector2(first.x - 6, first.x));
                }

                foreach (Vector2 vec in choices)
                {
                    this.classifyEdge(vec, Vector2.one, true, true);
                }
            }
            else
            {
                this.classifyEdge(first, Vector2.one, false, true);
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
                this.classifyEdge(mapOne, mapTwo, true, false);
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
                this.classifyEdge(mapped, Vector2.one, false, false);
            }
        }

        return res;
    }

    private bool isOnInnerWall(Vector2 point)
    {
        bool yOnOuterWall = (Mathf.Abs(point.y - 3f) < Mathf.Pow(10, -3)) || (Mathf.Abs(point.y + 2f) < Mathf.Pow(10, -3));
        bool xOnOuterWall = (Mathf.Abs(point.x - 1f) < Mathf.Pow(10, -3)) || (Mathf.Abs(point.x + 4f) < Mathf.Pow(10, -3));

        return !xOnOuterWall && !yOnOuterWall;
    }

    private ArrayList generatePointsOnPath(Vector2 position, Vector2 hit, Vector2 direction)
    {
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
            float currX = hit.x;
            float currY = hit.y;
            // we are on a corner
            if (direction.x > 0 && direction.y < 0)
            {
                while (currX > position.x && currY < position.y)
                {
                    points.Add(new Vector2(currX, currY));
                    currX -= 0.5f;
                    currY += 0.5f;
                }
            }
            else if (direction.x > 0 && direction.y > 0)
            {
                while (currX > position.x && currY > position.y)
                {
                    points.Add(new Vector2(currX, currY));
                    currX -= 0.5f;
                    currY -= 0.5f;
                }
            }
            else if (direction.x < 0 && currY < position.y)
            {
                while (currX < position.x && currY < position.y)
                {
                    points.Add(new Vector2(currX, currY));
                    currX += 0.5f;
                    currY += 0.5f;
                }
            }
            else
            {
                while (currX < position.x && currY > position.y)
                {
                    points.Add(new Vector2(currX, currY));
                    currX += 0.5f;
                    currY -= 0.5f;
                }
            }
        }
        return points;
    }

    private void classifyEdge(Vector2 edgeOne, Vector2 edgeTwo, bool diag, bool isWall)
    {
        if (isWall)
        {
            int x = Mathf.RoundToInt(edgeOne.x);
            int y = Mathf.RoundToInt(edgeOne.y);
            if (diag)
            {
                Map curr = this.edges[x, y];

                switch (curr)
                {
                    case Map.Blank:
                    case Map.InnerWall:
                        break;
                    case Map.OuterWall:
                        this.drawEdge(this.mapEdge(x, y), Color.red);
                        break;
                    case Map.MaybeWall:
                        // this.edges[x, y] = Map.InnerWall;
                        // this.edges[y, x] = Map.InnerWall;
                        // this.drawEdge(this.mapEdge(x, y), Color.red);
                        break;
                    case Map.Unknown:
                        // this.edges[x, y] = Map.MaybeWall;
                        // this.edges[y, x] = Map.MaybeWall;
                        // this.drawEdge(this.mapEdge(x, y), Color.yellow);
                        break;
                    case Map.Seen:
                        break;
                    default:
                        break;
                }
            }
            else
            {
                this.drawEdge(this.mapEdge(x, y), Color.red);
                this.edges[x, y] = Map.InnerWall;
                this.edges[y, x] = Map.InnerWall;
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
                        this.drawEdge(this.mapEdge(x, y), Color.green);
                        this.edges[x, y] = Map.Seen;
                        this.edges[y, x] = Map.Seen;
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

    private ArrayList edgesOnDiag(float nodeOne, float nodeTwo)
    {
        ArrayList res = new ArrayList();
        int lowNode = Mathf.RoundToInt(Mathf.Min(nodeOne, nodeTwo));
        int highNode = Mathf.RoundToInt(Mathf.Max(nodeOne, nodeTwo));
        if (Mathf.Abs(highNode - lowNode - 7f) < Mathf.Pow(10, -3))
        {
            // NW => SE
            if (lowNode + 6 <= 35)
            {
                res.Add(new Vector2(lowNode, lowNode + 6));
            }
            if (lowNode + 1 <= 35)
            {
                res.Add(new Vector2(lowNode, lowNode + 1));
            }
            if (highNode - 6 >= 0)
            {
                res.Add(new Vector2(highNode - 6, highNode));
            }
            if (highNode - 1 >= 0)
            {
                res.Add(new Vector2(highNode - 1, highNode));
            }
        }
        else
        {
            // NE => SW
            // difference is 5
            if (lowNode + 6 <= 35)
            {
                res.Add(new Vector2(lowNode, lowNode + 6));
            }
            if (lowNode - 1 >= 0)
            {
                res.Add(new Vector2(lowNode - 1, lowNode));
            }
            if (highNode - 6 >= 0)
            {
                res.Add(new Vector2(highNode - 6, highNode));
            }
            if (highNode + 1 <= 35)
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

        // Debug.Log($"({point.x}, {Mathf.Round(point.x)}, {xInt}), ({point.y}, {Mathf.Round(point.y)}, {yInt})");

        if (xInt && yInt)
        {
            // Debug.Log("Both zero");
            // we have a corner situation here
            float col = point.x + 4;
            float row = -point.y + 3;
            float node = mapNode(row, col);
            return new Vector2(node, node);
        }
        else if (xInt)
        {
            // Debug.Log("X Zero");
            float col = point.x + 4;
            float lowRow = Mathf.Min(-Mathf.Floor(point.y) + 3, -Mathf.Ceil(point.y) + 3);
            float highRow = Mathf.Max(-Mathf.Floor(point.y) + 3, -Mathf.Ceil(point.y) + 3);
            float lowNode = this.mapNode(lowRow, col);
            float highNode = this.mapNode(highRow, col);
            return new Vector2(lowNode, highNode);
        }
        else if (yInt)
        {
            // Debug.Log("Y Zero");
            float row = -point.y + 3;
            float lowCol = Mathf.Min(Mathf.Floor(point.x) + 4, Mathf.Ceil(point.x) + 4);
            float highCol = Mathf.Max(Mathf.Floor(point.x) + 4, Mathf.Ceil(point.x) + 4);
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
        return row * 6 + col;
    }

    private (Vector2, Vector2) mapEdge(float nodeOne, float nodeTwo)
    {
        float rowOne = 0, colOne = 0, rowTwo = 0, colTwo = 0;
        for (float r = 0; r <= 5; r++)
        {
            for (float c = 0; c <= 5; c++)
            {
                if (Mathf.Approximately(nodeOne, r * 6 + c))
                {
                    rowOne = r;
                    colOne = c;
                }
                if (Mathf.Approximately(nodeTwo, r * 6 + c))
                {
                    rowTwo = r;
                    colTwo = c;
                }
            }
        }
        rowOne = -1 * (rowOne - 3);
        rowTwo = -1 * (rowTwo - 3);
        colOne = colOne - 4;
        colTwo = colTwo - 4;
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
