using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using System.Text;

public class Experiment : MonoBehaviour
{
    public Text statusText;
    public Text experimentNumberText;

    private enum ExperimentType
    {
        Tile,
        DFS,
        BFS,
        Done
    }
    private ExperimentType currExperimentType;

    public enum MapType
    {
        Random,
        Algorithm
    }
    public MapType mapType = MapType.Algorithm;

    public int NUM_EXPERIMENTS_TO_RUN = 100;
    private int currExperimentRun = 0;

    public int MAX_EXPERIMENTS = 100;
    private int currExperimentCount = 0;

    private List<bool> dfsResults = new List<bool>();
    private List<float> dfsTimes = new List<float>();
    private List<bool> bfsResults = new List<bool>();
    private List<float> bfsTimes = new List<float>();
    private List<bool> tileResults = new List<bool>();
    private List<float> tileTimes = new List<float>();

    public Tilemap innerWallTilemap;
    public TileBase wallTile;

    public GameObject seeker;
    public GameObject hider;

    private DFSGraphSeeker dfsSeeker;
    private BFSGraphSeeker bfsSeeker;
    private TileSeeker tileSeeker;

    private (float, float) seekerPosition;
    private (float, float) hiderPosition;

    private (int, int)[] fourCorners = new (int, int)[] {
        (-4, 4), (-3, 4), (-4, 1), (-3, 1),
        (-1, -2), (-1, -1), (-1, 0), (-1, 1),
        (-1, 7), (-1, 6), (-1, 5), (-1, 4),
        (0, 1), (1, 1),
        (0, 4), (1, 4),
        (2, -2), (2, -1), (2, 0), (2, 1),
        (2, 7), (2, 6), (2, 5), (2, 4),
        (5, 1), (4, 1),
        (5, 4), (4, 4)
    };
    private (float, float) fourCornersHiderPos = (5.5f, 5.5f);
    private (float, float) fourCornersSeekerPos = (-3.5f, 7.5f);

    private Stack<List<(int, int)>> preLoadedMaps = new Stack<List<(int, int)>>();
    private Stack<(float, float)> preLoadedHiders = new Stack<(float, float)>();
    private Stack<(float, float)> preLoadedSeekers = new Stack<(float, float)>();
    private List<(int, int)> tileSpace = new List<(int, int)>();
    private List<(int, int)> currWalls = new List<(int, int)>();

    // Start is called before the first frame update
    void Start()
    {
        for (int x = -4; x <= 5; x++)
        {
            for (int y = -2; y <= 7; y++)
            {
                this.tileSpace.Add((x, y));
            }
        }

        this.dfsSeeker = seeker.GetComponent<DFSGraphSeeker>() as DFSGraphSeeker;
        this.bfsSeeker = seeker.GetComponent<BFSGraphSeeker>() as BFSGraphSeeker;
        this.tileSeeker = seeker.GetComponent<TileSeeker>() as TileSeeker;

        this.preLoadedMaps.Push(this.fourCorners.ToList());
        this.preLoadedHiders.Push(this.fourCornersHiderPos);
        this.preLoadedSeekers.Push(this.fourCornersSeekerPos);

        this.SetupExperiment();
    }

    private void ResetMap()
    {
        this.currWalls = new List<(int, int)>();
        foreach ((int, int) tup in this.tileSpace)
        {
            this.innerWallTilemap.SetTile(new Vector3Int(tup.Item1, tup.Item2, 0), null);
        }
    }

