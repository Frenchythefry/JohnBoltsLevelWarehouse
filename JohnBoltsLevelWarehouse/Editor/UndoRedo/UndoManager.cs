using System.Collections.Generic;
using System.Drawing;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace JohnBoltsLevelWarehouse.Editor.UndoRedo
{
    public class UndoManager
    {
        public List<UndoableAction> UndoHistory = new List<UndoableAction>();
        int pointer = -1;
        public static UndoManager instance;
        public UndoManager()
        {
            instance = this;
        }
        public void Undo()
        {
            if (pointer == -1) return;
            UndoableAction undoable = new UndoableAction(new List<(Vector3Int pos, UndoTile previousTile)>(), false);
            foreach ((Vector3Int pos, UndoTile tile) undo in UndoHistory[pointer].tiles)
            {
                undoable.tiles.Add((undo.pos, new UndoTile((Tile)LayerManager.instance.layers[undo.tile.layer].map.GetTile(undo.pos), undo.tile.layer)));
                LayerManager.instance.layers[undo.tile.layer].map.SetTile(undo.pos, undo.tile.tile);
            }
            UndoHistory[pointer] = undoable;
            pointer--;
        }
        public void Redo()
        {
            if (UndoHistory.Count == 0 || pointer == UndoHistory.Count - 1) return;
            UndoableAction undoable = new UndoableAction(new List<(Vector3Int pos, UndoTile previousTile)>(), false);
            foreach ((Vector3Int pos, UndoTile tile) undo in UndoHistory[pointer + 1].tiles)
            {
                undoable.tiles.Add((undo.pos, new UndoTile((Tile)LayerManager.instance.layers[undo.tile.layer].map.GetTile(undo.pos), undo.tile.layer)));
                LayerManager.instance.layers[undo.tile.layer].map.SetTile(undo.pos, undo.tile.tile);
            }
            UndoHistory[pointer + 1] = undoable;
            pointer++;
        }
        public void Clear()
        {
            UndoHistory.Clear();
            pointer = -1;
        }
        public void Add(UndoableAction undoableAction)
        {
            if (pointer != UndoHistory.Count - 1)
            {
                for (int i = UndoHistory.Count - 1; i > pointer; i--)
                {
                    UndoHistory.RemoveAt(i);
                }
            }
            pointer++;
            UndoHistory.Add(undoableAction);
        }
        public void FixLayerRemove(int layer)
        {
            foreach (UndoableAction undo in UndoHistory)
            {
                for (int i = undo.tiles.Count - 1; i >= 0; i--)
                {
                    if (undo.tiles[i].previousTile.layer == layer)
                    {
                        undo.tiles.RemoveAt(i);
                    }
                    else if (undo.tiles[i].previousTile.layer > layer)
                    {
                        var entry = undo.tiles[i];
                        entry.previousTile.layer--;
                        undo.tiles[i] = entry;
                    }
                }
            }
        }
    }
}