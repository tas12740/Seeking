using UnityEngine;
using UnityEngine.Tilemaps;

public class TestTile : MonoBehaviour
{
    public Tilemap innerWallTilemap;
    public TileBase wallTile;
    void Start() {
        this.innerWallTilemap.SetTile(new Vector3Int(5, 7, 0), this.wallTile);
        this.innerWallTilemap.SetTile(new Vector3Int(-4, 7, 0), this.wallTile);
        this.innerWallTilemap.SetTile(new Vector3Int(-4, -2, 0), this.wallTile);
        this.innerWallTilemap.SetTile(new Vector3Int(5, -2, 0), this.wallTile);
    }
}