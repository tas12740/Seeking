using System.Linq;
using UnityEngine;
using System.Collections.Generic;

public class Room
{
    public Wall[] allWalls;
    public Vector2 NWCorner;
    public Vector2 NECorner;
    public Vector2 SWCorner;
    public Vector2 SECorner;
    public bool cleared;
    public List<int> blocksEntrance;

    public Room(Wall wallOne, Wall wallTwo, Wall wallThree, Wall wallFour)
    {
        this.blocksEntrance = new List<int>();

        Wall[] allWalls = new Wall[] { wallOne, wallTwo, wallThree, wallFour };
        this.allWalls = allWalls;

        IEnumerable<Wall> horizontalWalls = allWalls.Where(t => t.horizontal);
        IEnumerable<Wall> verticalWalls = allWalls.Where(t => !t.horizontal);

        IEnumerable<float> xs = verticalWalls.Select(t => t.xMin).Concat(verticalWalls.Select(t => t.xMax));
        IEnumerable<float> ys = horizontalWalls.Select(t => t.yMin).Concat(horizontalWalls.Select(t => t.yMax));

        float xMin = xs.Min();
        float xMax = xs.Max();
        float yMin = ys.Min();
        float yMax = ys.Max();

        this.SWCorner = new Vector2(xMin, yMin);
        this.SECorner = new Vector2(xMax, yMin);
        this.NWCorner = new Vector2(xMin, yMax);
        this.NECorner = new Vector2(xMax, yMax);
    }

    public static bool twoWallsIntersect(Wall one, Wall two)
    {
        float epsilon = Mathf.Pow(10, -3);
        if (one.horizontal == two.horizontal)
        {
            // for the sake of our use here, return true if walls are parallel - we want to find the two that don't intersect
            return true;
        }
        Wall horizontal = (one.horizontal) ? one : two;
        Wall vertical = (one.horizontal) ? two : one;

        float xVert = vertical.xMin;
        float yHor = horizontal.yMin;
        return (horizontal.xMin - epsilon <= xVert && horizontal.xMax + epsilon >= xVert && vertical.yMin - epsilon <= yHor && vertical.yMax + epsilon >= yHor);
    }

    public int getRandBlock()
    {
        int randIndex = Mathf.FloorToInt(UnityEngine.Random.value * this.blocksEntrance.Count);
        return this.blocksEntrance[randIndex];
    }
}