    private void SetupExperiment()
    {
        ResetMap();

        this.dfsResults = new List<bool>();
        this.dfsTimes = new List<float>();
        this.bfsResults = new List<bool>();
        this.bfsTimes = new List<float>();
        this.tileResults = new List<bool>();
        this.tileTimes = new List<float>();

        if (this.preLoadedMaps.Count == 0)
        {
            this.SampleTileSpace();
            foreach ((int, int) tup in this.currWalls)
            {
                this.innerWallTilemap.SetTile(new Vector3Int(tup.Item1, tup.Item2, 0), this.wallTile);
            }
        }
        else
        {
            List<(int, int)> currMap = this.preLoadedMaps.Pop();
            this.currWalls = currMap;
            foreach ((int, int) tup in currMap)
            {
                this.innerWallTilemap.SetTile(new Vector3Int(tup.Item1, tup.Item2, 0), this.wallTile);
            }
        }

        this.placeHiderAndSeeker();
        this.currExperimentType = ExperimentType.DFS;
        this.RunExperiment();
    }

    private void RunExperiment()
    {
        this.RunExperiment(this.currExperimentType);
    }

    private void RunExperiment(ExperimentType type)
    {
        this.experimentNumberText.text = $"Test: {this.currExperimentRun+1} of {NUM_EXPERIMENTS_TO_RUN}";

        if (this.currExperimentCount >= this.MAX_EXPERIMENTS)
        {
            this.DoneExperiment();
            return;
        }
        if (this.currExperimentType == ExperimentType.Done)
        {
            this.currExperimentRun++;
            this.WriteResults();
            if (this.currExperimentRun == NUM_EXPERIMENTS_TO_RUN)
            {
                Application.Quit();
                return;
            }
            this.SetupExperiment();
        }

        this.seeker.transform.position = new Vector3(this.seekerPosition.Item1, this.seekerPosition.Item2, 0);

        if (type == ExperimentType.DFS)
        {
            statusText.text = $"DFS: {this.currExperimentCount+1} of {MAX_EXPERIMENTS}";
            this.RunDFS();
        }
        else if (type == ExperimentType.BFS)
        {
            statusText.text = $"BFS {this.currExperimentCount+1} of {MAX_EXPERIMENTS}";
            this.RunBFS();
        }
        else if (type == ExperimentType.Tile)
        {
            statusText.text = $"Corners: {this.currExperimentCount+1} of {MAX_EXPERIMENTS}";
            this.RunTile();
        }
    }

    private void DoneExperiment()
    {
        if (this.currExperimentType == ExperimentType.DFS)
        {
            float average = this.dfsTimes.Average();
            this.currExperimentType = ExperimentType.BFS;
            Debug.Log($"Average DFS: {average}");
        }
        else if (this.currExperimentType == ExperimentType.BFS)
        {
            float average = this.bfsTimes.Average();
            this.currExperimentType = ExperimentType.Tile;
            Debug.Log($"Average BFS: {average}");
        }
        else if (this.currExperimentType == ExperimentType.Tile)
        {
            float average = this.tileTimes.Average();
            this.currExperimentType = ExperimentType.Done;
            Debug.Log($"Average Tile: {average}");
        }
        this.currExperimentCount = 0;
        this.RunExperiment();
    }

    private void WriteResults()
    {
        string fileName = $"Experiment{this.currExperimentRun}.json";
        string dir = Application.dataPath + "/../";

        string textPath = Path.Combine(dir, fileName);

        using (StreamWriter writer = new StreamWriter(textPath))
        {
            ExportClass export = new ExportClass();
            export.bfsResults = this.bfsResults;
            export.bfsTimes = this.bfsTimes;
            export.dfsResults = this.bfsResults;
            export.dfsTimes = this.dfsTimes;
            export.tileResults = this.tileResults;
            export.tileTimes = this.tileTimes;
            export.map = new List<int>();
            export.hiderX = this.hiderPosition.Item1;
            export.hiderY = this.hiderPosition.Item2;
            export.seekerX = this.seekerPosition.Item1;
            export.seekerY = this.seekerPosition.Item2;
            export.mapType = this.mapType;

            foreach ((int, int) tup in this.currWalls)
            {
                export.map.Add(tup.Item1);
                export.map.Add(tup.Item2);
            }

            string json = JsonUtility.ToJson(export);
            writer.WriteLine(json);
        }
    }

