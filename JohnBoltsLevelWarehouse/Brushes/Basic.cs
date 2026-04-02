using System.Collections.Generic;
using JohnBoltsLevelWarehouse.Editor;
using JohnBoltsLevelWarehouse.Editor.UndoRedo;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace JohnBoltsLevelWarehouse.Brushes
{
    public class Basic : IBrush
    {
        public UndoableAction Draw(Vector3Int posInt, int brushSize)
        {
            List<(Vector3Int pos, UndoTile tile)> undoables = new List<(Vector3Int pos, UndoTile tile)>();
            UndoableAction undoable = new UndoableAction(new List<(Vector3Int pos, UndoTile previousTile)>(), false);
            for (int i = brushSize / -2; i <= brushSize / 2; i++)
            {
                for (int j = brushSize / -2; j <= brushSize / 2; j++)
                {
                    if (Drawing.instance.IsInBounds(posInt.x, posInt.y))
                    {
                        if (Drawing.instance.tiles.Count != 0 && Drawing.instance.tiles[Drawing.instance.selectedTile].tile != null)
                        {
                            undoables.Add((posInt, new UndoTile((Tile)LayerManager.instance.activeLayer.map.GetTile(posInt), LayerManager.instance.selectedLayer)));
                            LayerManager.instance.activeLayer.map.SetTile(posInt, Drawing.instance.tiles[Drawing.instance.selectedTile].tile);
                            undoable += Drawing.instance.RemoveOtherIfOccupied(posInt);
                        }
                    }
                }
            }
            return undoable + new UndoableAction(undoables, false);
        }
        public UndoableAction Erase(Vector3Int posInt, int brushSize)
        {
            List<(Vector3Int pos, UndoTile tile)> undoables = new List<(Vector3Int pos, UndoTile tile)>();
            for (int i = brushSize / -2; i <= brushSize / 2; i++)
            {
                for (int j = brushSize / -2; j <= brushSize / 2; j++)
                {
                    if (Drawing.instance.IsInBounds(posInt.x, posInt.y))
                    {
                        undoables.Add((posInt, new UndoTile((Tile)LayerManager.instance.activeLayer.map.GetTile(posInt), LayerManager.instance.selectedLayer)));
                        LayerManager.instance.activeLayer.map.SetTile(posInt, null);
                    }
                }
            }
            return new UndoableAction(undoables, true);
        }
    }
}