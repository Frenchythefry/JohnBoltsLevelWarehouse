using System.Collections.Generic;
using JohnBoltsLevelWarehouse.Editor;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Tilemaps;
namespace JohnBoltsLevelWarehouse
{
    public class JsonLayer
    {
        public string name;
        public TilePos map;
        public LayerType type;
        [JsonConstructor]
        public JsonLayer(string name, TilePos map, LayerType type)
        {
            this.name = name;
            this.map = map;
            this.type = type;
        }
        public JsonLayer(Layer layer)
        {
            this.name = layer.name;
            List<List<int>> tiles = new List<List<int>>();
            int row = 0;
            for (int j = LevelEditorFrontend.instance.dimensions.y / 2; j >= LevelEditorFrontend.instance.dimensions.y / -2; j--)
            {
                tiles.Add(new List<int>());
                for (int i = LevelEditorFrontend.instance.dimensions.x / -2; i <= LevelEditorFrontend.instance.dimensions.x / 2; i++)
                {
                    if (layer.map.HasTile(new Vector3Int(i, j)))
                        tiles[row].Add(Drawing.instance.tileToInt[(Tile)layer.map.GetTile(new Vector3Int(i, j))]);
                    else
                        tiles[row].Add(0);
                }
                row++;
            }
            this.map = new TilePos(tiles);
            this.type = layer.type;
        }
    }
    public enum LayerType
    {
        Normal,
        NormalGrapple,
        Deadly,
        DeadlyGrapple,
        Invisible,
        NoColl,
    }
}