    public void NotifyDone(bool found, float time)
    {
        this.currExperimentCount++;
        if (this.currExperimentType == ExperimentType.BFS)
        {
            this.bfsResults.Add(found);
            this.bfsTimes.Add(time);
        }
        else if (this.currExperimentType == ExperimentType.DFS)
        {
            this.dfsResults.Add(found);
            this.dfsTimes.Add(time);
        }
        else if (this.currExperimentType == ExperimentType.Tile)
        {
            this.tileResults.Add(found);
            this.tileTimes.Add(time);
        }
        // Debug.Log($"{found}: {time}");
        this.RunExperiment(this.currExperimentType);
    }

    private void RunDFS()
    {
        this.dfsSeeker.Run();
    }

    private void RunBFS()
    {
        this.bfsSeeker.Run();
    }

    private void RunTile()
    {
        this.tileSeeker.Run();
    }

    private (int, int) randFromIntIntList(List<(int, int)> list)
    {
        return list[Mathf.FloorToInt(UnityEngine.Random.value * list.Count)];
    }

    private void SampleTileSpace()
    {
        if (this.mapType == MapType.Random)
        {
            List<(int, int)> result = new List<(int, int)>();
            int numNeeded = Mathf.FloorToInt(UnityEngine.Random.value * this.tileSpace.Count);
            int numLeft = this.tileSpace.Count;
            int curr = 0;
            while (numNeeded > 0)
            {
                float val = UnityEngine.Random.value;
                float prob = ((float)numNeeded) / ((float)numLeft);
                if (val < prob)
                {
                    result.Add(this.tileSpace[curr]);
                    numNeeded--;
                }
                curr++;
                numLeft--;
            }
            this.currWalls = result;

            if (Mathf.Abs(this.currWalls.Count - this.tileSpace.Count) <= 5)
            {
                this.SampleTileSpace();
            }
        }
        else
        {
            this.BuildMap(-3, 8, -5, 6);
        }
    }

    private void BuildMap(int bottomRow, int topRow, int leftCol, int rightCol)
    {
        if (leftCol >= rightCol || bottomRow >= topRow) return;

        int width = rightCol - leftCol - 1;
        int height = topRow - bottomRow - 1;

        if (width <= 1 || height <= 1) return;

        int area = width * height;
        int MAX_AREA = 9;
        if (area < MAX_AREA) return;

        bool horizontal = UnityEngine.Random.value > 0.5f;
        if (horizontal)
        {
            // pick a random row
            int max = topRow - 1;
            int min = bottomRow + 1;

            // Debug.Log($"({bottomRow}, {topRow}, {leftCol}, {rightCol}) - {min} - {max}");
            int row = Mathf.FloorToInt(UnityEngine.Random.value * (max - min + 1) + min);

            List<int> wallsOnRow = this.randomValuesInRange(leftCol + 1, rightCol - 1);

            // StringBuilder sb = new StringBuilder();
            // sb.Append("Columns: ");
            foreach (int c in wallsOnRow)
            {
                // sb.Append($"{c}, ");
                this.currWalls.Add((c, row));
            }

            // Debug.Log($"Chosen row: {row}");
            // Debug.Log(sb.ToString());

            this.BuildMap(row, topRow, leftCol, rightCol);
            this.BuildMap(bottomRow, row, leftCol, rightCol);
        }
        else
        {
            // pick a random column
            int max = (rightCol - 1);
            int min = leftCol + 1;

            // Debug.Log($"({bottomRow}, {topRow}, {leftCol}, {rightCol}) - {min} - {max}");
            int col = Mathf.FloorToInt(UnityEngine.Random.value * (max - min + 1) + min);

            List<int> wallsOnCol = this.randomValuesInRange(bottomRow + 1, topRow - 1);

            StringBuilder sb = new StringBuilder();
            // sb.Append("Rows: ");
            foreach (int r in wallsOnCol)
            {
                // sb.Append($"{r}, ");
                this.currWalls.Add((col, r));
            }

            // Debug.Log($"Chosen col: {col}");
            // Debug.Log(sb.ToString());

            this.BuildMap(bottomRow, topRow, col, rightCol);
            this.BuildMap(bottomRow, topRow, leftCol, col);
        }
    }

