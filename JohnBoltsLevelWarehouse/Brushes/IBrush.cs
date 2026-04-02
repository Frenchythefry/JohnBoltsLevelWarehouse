using System.Collections.Generic;
using JohnBoltsLevelWarehouse.Editor.UndoRedo;
using UnityEngine;
namespace JohnBoltsLevelWarehouse.Brushes
{
    public interface IBrush
    {
        public UndoableAction Draw(Vector3Int posInt, int brushSize);
        public UndoableAction Erase(Vector3Int posInt, int brushSize);
    }
}