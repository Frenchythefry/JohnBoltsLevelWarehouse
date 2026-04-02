using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace JohnBoltsLevelWarehouse.Editor.UndoRedo
{
    public class UndoableAction
    {
        public List<(Vector3Int pos, UndoTile previousTile)> tiles;

        public bool wasErase;

        public UndoableAction(List<(Vector3Int pos, UndoTile previousTile)> tiles, bool wasErase)
        {
            this.tiles = tiles;
            this.wasErase = wasErase;
        }
        public static UndoableAction operator +(UndoableAction left, UndoableAction right)
        {
            if (left.wasErase != right.wasErase)
            {
                throw new ArithmeticException("Cannot add an erase and non-erase");
            }
            return new UndoableAction(left.tiles.Concat(right.tiles).ToList(), left.wasErase);
        }
    }
    public class UndoTile
    {
        public Tile tile;
        public int layer;
        public UndoTile(Tile tile, int layer)
        {
            this.tile = tile;
            this.layer = layer;
        }
    }
}