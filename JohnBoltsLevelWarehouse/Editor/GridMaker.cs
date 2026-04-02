using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace JohnBoltsLevelWarehouse.Editor
{
    public static class GridMaker
    {
        public static List<LineRenderer> horizontals = new List<LineRenderer>();
        public static List<LineRenderer> verticals = new List<LineRenderer>();
        public static GameObject parent;
        public static void InitGrid(int x, int y)
        {
            parent = new GameObject();
            Color color = new Color(LevelEditorFrontend.tjYellow.r, LevelEditorFrontend.tjYellow.g, LevelEditorFrontend.tjYellow.b, 0.5f);
            Material mat = new Material(Shader.Find("Sprites/Default"));
            for (int i = y / -2; i <= y / 2 + 1; i++)
            {
                GameObject LRObj = new GameObject();
                LRObj.transform.parent = parent.transform;
                LineRenderer lr = LRObj.AddComponent<LineRenderer>();
                lr.material = lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.SetPosition(0, new Vector3(x / -2, i, 0));
                lr.SetPosition(1, new Vector3(x / 2 + 1, i, 0));
                //MelonLogger.Msg(lr.GetPosition(1));
                lr.startColor = color;
                lr.endColor = color;
                lr.startWidth = 0.1f;
                lr.endWidth = 0.1f;
                lr.sortingOrder = -1;
            }
            for (int i = x / -2; i <= x / 2 + 1; i++)
            {
                GameObject LRObj = new GameObject();
                LRObj.transform.parent = parent.transform;
                LineRenderer lr = LRObj.AddComponent<LineRenderer>();
                lr.material = lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.SetPosition(0, new Vector3(i, y / -2, 0));
                lr.SetPosition(1, new Vector3(i, y / 2 + 1, 0));
                //MelonLogger.Msg(lr.GetPosition(1));
                lr.startColor = color;
                lr.endColor = color;
                lr.startWidth = 0.1f;
                lr.endWidth = 0.1f;
                lr.sortingOrder = -1;
            }
        }
        public static void UpdateGrid(int x, int y)
        {
            for (int i = parent.transform.childCount - 1; i >= 0; i--)
            {
                GameObject.Destroy(parent.transform.GetChild(i).gameObject);
            }
            InitGrid(x, y);
        }
    }
}