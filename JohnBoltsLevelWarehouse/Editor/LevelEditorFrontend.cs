using System.Collections.Generic;
using TMPro;
using UnityEngine;
using System.Reflection;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System;
using MelonLoader;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using Newtonsoft.Json;
using System.Linq;
using Unity.VisualScripting;
using JohnBoltsLevelWarehouse.Editor.UndoRedo;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace JohnBoltsLevelWarehouse.Editor
{
    /*
    TODO
    Fixes:
        Add palette order and paths, as currently cant save an alr saved level
    Improvements
        do UI
        UndoRedo
        
    */
    public class LevelEditorFrontend
    {
        public (float x, float y) factoryPos = (0, 0);
        public (float x, float y) playerPos = (0, 0);
        public List<(float x, float y)> checkpoints = new List<(float x, float y)>();
        public List<GameObject> checkpointGOs = new List<GameObject>();
        public int currentCheckpoint = -1;
        public static Material normalCheckpointMat;
        public static Material darkCheckpointMat;
        public Ability ability = Ability.Lightning;
        public bool allowSwap = false;
        public (int x, int y) dimensions = (50, 50);
        public GameObject factoryObj;
        public GameObject playerObj;
        GameObject canvas;
        GameObject SaveLevelMenu;
        GameObject OpenLevelMenu;
        public FileBrowser fileBrowser;
        public Camera cam;
        public const int scrollSpeed = 1;
        public bool allowPanZoom = true;
        public static bool verboseLogging = true;
        private TMP_Dropdown checkpointList;
        public static readonly Color tjYellow = new UnityEngine.Color(1, 0.812f, 0.004f, 1);
        public static LevelEditorFrontend instance;
        public LevelEditorFrontend()
        {
            // Fields default: dimensions=(50,50), playerPos=(0,0), factoryPos=(0,0), allowSwap=true, ability=default
            new LayerManager();
            new UndoManager();
            instance = this;
            new Drawing();
            SharedInit();
            LayerManager.instance.AddLayer();
        }

        public LevelEditorFrontend(string levelToLoad)
        {
            new LayerManager();
            new UndoManager();
            instance = this;
            new Drawing();
            string fullpath = Path.Combine(MelonUtils.GameDirectory, "Levels", levelToLoad + ".tjl");
            string tempDir = Program.ExtractArchive(fullpath);

            Rules rules = JsonConvert.DeserializeObject<Rules>(File.ReadAllText(Path.Combine(tempDir, "rules.json")));
            dimensions = rules.dimensions;
            playerPos = rules.startPos;
            allowSwap = rules.allowSwap;
            ability = (Ability)rules.allowedAbility;

            // Pull Win case out early so factoryPos is set before SetupGameObjects places the factory
            foreach ((Case c, List<float> p) thing in rules.other)
            {
                if (thing.c == Case.Win)
                    factoryPos = (thing.p[0], thing.p[1]);
            }
            /*foreach (string f in Directory.GetFiles(Path.Combine(tempDir, "Layers")))
            {
                JsonLayer layer = JsonConvert.DeserializeObject<JsonLayer>(File.ReadAllText(Path.Combine(f)));
                layerManager.AddLayer(layer, drawer);
            }*/
            SharedInit();
            LoadTilesAndCheckpoints(tempDir, rules);
            Program.CleanupTemporaryDirectory(tempDir);
        }

        // -------------------------------------------------------------------------
        // Core init
        // -------------------------------------------------------------------------

        private void SharedInit()
        {
            SetupCanvas();
            SetupGameObjects();
            WireUI();
            GridMaker.InitGrid(dimensions.x, dimensions.y);
        }

        // -------------------------------------------------------------------------
        // GameObject setup — reads from fields so both constructors work identically
        // -------------------------------------------------------------------------

        private void SetupCanvas()
        {
            canvas = GameObject.Find("Canvas");
            canvas.transform.position -= Vector3.forward * 5;
        }

        private void SetupGameObjects()
        {
            cam = Camera.main;
            cam.orthographicSize = 20;
            if (verboseLogging) MelonLogger.Msg("camera loaded");

            factoryObj = GameObject.Instantiate(Melon<Program>.Instance.win);
            factoryObj.GetComponent<WinController>().enabled = false;
            factoryObj.GetComponent<Animator>().enabled = false;
            factoryObj.transform.position = new Vector3(factoryPos.x, factoryPos.y);
            foreach (Transform child in factoryObj.transform)
            {
                bool keep = child.name.Contains("Emission") || child.name == "Factory1";
                child.gameObject.SetActive(keep);
            }
            if (verboseLogging) MelonLogger.Msg("Factory obj loaded");

            playerObj = GameObject.Instantiate(Melon<Program>.Instance.objBundle.LoadAsset<GameObject>("Player B3"));
            playerObj.GetComponent<Rigidbody2D>().gravityScale = 0;
            playerObj.transform.position = new Vector3(playerPos.x, playerPos.y);
            foreach (Transform child in playerObj.transform)
            {
                bool isSprite = child.name == "Sprite" || child.name == "sprite";
                child.gameObject.SetActive(isSprite);
                if (isSprite)
                {
                    var anim = child.GetComponent<AnimationManager>();
                    anim.anim.Play("Idle");
                    anim.enabled = false;
                }
            }
            playerObj.GetComponent<PlayerWalking>().enabled = false;
            playerObj.GetComponent<PlayerSwimming>().enabled = false;
            playerObj.GetComponent<PlayerInputs>().enabled = false;
            if (verboseLogging) MelonLogger.Msg("player obj loaded");
        }

        // -------------------------------------------------------------------------
        // UI wiring — all .text assignments read from fields, so initial values
        // are correct for both the blank and loaded-level cases
        // -------------------------------------------------------------------------

        private void WireUI()
        {
            var topBar = canvas.transform.GetChild(0).gameObject;
            var leftBar = canvas.transform.GetChild(1).gameObject;

            WireTileSelector(leftBar);
            WireGrid(leftBar);
            WireBrushSize(leftBar);
            WireVec2Fields(leftBar, 5, () => factoryPos, v => factoryPos = v, factoryObj, "factory position");
            WireVec2Fields(leftBar, 6, () => playerPos, v => playerPos = v, playerObj, "player position");
            WireCheckpoints(leftBar);
            WireLayerMenu(leftBar);
            WireBrushSelector(leftBar);
            WireDimensions(topBar);
            WireAbilitySelector(topBar);
            WireOpenLevelMenu(topBar);
            WireSaveLevelMenu(topBar);
            WireAlertBox();
            WireQuit(leftBar);

            fileBrowser = new FileBrowser(this);
            if (verboseLogging) MelonLogger.Msg("filebrowser set");
        }

        // -------------------------------------------------------------------------
        // Individual UI wiring helpers
        // -------------------------------------------------------------------------

        private void WireTileSelector(GameObject leftBar)
        {
            GameObject currentTile = leftBar.transform.GetChild(1).gameObject;
            TMP_Dropdown tileDropdown = currentTile.transform.GetChild(0).GetComponent<TMP_Dropdown>();
            Image tileImage = leftBar.transform.GetChild(0).GetComponent<Image>();

            Drawing.instance.TileDropdown = tileDropdown;
            Drawing.instance.TileImage = tileImage;

            var openWatcher = tileDropdown.gameObject.AddComponent<DropdownOpenWatcher>();
            openWatcher.OnOpened += () =>
            {
                int count = 0;
                foreach (Button b in tileDropdown.transform.GetChild(3).GetComponentsInChildren<Button>())
                {
                    int num = count;
                    b.onClick.RemoveAllListeners();
                    b.onClick.AddListener(() =>
                    {
                        Drawing.instance.RemoveTileFromPalette(num);
                    });
                    count++;
                }
            };

            tileDropdown.onValueChanged.AddListener((int value) =>
            {
                Drawing.instance.selectedTile = value;
                tileImage.sprite = Drawing.instance.tiles[value].tile.sprite;
            });


            tileDropdown.SetValueWithoutNotify(Drawing.instance.selectedTile);
            tileDropdown.RefreshShownValue();
            if (verboseLogging) MelonLogger.Msg("current tile stuff set ");
        }

        private void WireGrid(GameObject leftBar)
        {
            GameObject grid = GameObject.Find("Grid");

            if (verboseLogging) MelonLogger.Msg("grid set");
        }

        private void WireBrushSize(GameObject leftBar)
        {
            GameObject brushSizeGO = leftBar.transform.GetChild(3).gameObject;
            TMP_InputField field = brushSizeGO.transform.GetChild(0).GetComponent<TMP_InputField>();
            field.text = Drawing.instance.brushSize.ToString();
            field.onValueChanged.AddListener((string value) => LockToInt(value, field));
            field.onEndEdit.AddListener((string value) => SetVar(value, ref Drawing.instance.brushSize, field));
            if (verboseLogging) MelonLogger.Msg("brush size field set");
        }

        // Wires an X/Y pair of float input fields to a (float x, float y) tuple field,
        // and moves `target` whenever the position changes.
        private void WireVec2Fields(
            GameObject leftBar,
            int childIndex,
            Func<(float x, float y)> get,
            Action<(float x, float y)> set,
            GameObject target,
            string label)
        {
            GameObject go = leftBar.transform.GetChild(childIndex).gameObject;
            TMP_InputField fx = go.transform.GetChild(0).GetComponent<TMP_InputField>();
            TMP_InputField fy = go.transform.GetChild(1).GetComponent<TMP_InputField>();

            fx.text = get().x.ToString();
            fy.text = get().y.ToString();

            fx.onValueChanged.AddListener((string v) => LockToFloat(v, fx));
            fy.onValueChanged.AddListener((string v) => LockToFloat(v, fy));
            fx.onEndEdit.AddListener((string v) =>
            {
                var cur = get();
                if (float.TryParse(v, out float result)) cur.x = result;
                else fx.text = cur.x.ToString();
                set(cur);
                target.transform.position = new Vector3(get().x, get().y);
            });
            fy.onEndEdit.AddListener((string v) =>
            {
                var cur = get();
                if (float.TryParse(v, out float result)) cur.y = result;
                else fy.text = cur.y.ToString();
                set(cur);
                target.transform.position = new Vector3(get().x, get().y);
            });
            if (verboseLogging) MelonLogger.Msg(label + " stuff fully set");
        }


        private void WireCheckpoints(GameObject leftBar)
        {
            GameObject currentCheckpointGO = leftBar.transform.GetChild(7).gameObject;
            checkpointList = currentCheckpointGO.transform.GetChild(0).GetComponent<TMP_Dropdown>();
            checkpointList.ClearOptions();

            Button add = currentCheckpointGO.transform.GetChild(2).GetComponent<Button>();
            Button remove = currentCheckpointGO.transform.GetChild(1).GetComponent<Button>();
            add.onClick.AddListener(() => AddCheckpoint(checkpointList));
            remove.onClick.AddListener(() => RemoveCheckpoint(checkpointList));

            GameObject checkpointPos = leftBar.transform.GetChild(8).gameObject;
            TMP_InputField cpx = checkpointPos.transform.GetChild(0).GetComponent<TMP_InputField>();
            TMP_InputField cpy = checkpointPos.transform.GetChild(1).GetComponent<TMP_InputField>();
            cpx.text = 0.ToString();
            cpy.text = 0.ToString();

            cpx.onValueChanged.AddListener((string v) => LockToFloat(v, cpx));
            cpy.onValueChanged.AddListener((string v) => LockToFloat(v, cpy));

            cpx.onEndEdit.AddListener((string v) =>
            {
                SetCheckpoint(v, currentCheckpoint, cpx, true);
                if (checkpoints.Count != 0)
                    checkpointGOs[currentCheckpoint].transform.position =
                        new Vector3(checkpoints[currentCheckpoint].x, checkpoints[currentCheckpoint].y);
            });
            cpy.onEndEdit.AddListener((string v) =>
            {
                SetCheckpoint(v, currentCheckpoint, cpy, false);
                if (checkpoints.Count != 0)
                    checkpointGOs[currentCheckpoint].transform.position =
                        new Vector3(checkpoints[currentCheckpoint].x, checkpoints[currentCheckpoint].y);
            });

            checkpointList.onValueChanged.AddListener((int value) =>
            {
                currentCheckpoint = value;
                SetCheckpointActive(value);
                cpx.text = checkpoints[currentCheckpoint].x.ToString();
                cpy.text = checkpoints[currentCheckpoint].y.ToString();
            });
            if (verboseLogging) MelonLogger.Msg("checkpoint stuff fully set");
        }

        private void WireLayerMenu(GameObject leftBar)
        {
            GameObject layerSelect = leftBar.transform.GetChild(4).gameObject;
            LayerManager.instance.SetupVars(layerSelect);

            Button addLayer = layerSelect.transform.GetChild(2).GetComponent<Button>();
            addLayer.onClick.AddListener(() =>
            {
                LayerManager.instance.AddLayer();
            });

            if (verboseLogging) MelonLogger.Msg("layer menu set");
        }

        private void WireBrushSelector(GameObject leftBar)
        {
            GameObject brushSelector = leftBar.transform.GetChild(2).gameObject;

            TMP_Dropdown brushDropdown = brushSelector.transform.GetChild(0).GetComponent<TMP_Dropdown>();
            brushDropdown.options.Clear();
            foreach (Brushes.IBrush brush in Drawing.brushes)
                brushDropdown.options.Add(new TMP_Dropdown.OptionData(brush.GetType().Name));
            brushDropdown.RefreshShownValue();
            brushDropdown.onValueChanged.AddListener((int value) => { Drawing.instance._brush = value; });

            GameObject eraserToggleGO = brushSelector.transform.GetChild(1).gameObject;
            Toggle eraseToggle = eraserToggleGO.transform.GetChild(0).GetComponent<Toggle>();
            Toggle drawToggle = eraserToggleGO.transform.GetChild(1).GetComponent<Toggle>();
            Image eti = eraseToggle.GetComponentInChildren<Image>();
            Image dti = drawToggle.GetComponentInChildren<Image>();

            eraseToggle.onValueChanged.AddListener((bool value) =>
            {
                if (!value) return;
                drawToggle.transform.SetAsLastSibling();
                eti.color = new Color(eti.color.r, eti.color.g, eti.color.b, 1);
                dti.color = new Color(dti.color.r, dti.color.g, dti.color.b, 0);
                Drawing.instance.erasing = false;
            });
            drawToggle.onValueChanged.AddListener((bool value) =>
            {
                if (!value) return;
                eraseToggle.transform.SetAsLastSibling();
                eti.color = new Color(eti.color.r, eti.color.g, eti.color.b, 0);
                dti.color = new Color(dti.color.r, dti.color.g, dti.color.b, 1);
                Drawing.instance.erasing = true;
            });
            if (verboseLogging) MelonLogger.Msg("brush selector set");
        }

        private void WireDimensions(GameObject topBar)
        {
            GameObject levelDimensions = topBar.transform.GetChild(0).gameObject;
            TMP_InputField dx = levelDimensions.transform.GetChild(0).GetComponent<TMP_InputField>();
            TMP_InputField dy = levelDimensions.transform.GetChild(1).GetComponent<TMP_InputField>();

            dx.text = dimensions.x.ToString();
            dy.text = dimensions.y.ToString();

            dx.onValueChanged.AddListener((string v) => LockToFloat(v, dx));
            dy.onValueChanged.AddListener((string v) => LockToFloat(v, dy));

            dx.onEndEdit.AddListener((string v) =>
            {
                (int x, int y) prev = dimensions;
                SetVar(v, ref dimensions, dx, true);
                MelonLogger.Msg("prev: " + prev + " new: " + dimensions);
                RemoveTilesOutsideGrid(prev, dimensions);
                GridMaker.UpdateGrid(dimensions.x, dimensions.y);
            });
            dy.onEndEdit.AddListener((string v) =>
            {
                (int x, int y) prev = dimensions;
                SetVar(v, ref dimensions, dy, false);
                MelonLogger.Msg("prev: " + prev + " new: " + dimensions);
                RemoveTilesOutsideGrid(prev, dimensions);
                GridMaker.UpdateGrid(dimensions.x, dimensions.y);
            });
            if (verboseLogging) MelonLogger.Msg("level dimensions stuff fully set");
        }
        //DOES NOT PROPERLY SET LOCKED OR UNLOCKED WHEN LOADING LEVEL AT LEAST VISUALLY. PLZ FIX @FRENCHYTHEFRY
        private void WireAbilitySelector(GameObject topBar)
        {
            GameObject abilitySelector = topBar.transform.GetChild(1).gameObject;
            TMP_Dropdown abilityDropdown = abilitySelector.transform.GetChild(0).GetComponent<TMP_Dropdown>();
            abilityDropdown.onValueChanged.AddListener((int value) => { ability = (Ability)value; });
            abilityDropdown.SetValueWithoutNotify((int)ability);
            abilityDropdown.RefreshShownValue();
            GameObject lockUnlockGO = abilitySelector.transform.GetChild(1).gameObject;
            Toggle lockToggle = lockUnlockGO.transform.GetChild(0).GetComponent<Toggle>();
            Toggle unlockToggle = lockUnlockGO.transform.GetChild(1).GetComponent<Toggle>();
            Image lti = lockToggle.GetComponentInChildren<Image>();
            Image uti = unlockToggle.GetComponentInChildren<Image>();

            lockToggle.onValueChanged.AddListener((bool value) =>
            {
                if (!value) return;
                unlockToggle.transform.SetAsLastSibling();
                lti.color = new Color(lti.color.r, lti.color.g, lti.color.b, 1);
                uti.color = new Color(uti.color.r, uti.color.g, uti.color.b, 0);
                allowSwap = false;
            });
            unlockToggle.onValueChanged.AddListener((bool value) =>
            {
                if (!value) return;
                lockToggle.transform.SetAsLastSibling();
                lti.color = new Color(lti.color.r, lti.color.g, lti.color.b, 0);
                uti.color = new Color(uti.color.r, uti.color.g, uti.color.b, 1);
                allowSwap = true;
            });
            if (allowSwap)
            {
                unlockToggle.isOn = true;
            }
            if (verboseLogging) MelonLogger.Msg("ability selector set");
        }


        private void WireOpenLevelMenu(GameObject topBar)
        {
            GameObject openOpenLevel = topBar.transform.GetChild(3).gameObject;
            OpenLevelMenu = canvas.transform.GetChild(2).gameObject;
            TMP_Dropdown openLevelSelect = OpenLevelMenu.transform.GetChild(1).GetChild(0).GetComponent<TMP_Dropdown>();

            openOpenLevel.GetComponent<Button>().onClick.AddListener(() =>
            {
                OpenLevelMenu.SetActive(true);
                PropogateLevelSelect(openLevelSelect);
                openLevelSelect.RefreshShownValue();
                allowPanZoom = false;
            });
            OpenLevelMenu.transform.GetChild(2).GetComponent<Button>().onClick.AddListener(() =>
            {
                OpenLevel(openLevelSelect.options[openLevelSelect.value].text);
            });
            OpenLevelMenu.transform.GetChild(4).GetComponent<Button>().onClick.AddListener(() =>
            {
                CreateNewLevel();
            });
            OpenLevelMenu.transform.GetChild(5).GetComponent<Button>().onClick.AddListener(() =>
            {
                OpenLevelMenu.SetActive(false);
                allowPanZoom = true;
            });
            if (verboseLogging) MelonLogger.Msg("open level menu set");
        }

        private void WireSaveLevelMenu(GameObject topBar)
        {
            GameObject openSaveLevel = topBar.transform.GetChild(4).gameObject;
            SaveLevelMenu = canvas.transform.GetChild(3).gameObject;
            TMP_Dropdown saveLevelSelect = SaveLevelMenu.transform.GetChild(1).GetChild(0).GetComponent<TMP_Dropdown>();
            TMP_InputField saveAsBox = SaveLevelMenu.transform.GetChild(3).GetChild(0).GetComponent<TMP_InputField>();

            openSaveLevel.GetComponent<Button>().onClick.AddListener(() =>
            {
                SaveLevelMenu.SetActive(true);
                PropogateLevelSelect(saveLevelSelect);
                saveLevelSelect.RefreshShownValue();
                allowPanZoom = false;
            });
            SaveLevelMenu.transform.GetChild(4).GetComponent<Button>().onClick.AddListener(() =>
            {
                if (saveAsBox.text == "")
                    SaveLevel(saveLevelSelect.options[saveLevelSelect.value].text, true);
                else
                    SaveLevel(saveAsBox.text);
            });
            SaveLevelMenu.transform.GetChild(5).GetComponent<Button>().onClick.AddListener(() =>
            {
                SaveLevelMenu.SetActive(false);
                allowPanZoom = true;
            });
            if (verboseLogging) MelonLogger.Msg("save level menu set");
        }

        private void WireAlertBox()
        {
            GameObject alertMenu = canvas.transform.GetChild(5).gameObject;
            alertMenu.transform.GetChild(2).GetComponent<Button>().onClick.AddListener(() =>
            {
                alertMenu.SetActive(false);
                allowPanZoom = true;
            });
            if (verboseLogging) MelonLogger.Msg("alert box set");
        }

        private void WireQuit(GameObject LeftBar)
        {
            Button CloseButton = LeftBar.transform.GetChild(9).GetComponent<Button>();
            CloseButton.onClick.AddListener(() =>
            {
                SceneManager.LoadScene(0);
            });
        }

        private void LoadTilesAndCheckpoints(string tempDir, Rules rules)
        {
            // Palette images
            foreach (string f in Directory.GetFiles(Path.Combine(tempDir, "Palette")))
                if (new[] { ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(f).ToLower()))
                    Drawing.instance.LoadFilesToPalette(new[] { f });

            // Checkpoints (Win was already handled before SharedInit)
            foreach ((Case c, List<float> p) thing in rules.other)
            {
                if (thing.c == Case.Checkpoint)
                {
                    AddCheckpoint(checkpointList);
                    checkpoints[^1] = (thing.p[0], thing.p[1]);
                    checkpointGOs[^1].transform.position = new Vector3(thing.p[0], thing.p[1]);
                }
            }
            foreach (string f in Directory.GetFiles(Path.Combine(tempDir, "Layers")))
            {
                MelonLogger.Msg(f);
                JsonLayer temp = JsonConvert.DeserializeObject<JsonLayer>(File.ReadAllText(f));
                LayerManager.instance.AddLayer(temp, Drawing.instance);
            }
        }
        public static void LockToFloat(string value, TMP_InputField field)
        {
            //MelonLogger.Msg("locking");
            string filtered = Regex.Replace(value, @"[^0-9.,-]", "");
            if (filtered != value)
                field.SetTextWithoutNotify(filtered);
        }
        public static void LockToInt(string value, TMP_InputField field)
        {
            //MelonLogger.Msg("locking");
            string filtered = Regex.Replace(value, @"[^0-9-]", "");
            if (filtered != value)
                field.SetTextWithoutNotify(filtered);
        }
        public static void SetVar(string value, ref (float x, float y) pos, TMP_InputField field, bool isX)
        {
            if (isX)
            {
                float temp = 0;
                if (float.TryParse(value, out temp))
                {
                    pos.x = temp;
                }
                else
                {
                    field.SetTextWithoutNotify(pos.x.ToString());
                }
            }
            else
            {
                float temp = 0;
                if (float.TryParse(value, out temp))
                {
                    pos.y = temp;
                }
                else
                {
                    field.SetTextWithoutNotify(pos.y.ToString());
                }
            }
        }
        public static void SetVar(string value, ref (int x, int y) dimension, TMP_InputField field, bool isX)
        {
            if (isX)
            {
                int temp = 0;
                if (int.TryParse(value, out temp))
                {
                    dimension.x = temp;
                }
                else
                {
                    field.SetTextWithoutNotify(dimension.x.ToString());
                }
            }
            else
            {
                int temp = 0;
                if (int.TryParse(value, out temp))
                {
                    dimension.y = temp;
                }
                else
                {
                    field.SetTextWithoutNotify(dimension.y.ToString());
                }
            }
        }
        public static void SetVar(string value, ref float var, TMP_InputField field)
        {
            float temp = 0;
            if (float.TryParse(value, out temp))
            {
                var = temp;
            }
            else
            {
                field.SetTextWithoutNotify(var.ToString());
            }
        }
        public static void SetVar(string value, ref int var, TMP_InputField field)
        {
            int temp = 0;
            if (int.TryParse(value, out temp))
            {
                var = temp;
            }
            else
            {
                field.SetTextWithoutNotify(var.ToString());
            }
        }
        public void SetCheckpoint(string value, int index, TMP_InputField field, bool isX)
        {

            float temp = 0;
            if (!isX)
            {
                if (index == -1)
                {
                    field.SetTextWithoutNotify(0.ToString());
                    return;
                }
                if (float.TryParse(value, out temp))
                {
                    checkpoints[index] = (checkpoints[index].x, temp);
                }
                else
                {
                    field.SetTextWithoutNotify(checkpoints[index].y.ToString());
                }
            }
            else
            {
                if (index == -1)
                {
                    field.SetTextWithoutNotify(0.ToString());
                    return;
                }
                if (float.TryParse(value, out temp))
                {
                    checkpoints[index] = (temp, checkpoints[index].y);
                }
                else
                {
                    field.SetTextWithoutNotify(checkpoints[index].x.ToString());
                }
            }
        }
        public static Sprite LoadSpriteFromEmbeddedResource(string resourceName, float pixelsPerUnit = 100f)
        {
            // Get the calling assembly (your mod's assembly)
            Assembly assembly = Assembly.GetCallingAssembly();

            // Find the full resource name (handles partial matches)
            string fullResourceName = null;
            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (name.EndsWith(resourceName))
                {
                    fullResourceName = name;
                    break;
                }
            }

            if (fullResourceName == null)
            {
                MelonLoader.MelonLogger.Error($"[LoadSprite] Resource not found: {resourceName}");
                return null;
            }

            // Read the resource stream into a byte array
            using (Stream stream = assembly.GetManifestResourceStream(fullResourceName))
            {
                if (stream == null)
                {
                    MelonLoader.MelonLogger.Error($"[LoadSprite] Failed to open stream for: {fullResourceName}");
                    return null;
                }

                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);

                // Load bytes into a Texture2D
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, data))
                {
                    MelonLoader.MelonLogger.Error($"[LoadSprite] Failed to decode image: {fullResourceName}");
                    return null;
                }

                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;

                // Create and return the sprite
                return Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), // centered pivot
                    pixelsPerUnit
                );
            }
        }
        public static void UpdateObjPos(GameObject obj, ref (float x, float y) pos)
        {
            obj.transform.position = new Vector2(pos.x, pos.y);
        }
        public void AddCheckpoint(TMP_Dropdown dropdown)
        {
            dropdown.options.Add(new TMP_Dropdown.OptionData("#" + checkpoints.Count));
            checkpoints.Add((0, 0));
            dropdown.value = checkpoints.Count - 1;
            //MelonLogger.Msg(dropdown.value);
            currentCheckpoint = dropdown.value;
            dropdown.RefreshShownValue();
            GameObject cp = GameObject.Instantiate(Melon<Program>.Instance.checkpoint);
            cp.GetComponent<CheckpointController>().enabled = false;
            cp.transform.position = Vector3.zero;
            checkpointGOs.Add(cp);
            SetCheckpointActive(currentCheckpoint);
        }
        public void RemoveCheckpoint(TMP_Dropdown dropdown)
        {
            if (checkpoints.Count != 0)
            {
                int lastIndex = checkpoints.Count - 1;

                dropdown.options.RemoveAt(lastIndex);
                checkpoints.RemoveAt(lastIndex);
                GameObject.Destroy(checkpointGOs[lastIndex]);
                checkpointGOs.RemoveAt(lastIndex);

                if (dropdown.value >= checkpoints.Count)
                {
                    currentCheckpoint = checkpoints.Count - 1;
                    SetCheckpointActive(currentCheckpoint);
                    dropdown.value = checkpoints.Count - 1;
                    dropdown.RefreshShownValue();
                }
            }
        }
        public void SetCheckpointActive(int value)
        {
            for (int i = 0; i < checkpointGOs.Count; i++)
            {
                if (value != i)
                {
                    //MelonLogger.Msg(checkpointGOs[i] != null);
                    SetObjBrightness(checkpointGOs[i], false);
                }
                else
                {
                    //MelonLogger.Msg(checkpointGOs[i] != null);
                    SetObjBrightness(checkpointGOs[i]);
                }
            }
        }

        //NEVER TOUCH FUNCTION AGAIN IT BARELY WORKS AS IS
        public void SetObjBrightness(GameObject target, bool high = true)
        {
            var mat = high ? normalCheckpointMat : darkCheckpointMat;
            var color = mat.GetColor("Color_10217c9d92e948b9b176775f87c2a94b");

            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("Color_10217c9d92e948b9b176775f87c2a94b", color);
                renderer.material = mat;
                renderer.SetPropertyBlock(block);
            }
        }
        public static void PropogateLevelSelect(TMP_Dropdown dropdown)
        {
            dropdown.ClearOptions();
            string path = Path.Combine(MelonUtils.GameDirectory, "Levels");
            foreach (string file in Directory.GetFiles(path))
            {
                if (Path.GetExtension(file) == ".tjl")
                {
                    dropdown.options.Add(new TMP_Dropdown.OptionData(Path.GetFileNameWithoutExtension(file)));
                }
            }
        }
        public void RemoveTilesOutsideGrid((int x, int y) previous, (int x, int y) nue)
        {
            for (int j = previous.y / -2; j <= previous.y / 2; j++)
            {
                for (int i = previous.x / -2; i <= previous.x / 2; i++)
                {
                    if (i < nue.x / -2 || i > nue.x / 2 || j < nue.y / -2 || j > nue.y / 2)
                    {
                        foreach (Layer t in LayerManager.instance.layers)
                        {
                            t.map.SetTile(new Vector3Int(i, j), null);
                        }
                    }
                }
            }
        }
        public void OpenLevel(string levelName)
        {
            Melon<Program>.Instance.LoadEditor(levelName);
        }
        public void CreateNewLevel()
        {
            Melon<Program>.Instance.LoadEditor();
        }
        public void SaveLevel(string levelName, bool overwrite = false)
        {
            string fullpath = Path.Combine(MelonUtils.GameDirectory, "Levels", levelName + ".tjl");
            if (!Directory.Exists(Path.Combine(MelonUtils.GameDirectory, "Levels")))
            {
                Directory.CreateDirectory(Path.Combine(MelonUtils.GameDirectory, "Levels"));
            }
            Rules rules = new Rules(allowSwap, (int)ability, playerPos, dimensions, levelName, new List<(Case, List<float>)>()
            {
                (Case.Win, new List<float>(){factoryPos.x, factoryPos.y})
            });
            foreach ((float x, float y) in checkpoints)
            {
                rules.other.Add((Case.Checkpoint, new List<float>() { x, y }));
            }
            string tempDir = Path.Combine(System.IO.Path.GetTempPath(), levelName);
            Directory.CreateDirectory(tempDir);
            string layerDir = Path.Combine(tempDir, "Layers");
            Directory.CreateDirectory(layerDir);
            for (int k = 0; k < LayerManager.instance.layers.Count; k++)
            {
                JsonLayer TP = new JsonLayer(LayerManager.instance.layers[k]);
                string tileposPath = Path.Combine(layerDir, TP.name + ".json");
                string tileposJson = Newtonsoft.Json.JsonConvert.SerializeObject(TP);
                File.WriteAllText(tileposPath, tileposJson);
            }
            string rulesPath = Path.Combine(tempDir, "rules.json");
            string rulesJson = Newtonsoft.Json.JsonConvert.SerializeObject(rules);
            try
            {
                JsonConvert.DeserializeObject<Rules>(rulesJson);
            }
            catch (Exception e)
            {
                MelonLogger.Error("rules serialized wrong");
            }
            File.WriteAllText(rulesPath, rulesJson);
            int count = 0;
            Directory.CreateDirectory(Path.Combine(tempDir, "Palette"));
            foreach ((string path, Tile t) in Drawing.instance.tiles)
            {
                SaveToPng(t.sprite, Path.Combine(tempDir, "Palette", count + Path.GetExtension(path)));
                count++;
            }
            if (!overwrite && File.Exists(fullpath))
            {
                ShowError("File already exists: " + levelName);
                Directory.Delete(tempDir, true);
                return;
            }
            if (overwrite && File.Exists(fullpath))
            {
                File.Delete(fullpath);
            }
            ZipFile.CreateFromDirectory(tempDir, fullpath);
            Directory.Delete(tempDir, true);
            SaveLevelMenu.SetActive(false);
            allowPanZoom = true;
        }
        public static void SaveToPng(Texture2D texture, string filePath)
        {
            if (texture == null)
            {
                MelonLoader.MelonLogger.Error("Texture is null.");
                return;
            }

            // If the texture isn't readable, blit it to a readable RenderTexture first
            Texture2D readable = EnsureReadable(texture);

            byte[] pngBytes = readable.EncodeToPNG();

            if (readable != texture)
                GameObject.Destroy(readable);

            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(filePath, pngBytes);
            MelonLoader.MelonLogger.Msg($"[TextureUtils] Saved texture to: {filePath}");
        }
        public static void SaveToPng(Sprite sprite, string filePath)
        {
            if (sprite == null)
            {
                MelonLoader.MelonLogger.Error("[TextureUtils] Sprite is null.");
                return;
            }

            Texture2D readable = EnsureReadable(sprite.texture);

            Rect texRect = sprite.textureRect;
            Rect fullRect = sprite.rect;
            Vector2 offset = sprite.textureRectOffset;

            int fullW = (int)fullRect.width;
            int fullH = (int)fullRect.height;
            int cropX = (int)texRect.x;
            int cropY = (int)texRect.y;
            int cropW = (int)texRect.width;
            int cropH = (int)texRect.height;
            int padX = (int)offset.x;
            int padY = (int)offset.y;

            // Get the tight pixels from the atlas
            Color[] tightPixels = readable.GetPixels(cropX, cropY, cropW, cropH);
            if (readable != sprite.texture)
                GameObject.Destroy(readable);

            // Create full-size texture, filled with transparent black
            Texture2D result = new Texture2D(fullW, fullH, TextureFormat.RGBA32, false);
            Color[] empty = new Color[fullW * fullH]; // defaults to (0,0,0,0)
            result.SetPixels(empty);

            // Stamp the tight region at the correct offset
            result.SetPixels(padX, padY, cropW, cropH, tightPixels);
            result.Apply();

            SaveToPng(result, filePath);
            GameObject.Destroy(result);
        }
        private static Texture2D EnsureReadable(Texture2D source)
        {
            // Already readable — return as-is
            if (source.isReadable)
                return source;

            // Blit to a temporary RenderTexture so we can read pixels
            RenderTexture rt = RenderTexture.GetTemporary(
                source.width, source.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

            Graphics.Blit(source, rt);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            return copy;
        }
        public void ShowError(string error)
        {
            GameObject AlertBox = canvas.transform.GetChild(5).gameObject;
            for (int i = 2; i <= 4; i++)
            {
                canvas.transform.GetChild(i).gameObject.SetActive(false);
            }
            AlertBox.SetActive(true);
            AlertBox.transform.GetChild(0).GetComponent<TMP_Text>().text = "Error";
            AlertBox.transform.GetChild(1).GetComponent<TMP_Text>().text = error;
            allowPanZoom = false;
        }
        public static void PrintPos((float x, float y) pos)
        {
            MelonLogger.Msg("( " + pos.x + ", " + pos.y + " )");
        }
        public Vector2 lastMousePosition;
        public bool isPanning;
        public void Update()
        {
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.Z))
            {
                UndoManager.instance.Undo();
            }
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.Y))
            {
                UndoManager.instance.Redo();
            }
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.Z))
            {
                UndoManager.instance.Redo();
            }
            try
            {
                if (!allowPanZoom)
                {
                    isPanning = false;
                    return;
                }
                if (!Drawing.IsMouseOverBars())
                {
                    cam.orthographicSize += Input.mouseScrollDelta.y * -scrollSpeed * cam.orthographicSize / 15;
                    cam.orthographicSize = Math.Max(cam.orthographicSize, 5);
                    cam.orthographicSize = Math.Min(cam.orthographicSize, 200);
                }
                if (Input.GetMouseButtonDown(1))
                {
                    lastMousePosition = cam.ScreenToWorldPoint(Input.mousePosition);
                    isPanning = true;
                }
                if (Input.GetMouseButton(1) && isPanning)
                {
                    Vector3 vector = cam.ScreenToWorldPoint(Input.mousePosition) - cam.transform.position;
                    Vector3 position = lastMousePosition - (Vector2)vector;
                    cam.transform.position = new Vector3(position.x, position.y, -10);
                }
                if (Input.GetMouseButtonUp(1))
                {
                    isPanning = false;
                }
            }
            catch
            {
                return;
            }
        }
    }
    public class DropdownOpenWatcher : MonoBehaviour, IPointerClickHandler
    {
        public Action OnOpened;
        public void OnPointerClick(PointerEventData eventData)
        {
            OnOpened?.Invoke();
        }
    }
    public enum Ability
    {
        Lightning,
        Glider,
        Pogo,
        Gun
    }
}