using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;
using MelonLoader;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.TerrainTools;
using Unity.VisualScripting;
using JohnBoltsLevelWarehouse.Editor.UndoRedo;
namespace JohnBoltsLevelWarehouse.Editor
{
    public class Drawing
    {
        public int brushSize = 1;
        public static readonly List<Brushes.IBrush> brushes = new List<Brushes.IBrush>
        {
            new Brushes.Basic(),
            new Brushes.Box(),
        };
        public int _brush = 0;
        public Brushes.IBrush brush
        {
            get => brushes[_brush];
        }
        public bool erasing = false;
        public int selectedTile;
        public TMP_Dropdown TileDropdown;
        public Image TileImage;
        public List<(string path, Tile tile)> tiles = new List<(string path, Tile tile)>();
        public Dictionary<Tile, int> tileToInt = new Dictionary<Tile, int>();
        public static Drawing instance;
        public Drawing()
        {
            instance = this;
        }

        public void LoadFilesToPalette(string[] files)
        {
            foreach (string file in files)
            {
                Tile t = LoadTileFromPath(file);
                tiles.Add((file, t));
                MelonLogger.Msg("Added tile: " + Path.GetFileNameWithoutExtension(file));
                TileDropdown.options.Add(new TMP_Dropdown.OptionData(Path.GetFileNameWithoutExtension(file)));
                TileDropdown.RefreshShownValue();
                TileDropdown.onValueChanged.Invoke(TileDropdown.value);
                tileToInt.Add(t, tiles.Count);
            }

        }
        private static Tile LoadTileFromPath(string path)
        {
            //MelonLogger.Msg("file at path " + path + " exists? " + File.Exists(path));
            Sprite sprite;
            byte[] data = File.ReadAllBytes(path);
            Texture2D texture2D = new Texture2D(2, 2);
            if (texture2D.LoadImage(data))
            {
                texture2D.filterMode = FilterMode.Point;
                sprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f));
            }
            else
            {
                return null;
            }
            Tile ret = ScriptableObject.CreateInstance<Tile>();
            ret.sprite = ScaleSpriteToUnitSize(sprite);
            return ret;

        }
        public static Sprite ScaleSpriteToUnitSize(Sprite sprite, float targetUnitSize = 1)
        {
            float num = sprite.texture.width;
            float num2 = sprite.texture.height;
            float x = sprite.bounds.size.x;
            float y = sprite.bounds.size.y;
            float num3 = targetUnitSize / x;
            float num4 = targetUnitSize / y;
            Texture2D texture2D = new Texture2D((int)(num * num3), (int)(num2 * num4));
            for (int i = 0; i < texture2D.width; i++)
            {
                for (int j = 0; j < texture2D.height; j++)
                {
                    Color pixelBilinear = sprite.texture.GetPixelBilinear((float)i / (float)texture2D.width, (float)j / (float)texture2D.height);
                    texture2D.SetPixel(i, j, pixelBilinear);
                }
            }
            texture2D.Apply();
            return Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f));
        }
        Vector3Int lastPosition;
        UndoRedo.UndoableAction tempBoxTiles;
        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.Backslash))
            {
                erasing = !erasing;
            }
            if (tiles.Count != 0 && tiles[selectedTile].tile != null)
            {
                if (Input.GetMouseButton(0) && LevelEditorFrontend.instance.allowPanZoom && !IsMouseOverBars())
                {
                    //MelonLogger.Msg("press");
                    Vector3 pos = LevelEditorFrontend.instance.cam.ScreenToWorldPoint(Input.mousePosition);
                    //MelonLogger.Msg("poscalc done");
                    Vector3Int posInt = LayerManager.instance.activeLayer.map.WorldToCell(pos);
                    //MelonLogger.Msg("posintcalc done");
                    if (posInt != lastPosition)
                    {
                        lastPosition = posInt;
                        if (!erasing)
                        {
                            UndoableAction action = brush.Draw(posInt, brushSize);
                            if (action != null)
                            {
                                UndoManager.instance.Add(action);
                            }
                        }
                        else
                        {
                            UndoableAction action = brush.Erase(posInt, brushSize);
                            if (action != null)
                            {
                                UndoManager.instance.Add(action);
                            }
                        }
                    }
                }
            }
        }
        public UndoRedo.UndoableAction RemoveOtherIfOccupied(Vector3Int pos)
        {
            List<(Vector3Int pos, UndoRedo.UndoTile tile)> tiles = new List<(Vector3Int pos, UndoRedo.UndoTile tile)>();
            for (int i = 0; i < LayerManager.instance.layers.Count; i++)
            {
                if (LayerManager.instance.selectedLayer != i && LayerManager.instance.layers[i].map.HasTile(pos))
                {
                    tiles.Add((pos, new UndoRedo.UndoTile((Tile)LayerManager.instance.layers[i].map.GetTile(pos), i)));
                    LayerManager.instance.layers[i].map.SetTile(pos, null);
                }
            }
            return new UndoRedo.UndoableAction(tiles, false);
        }
        public bool IsInBounds(int x, int y)
        {
            return (x < LevelEditorFrontend.instance.dimensions.x / 2 + 1 && x > LevelEditorFrontend.instance.dimensions.x / -2 - 1 && y < LevelEditorFrontend.instance.dimensions.y / 2 + 1 && y > LevelEditorFrontend.instance.dimensions.y / -2 - 1);
        }
        public static bool IsMouseOverBars()
        {
            return EventSystem.current.IsPointerOverGameObject();
        }
        public Vector3Int MousePosInt()
        {
            Vector3 pos = LevelEditorFrontend.instance.cam.ScreenToWorldPoint(Input.mousePosition);
            return LayerManager.instance.activeLayer.map.WorldToCell(pos);
        }
        public static int GetActualBrushSize(int num)
        {
            if (num % 2 == 0)
            {
                return num - 1;
            }
            return num;
        }
    }
}