using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;
using MelonLoader;
using JohnBoltsLevelWarehouse.Editor.UndoRedo;
using JohnBoltsLevelWarehouse.Editor;
namespace JohnBoltsLevelWarehouse.Brushes
{
    public class Box : IBrush
    {
        bool drawingBox = false;
        public UndoableAction Draw(Vector3Int posInt, int brushSize)
        {
            if (!drawingBox)
            {
                drawingBox = true;
                Vector3Int startPos = posInt;
                MelonCoroutines.Start(IDrawBox(posInt, false));
            }
            return null;
        }
        public UndoableAction Erase(Vector3Int posInt, int brushSize)
        {
            if (!drawingBox)
            {
                drawingBox = true;
                Vector3Int startPos = posInt;
                MelonCoroutines.Start(IDrawBox(posInt, true));
            }
            return null;
        }
        public System.Collections.IEnumerator IDrawBox(Vector3Int startPos, bool erase)
        {
            UndoableAction tempBox = null;
            while (!Input.GetMouseButtonUp(0))
            {
                tempBox = DrawBox(startPos, Drawing.instance.MousePosInt(), tempBox, erase);
                yield return null;
            }
            UndoManager.instance.Add(DrawBox(startPos, Drawing.instance.MousePosInt(), tempBox, erase));
            drawingBox = false;
        }
        public UndoableAction DrawBox(Vector3Int start, Vector3Int end, UndoableAction tempBox, bool erase)
        {
            List<(Vector3Int place, UndoTile previous)> placedTiles = new List<(Vector3Int place, UndoTile previous)>();
            UndoableAction undo = new UndoableAction(new List<(Vector3Int pos, UndoTile previousTile)>(), false);
            int minX = Mathf.Min(start.x, end.x), maxX = Mathf.Max(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y), maxY = Mathf.Max(start.y, end.y);
            if (tempBox != null)
            {
                foreach ((Vector3Int place, UndoTile previousTile) undoTile in tempBox.tiles)
                {
                    LayerManager.instance.layers[undoTile.previousTile.layer].map.SetTile(undoTile.place, undoTile.previousTile.tile);
                }
            }
            for (int i = minX; i <= maxX; i++)
            {
                for (int j = minY; j <= maxY; j++)
                {
                    if (Drawing.instance.IsInBounds(i, j))
                    {
                        Vector3Int pos = new Vector3Int(i, j);
                        if (!erase)
                        {
                            undo += Drawing.instance.RemoveOtherIfOccupied(pos);
                        }
                        placedTiles.Add((pos, new UndoTile((Tile)LayerManager.instance.activeLayer.map.GetTile(pos), LayerManager.instance.selectedLayer)));
                        LayerManager.instance.activeLayer.map.SetTile(pos, erase ? null : Drawing.instance.tiles[Drawing.instance.selectedTile].tile);
                    }
                }
            }
            return undo + new UndoableAction(placedTiles, false);
        }
    }
}