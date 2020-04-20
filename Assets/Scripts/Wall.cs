using UnityEngine;
using System.Text;

public class Wall
{
    public Vector2 pointOne;
    public Vector2 pointTwo;
    public float xMin;
    public float xMax;
    public float yMin;
    public float yMax;
    public bool horizontal;
    public bool permanent;

    public Wall(Vector2 pointOne, Vector2 pointTwo, bool permanent)
    {
        this.pointOne = pointOne;
        this.pointTwo = pointTwo;

        this.horizontal = Mathf.Abs(pointOne.y - pointTwo.y) < Mathf.Pow(10, -3);
        this.xMin = Mathf.Min(pointOne.x, pointTwo.x);
        this.xMax = Mathf.Max(pointOne.x, pointTwo.x);
        this.yMin = Mathf.Min(pointOne.y, pointTwo.y);
        this.yMax = Mathf.Max(pointOne.y, pointTwo.y);
        this.permanent = permanent;
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append((this.horizontal) ? "Horizontal: " : "Vertical: ");
        if (this.horizontal)
        {
            sb.Append($"y = {this.yMin}, x-min = {this.xMin}, x-max = {this.xMax}");
        }
        else
        {
            sb.Append($"x = {this.xMin}, y-min = {this.yMin}, y-max = {this.yMax}");
        }
        return sb.ToString();
    }
}