    private List<int> randomValuesInRange(int low, int high)
    {
        // Debug.Log($"Low: {low}, High: {high}");
        int maxNumValues = high - low;
        // get anywhere from 1 to all of the values
        int numbersToGet = Mathf.FloorToInt(UnityEngine.Random.value * maxNumValues + 1);

        List<int> valsToSelect = EnumerableUtility.Range(low, high + 1).ToList();

        List<int> res = new List<int>();

        int curr = 0;
        int numLeft = valsToSelect.Count;
        // UnityEngine.Debug.Log($"{numLeft}, {numbersToGet}");
        while (numbersToGet > 0)
        {
            float val = UnityEngine.Random.value;
            float prob = ((float)numbersToGet) / ((float)numLeft);
            if (val < prob)
            {
                res.Add(valsToSelect[curr]);
                numbersToGet--;
            }
            curr++;
            numLeft--;
        }
        return res;
    }

    private (int, int) findBlockNotInWalls()
    {
        (int, int) randTup = this.randFromIntIntList(this.tileSpace);
        bool contains = this.currWalls.Contains(randTup);
        while (contains)
        {
            randTup = this.randFromIntIntList(this.tileSpace);
            contains = this.currWalls.Contains(randTup);
        }
        return randTup;
    }

    private void placeHiderAndSeeker()
    {
        if (this.preLoadedHiders.Count == 0)
        {
            (int, int) seekerPosition = this.findBlockNotInWalls();
            while (!positionInBounds(seekerPosition))
            {
                seekerPosition = this.findBlockNotInWalls();
            }

            this.seekerPosition = (seekerPosition.Item1 + 0.5f, seekerPosition.Item2 + 0.5f);
            this.seeker.transform.position = new Vector3(seekerPosition.Item1 + 0.5f, seekerPosition.Item2 + 0.5f, 0);
            // Debug.Log(seekerPosition);

            (int, int) hiderPosition = this.findBlockNotInWalls();
            while (!positionInBounds(hiderPosition) || hiderPosition == seekerPosition)
            {
                hiderPosition = this.findBlockNotInWalls();
            }

            this.hiderPosition = (hiderPosition.Item1 + 0.5f, hiderPosition.Item2 + 0.5f);
            this.hider.transform.position = new Vector3(hiderPosition.Item1 + 0.5f, hiderPosition.Item2 + 0.5f, 0);
            // Debug.Log(hiderPosition);
        }
        else
        {
            (float, float) hiderPosition = this.preLoadedHiders.Pop();
            this.hiderPosition = hiderPosition;
            this.hider.transform.position = new Vector3(hiderPosition.Item1, hiderPosition.Item2, 0);

            (float, float) seekerPosition = this.preLoadedSeekers.Pop();
            this.seekerPosition = seekerPosition;
            this.seeker.transform.position = new Vector3(seekerPosition.Item1, seekerPosition.Item2, 0);
        }
    }

    private bool positionInBounds((int, int) pos)
    {
        float x = pos.Item1 + 0.5f;
        float y = pos.Item2 + 0.5f;

        return (x > -4f && x < 6f) && (y > -2f && y < 8f);
    }

    [Serializable]
    private class ExportClass
    {
        // tuple does not serialize, so instead treat as flattened
        public List<int> map;
        public float hiderX;
        public float hiderY;
        public float seekerX;
        public float seekerY;
        public List<bool> dfsResults;
        public List<float> dfsTimes;
        public List<bool> bfsResults;
        public List<float> bfsTimes;
        public List<bool> tileResults;
        public List<float> tileTimes;
        public MapType mapType;
    }
}