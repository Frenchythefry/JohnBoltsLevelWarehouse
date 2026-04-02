using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public class TilePos
{
    public List<List<int>> tiles;
    public TilePos(List<List<int>> Tiles)
    {
        this.tiles = Tiles;
    }
}