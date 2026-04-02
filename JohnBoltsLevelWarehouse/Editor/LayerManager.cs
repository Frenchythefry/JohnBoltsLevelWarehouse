using System;
using System.Collections.Generic;
using System.Linq;
using JohnBoltsLevelWarehouse.Editor.UndoRedo;
using MelonLoader;
using Newtonsoft.Json;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace JohnBoltsLevelWarehouse.Editor
{
    public class LayerManager
    {
        public int selectedLayer = -1;
        public List<Layer> layers = new List<Layer>();
        List<GameObject> layerUIObjs = new List<GameObject>();
        public GameObject layersObj;
        private Transform layerContainer;
        public static GameObject layerUIPrefab;
        public static LayerManager instance;
        public LayerManager()
        {
            instance = this;
        }
        public Layer activeLayer
        {
            get
            {
                if (selectedLayer == -1) return null;
                return layers[selectedLayer];
            }
        }
        public void AddLayer()
        {
            int num = GetNumNewLayers();
            Layer temp = new Layer("New Layer" + (num != 0 ? (" (" + num + ")") : ""), LayerType.Normal);
            layers.Add(temp);
            MelonLogger.Msg("After add to list");
            layerUIObjs.Add(WireLayerUIObj(temp));
            MelonLogger.Msg("After wire");
            SelectLayer(layers.Count - 1);
        }
        public void AddLayer(JsonLayer layer, Drawing drawer)
        {
            Layer temp = new Layer(layer, drawer);
            layers.Add(temp);
            layerUIObjs.Add(WireLayerUIObj(temp));
            SelectLayer(layers.Count - 1);
        }
        public void SelectLayer(int layer)
        {
            MelonLogger.Msg("Before select layer " + layer);
            for (int i = 0; i < layerUIObjs.Count; i++)
            {
                MelonLogger.Msg("Layer " + i);
                layerUIObjs[i].transform.GetChild(4).gameObject.SetActive((i == layer ? false : true));
            }
            selectedLayer = layer;
            MelonLogger.Msg("After select layer");
        }
        public void SelectLayer(Layer layer)
        {
            MelonLogger.Msg("Before select layer " + layer);
            for (int i = 0; i < layerUIObjs.Count; i++)
            {
                MelonLogger.Msg("Layer " + i);
                if (layers[i] == layer)
                {
                    layerUIObjs[i].transform.GetChild(4).gameObject.SetActive(false);
                    selectedLayer = i;
                }
                else
                {
                    layerUIObjs[i].transform.GetChild(4).gameObject.SetActive(true);
                }
            }
            MelonLogger.Msg("After select layer");
        }
        public void RemoveLayer(Layer layer)
        {
            int idx = layers.IndexOf(layer);

            // Decide what index to land on after removal, before the list shifts
            int newSelected;
            if (activeLayer == layer)
                newSelected = Mathf.Max(0, idx - 1);
            else if (selectedLayer > idx)
                newSelected = selectedLayer - 1; // shift compensated
            else
                newSelected = selectedLayer;     // unaffected

            // Remove everything
            GameObject.Destroy(layer.map.gameObject);
            layers.Remove(layer);
            GameObject.Destroy(layerUIObjs[idx]);
            layerUIObjs.RemoveAt(idx);

            // Select once, cleanly
            SelectLayer(newSelected);
            UndoManager.instance.FixLayerRemove(idx);
        }
        public void SetupVars(GameObject layersObj)
        {
            this.layersObj = layersObj;
            this.layerContainer = layersObj.transform.GetChild(0).GetChild(0).GetChild(0);
            Layer.grid = GameObject.Find("Grid");
        }
        public GameObject WireLayerUIObj(Layer layer)
        {
            GameObject obj = GameObject.Instantiate(layerUIPrefab, layerContainer);
            MelonLogger.Msg("prefab instantiated. Is null? " + (obj == null));

            TMP_Dropdown typeDropdown = obj.transform.GetChild(0).GetComponent<TMP_Dropdown>();
            foreach (LayerType type in Enum.GetValues(typeof(LayerType)).Cast<LayerType>())
            {
                typeDropdown.options.Add(new TMP_Dropdown.OptionData(type.ToString()));
            }
            typeDropdown.value = (int)layer.type;
            MelonLogger.Msg("value is " + typeDropdown.value + " because layertype is " + layer.type.ToString());
            typeDropdown.RefreshShownValue();
            typeDropdown.onValueChanged.AddListener((int value) =>
            {
                layer.type = (LayerType)value;
            });
            MelonLogger.Msg("After type dropdown");

            GameObject visibiltyToggleGO = obj.transform.GetChild(1).gameObject;
            Toggle visibleToggle = visibiltyToggleGO.transform.GetChild(0).GetComponent<Toggle>();
            Toggle invisibleToggle = visibiltyToggleGO.transform.GetChild(1).GetComponent<Toggle>();
            Image vti = visibleToggle.GetComponentInChildren<Image>();
            Image iti = invisibleToggle.GetComponentInChildren<Image>();
            visibleToggle.onValueChanged.AddListener((bool value) =>
            {
                if (!value) return;
                invisibleToggle.transform.SetAsLastSibling();
                vti.color = new Color(vti.color.r, vti.color.g, vti.color.b, 1);
                iti.color = new Color(iti.color.r, iti.color.g, iti.color.b, 0);
                layer.visible = true;
                SetAlphaOfMap(layer, 1);
            });
            invisibleToggle.onValueChanged.AddListener((bool value) =>
            {
                if (!value) return;
                visibleToggle.transform.SetAsLastSibling();
                vti.color = new Color(vti.color.r, vti.color.g, vti.color.b, 0);
                iti.color = new Color(iti.color.r, iti.color.g, iti.color.b, 1);
                layer.visible = false;
                SetAlphaOfMap(layer, 0);
            });
            MelonLogger.Msg("After visibilty");

            Button DeleteButton = obj.transform.GetChild(2).GetComponent<Button>();
            DeleteButton.onClick.AddListener(() =>
            {
                RemoveLayer(layer);
            });
            MelonLogger.Msg("After deletebutton");

            TMP_InputField layerName = obj.transform.GetChild(3).GetComponent<TMP_InputField>();
            layerName.text = layer.name;
            layerName.onEndEdit.AddListener((string value) =>
            {
                if (value == "" || IsDuplicateName(value))
                {
                    layerName.text = layer.name;
                }
                else
                {
                    layer.name = value;
                }
            });
            MelonLogger.Msg("After layername");

            Button button = obj.transform.GetChild(4).GetComponent<Button>();
            button.onClick.AddListener(() =>
            {
                SelectLayer(layers.IndexOf(layer));
            });
            MelonLogger.Msg("After select button");

            return obj;
        }
        public int GetNumNewLayers()
        {
            var nameSet = new HashSet<string>(layers.Select(l => l.name));
            int count = 0;
            while (nameSet.Contains(count == 0 ? "New Layer" : "New Layer (" + count + ")"))
            {
                count++;
            }
            return count;
        }
        public bool IsDuplicateName(string name)
        {
            foreach (Layer layer in layers)
            {
                if (layer.name == name)
                {
                    return true;
                }
            }
            return false;
        }
        public static void SetAlphaOfMap(Layer layer, float alpha)
        {
            layer.map.color = new Color(1, 1, 1, alpha);
        }
    }
    public class Layer
    {
        public string name;
        public Tilemap map;
        public LayerType type;
        public bool visible;
        public static GameObject tilemapPrefab;
        public static GameObject grid;
        public Layer(string name, LayerType type)
        {
            this.name = name;
            this.map = GameObject.Instantiate(tilemapPrefab, grid.transform).GetComponent<Tilemap>();
            map.gameObject.name = name;
            this.type = type;
        }
        public Layer(JsonLayer layer, Drawing drawer)
        {
            this.name = layer.name;
            this.map = GameObject.Instantiate(tilemapPrefab, grid.transform).GetComponent<Tilemap>();
            map.gameObject.name = name;
            var intToTile = drawer.tileToInt.ToDictionary(x => x.Value, x => x.Key);

            int row = 0;
            for (int j = LevelEditorFrontend.instance.dimensions.y / 2; j >= LevelEditorFrontend.instance.dimensions.y / -2; j--)
            {
                int col = 0;
                for (int i = LevelEditorFrontend.instance.dimensions.x / -2; i <= LevelEditorFrontend.instance.dimensions.x / 2; i++)
                {
                    if (layer.map.tiles[row][col] != 0)
                        this.map.SetTile(new Vector3Int(i, j), intToTile[layer.map.tiles[row][col]]);
                    col++;
                }
                row++;
            }
            this.type = layer.type;
        }
    }
}