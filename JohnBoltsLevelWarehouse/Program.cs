
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using MelonLoader;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;
using System.Reflection;
using System.Collections;
using System.IO.Compression;
using UnityEngine.UI;
using TMPro;
using HarmonyLib;
using Newtonsoft.Json;
using JohnBoltsLevelWarehouse.Editor;
using System.Security.Cryptography;

[assembly: MelonInfo(typeof(JohnBoltsLevelWarehouse.Program), "John Bolt's Level Warehouse", "3.0.0", "Frenchy")]
[assembly: MelonGame("Grouch", "Thunder Jumper")]
[assembly: VerifyLoaderVersion(0, 5, 7)]
[assembly: MelonAuthorColor(ConsoleColor.Cyan)]
[assembly: MelonColor(ConsoleColor.Blue)]
[assembly: MelonOptionalDependencies("ReplayMod")]
//TO DO: 1. Fix entering after doing hardcore 2. Create custom end screen 3. Save best times and bolts? in playerprefs?
namespace JohnBoltsLevelWarehouse
{
    public class Program : MelonMod
    {
        public List<UnityEngine.Tilemaps.Tile> pallete = new List<UnityEngine.Tilemaps.Tile>();
        public Tilemap tileMap; // Grid > Tilemap GameObject
        public Tilemap InvisWallsDeath;
        public int width;
        public int height;
        public GameObject win;
        Tile invisibleSolidTile;
        public GameObject checkpoint;
        public AssetBundle objBundle;
        public AssetBundle levBundle;
        public AssetBundle levPrefBundle;
        public static GameObject buttonPrefab;
        public static GameObject levelSelectPrefab;
        public static GameObject paintingPrefab;
        public string editorScene;
        public static bool allowSwap;
        public static bool MDHasLoaded;
        public string dir;
        public string archiveLoc;
        public string tempfileloc;
        public Editor.LevelEditorFrontend editorFrontend;
        bool replayModPresent = false;
        public override void OnApplicationStart()
        {
            var targetType = AccessTools.TypeByName("ReplayMod.MainClass");
            if (targetType == null) return; // mod not loaded

            var original = AccessTools.Method(targetType, "UpdateReplay");
            var patch = new HarmonyMethod(typeof(FixReplayModUpdate), nameof(FixReplayModUpdate.Prefix));
            HarmonyInstance.Patch(original, prefix: patch);

            var original2 = AccessTools.Method(targetType, "LoadReplay");
            var patch2 = new HarmonyMethod(typeof(FixReplayModLoad), nameof(FixReplayModLoad.Prefix));
            HarmonyInstance.Patch(original2, prefix: patch2);
        }
        public override void OnInitializeMelon()
        {
            invisibleSolidTile = ScriptableObject.CreateInstance<Tile>();
            invisibleSolidTile.sprite = null;
            objBundle = LoadEmbeddedBundle("JohnBoltsLevelWarehouse.Resources.objects.bundle");
            win = objBundle.LoadAsset<GameObject>("Win");
            checkpoint = objBundle.LoadAsset<GameObject>("Checkpoint");
            //DO NOT TOUCH
            Editor.LevelEditorFrontend.normalCheckpointMat = new Material(checkpoint.transform.GetChild(1).GetComponentInChildren<MeshRenderer>().sharedMaterial);
            Editor.LevelEditorFrontend.darkCheckpointMat = new Material(Editor.LevelEditorFrontend.normalCheckpointMat);
            var c = Editor.LevelEditorFrontend.darkCheckpointMat.GetColor("Color_10217c9d92e948b9b176775f87c2a94b");
            Editor.LevelEditorFrontend.darkCheckpointMat.SetColor("Color_10217c9d92e948b9b176775f87c2a94b",
                new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, c.a));
            //END OF DO NOT TOUCH
            levPrefBundle = LoadEmbeddedBundle("JohnBoltsLevelWarehouse.Resources.leveleditorprefabs.bundle");
            AssetBundle editorBundle = LoadEmbeddedBundle("JohnBoltsLevelWarehouse.Resources.leveleditor.bundle");
            string[] scenes = editorBundle.GetAllScenePaths();
            string scene = Path.GetFileNameWithoutExtension(scenes[0]);
            editorScene = scene;
            Editor.FileBrowser.FolderContentsItem = levPrefBundle.LoadAsset<GameObject>("FolderContentsItem");
            Editor.FileBrowser.FolderPickerItem = levPrefBundle.LoadAsset<GameObject>("FolderPickerItem");
            FileBrowser.FileExplorerPrefab = levPrefBundle.LoadAsset<GameObject>("FileExplorer");
            Editor.Layer.tilemapPrefab = levPrefBundle.LoadAsset<GameObject>("Tilemap");
            Editor.LayerManager.layerUIPrefab = levPrefBundle.LoadAsset<GameObject>("LayerPickerItem");
            buttonPrefab = levPrefBundle.LoadAsset<GameObject>("Buttons");
            levelSelectPrefab = levPrefBundle.LoadAsset<GameObject>("OpenLevel");
            paintingPrefab = levPrefBundle.LoadAsset<GameObject>("Painting");

            if (!Directory.Exists(Path.Combine(MelonUtils.GameDirectory, "Levels")))
            {
                Directory.CreateDirectory(Path.Combine(MelonUtils.GameDirectory, "Levels"));
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "Moonlight District")
            {
                //MelonLogger.Msg("MD Loaded, running");
                MelonCoroutines.Start(WaitForSceneLoaded(() =>
                {
                    MelonLogger.Msg("MD Loaded");
                    DoMD();
                }));
            }
            if (buildIndex == 0)
            {
                CreateUI();
            }
        }
        public override void OnUpdate()
        {
            /*if (Input.GetKeyDown(KeyCode.Insert))
            {
                string input = "level";
                //MelonLogger.Msg($"Input received: {input}");
                OpenLevel(input);
            }
            if (Input.GetKeyDown(KeyCode.Home))
            {
                LoadEditor();
            }*/
            if (SceneManager.GetActiveScene().name == "LevelEditor" && SceneManager.GetActiveScene().isLoaded)
            {
                if (editorFrontend != null)
                {
                    editorFrontend.Update();
                    Drawing.instance.Update();
                }
            }
        }
        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (buildIndex == 0)
            {
                //UIHelper ui = new UIHelper();
                //ui.Button(200, 100, 0, 0, Color.white, () => testFileBrowser(ui), "test", 36, Color.black);
            }
        }
        public void LoadEditor()
        {
            SceneManager.LoadScene(editorScene);
            MelonCoroutines.Start(WaitForSceneLoaded(() =>
            {
                MelonLogger.Msg("Editor Loaded");
                editorFrontend = new Editor.LevelEditorFrontend();
            }));
        }
        public void LoadEditor(string levelToLoad)
        {
            SceneManager.LoadScene(editorScene);
            MelonCoroutines.Start(WaitForSceneLoaded(() =>
            {
                MelonLogger.Msg("Editor Loaded");
                editorFrontend = new Editor.LevelEditorFrontend(levelToLoad);
            }));
        }
        public static IEnumerator WaitForSceneLoaded(UnityEngine.Events.UnityAction onLoaded)
        {
            yield return null;
            while (!SceneManager.GetActiveScene().isLoaded)
                yield return null;

            yield return null; // one frame buffer instead of WaitForEndOfFrame

            MelonLogger.Msg("Scene loaded, invoking callback");
            onLoaded?.Invoke();
        }
        /*private IEnumerator WaitAndDoMD()
        {
            // Wait for 1 second
            yield return new WaitForSeconds(0.5f);
            SetAll(input);
            GameObject pd = GameObject.Find("Death Collider");
            PlayerDeath pds = pd.GetComponent<PlayerDeath>();
            pds.DieSilent();
        }*/
        private void DoMD()
        {
            FixAbilityWheel.hasBeenSet = false;
            SetAll();
            GameObject pd = GameObject.Find("Death Collider");
            PlayerDeath pds = pd.GetComponent<PlayerDeath>();
            pds.DieSilent();
            CleanupTemporaryDirectory(tempfileloc);
        }
        private void CreateUI()
        {
            GameObject canvasObject = null;
            GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>(true);
            foreach (GameObject obj in allObjects)
            {
                if (obj.name == "WORLD SELECT")
                {
                    canvasObject = obj;
                    RectTransform rect1 = obj.transform.GetChild(5).GetComponent<RectTransform>();
                    rect1.position = new Vector2(rect1.position.x + 1.5f, rect1.position.y);
                    RectTransform rect4 = obj.transform.GetChild(8).GetComponent<RectTransform>();
                    rect4.position = new Vector2(rect4.position.x - 1.5f, rect4.position.y);
                    RectTransform rect2 = obj.transform.GetChild(6).GetComponent<RectTransform>();
                    rect2.position = new Vector2(rect2.position.x - 0.25f, -2.5f);
                    RectTransform rect3 = obj.transform.GetChild(7).GetComponent<RectTransform>();
                    rect3.position = new Vector2(rect3.position.x + 0.25f, -2.5f);
                    break;
                }
            }


            GameObject painting = GameObject.Instantiate(paintingPrefab, canvasObject.transform);
            RectTransform paintingRect = painting.GetComponent<RectTransform>();
            paintingRect.position = new Vector2(0, 1.35f);
            paintingRect.localScale = new Vector2(1.75f, 1.75f);

            GameObject Buttons = GameObject.Instantiate(buttonPrefab, canvasObject.transform);
            RectTransform buttonRect = Buttons.GetComponent<RectTransform>();
            buttonRect.localScale = new Vector2(0.6f, 0.6f);

            GameObject expobj = GameObject.Instantiate(FileBrowser.FileExplorerPrefab, canvasObject.transform);
            RectTransform expTransform = expobj.GetComponent<RectTransform>();
            expTransform.localScale = new Vector2(0.5f, 0.5f);
            FileBrowser fileBrowser = new FileBrowser(expobj, "Import Level");

            GameObject SelectorObj = GameObject.Instantiate(levelSelectPrefab, canvasObject.transform);
            RectTransform SelectTransform = SelectorObj.GetComponent<RectTransform>();
            SelectTransform.localScale = new Vector2(0.5f, 0.5f);

            Button Close = SelectTransform.GetChild(0).GetComponent<Button>();
            Close.onClick.AddListener(() =>
            {
                SelectorObj.SetActive(false);
            });

            GameObject LevelSelectParent = SelectTransform.GetChild(1).gameObject;
            TMP_Dropdown LevelDropdown = LevelSelectParent.transform.GetChild(1).GetComponent<TMP_Dropdown>();
            LevelEditorFrontend.PropogateLevelSelect(LevelDropdown);

            painting.transform.GetChild(0).GetComponent<Button>().onClick.AddListener(() =>
            {
                Buttons.SetActive(true);
            });

            Button OpenInEditorButton = SelectTransform.GetChild(2).GetComponent<Button>();
            OpenInEditorButton.onClick.AddListener(() =>
            {
                LoadEditor(LevelDropdown.options[LevelDropdown.value].text);
            });

            Button PlayButton = SelectTransform.GetChild(3).GetComponent<Button>();
            PlayButton.onClick.AddListener(() =>
            {
                OpenLevel(LevelDropdown.options[LevelDropdown.value].text);
            });

            SelectorObj.SetActive(false);


            Button OpenLevelEditor = buttonRect.GetChild(0).GetComponent<Button>();
            OpenLevelEditor.onClick.AddListener(() =>
            {
                LoadEditor();
            });

            Button OpenLevelSelect = buttonRect.GetChild(1).GetComponent<Button>();
            OpenLevelSelect.onClick.AddListener(() =>
            {
                LevelEditorFrontend.PropogateLevelSelect(LevelDropdown);
                SelectorObj.SetActive(true);
            });

            Button ImportLevel = buttonRect.GetChild(2).GetComponent<Button>();
            ImportLevel.onClick.AddListener(async () =>
            {
                string[] files = await fileBrowser.OpenFilePicker(new string[] { ".tjl" }, Environment.GetFolderPath(Environment.SpecialFolder.Recent), true);
                foreach (string file in files)
                {
                    if (Path.GetExtension(file).ToLower() == ".tjl")
                    {
                        File.Copy(file, Path.Combine(MelonUtils.GameDirectory, "Levels", Path.GetFileName(file)), true);
                    }
                }
            });

            Button ButtonClose = buttonRect.GetChild(3).GetComponent<Button>();
            ButtonClose.onClick.AddListener(() =>
            {
                Buttons.SetActive(false);
            });
        }
        public void OpenLevel(string userInput)
        {
            MDHasLoaded = false;
            dir = Path.Combine(MelonUtils.BaseDirectory, "Levels", (userInput + ".tjl"));
            archiveLoc = dir;
            if (!File.Exists(dir) && !Directory.Exists(dir))
            {
                MelonLogger.Error("Level doesn't exist!");
                return;
            }
            if (dir.EndsWith(".tjl", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"Extracting archive: {dir}");
                dir = ExtractArchive(dir);
                tempfileloc = dir;
                Debug.Log($"Archive extracted to: {dir}");
            }
            if (!File.Exists(dir))
                dir = dir.Trim('\"');

            MelonLogger.Msg(dir);
            pallete = LoadPaletteFromFile(dir);
            Rules rules = LoadRules();
            width = rules.dimensions.width;
            height = rules.dimensions.height;
            StartLoad();
        }

        public int getAllowedAbilities(int val)
        {
            if (val == 1)
            {
                return 2;
            }
            if (val == 2)
            {
                return 5;
            }
            if (val == 3)
            {
                return 3;
            }
            return 0;
        }
        public Rules LoadRules()
        {
            string modsFolder = dir;
            string filePath = Path.Combine(modsFolder, "rules.json");
            Rules temp;
            try
            {
                temp = Newtonsoft.Json.JsonConvert.DeserializeObject<Rules>(File.ReadAllText(filePath));
            }
            catch (Exception e)
            {
                MelonLogger.Error("Error when deserializing json for rules. Aborting...");
                Abort();
                return null;
            }
            return temp;
        }
        public void SetStartPos(Vector2 pos)
        {
            GameObject pd = GameObject.Find("Death Collider");
            PlayerDeath pds = pd.GetComponent<PlayerDeath>();
            var field = typeof(PlayerDeath).GetField("playerStartPos", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(pds, pos);
        }

        private int GetNumericPart(string filename)
        {
            // Extract numeric part from the filename
            string numericPart = new string(filename.Where(char.IsDigit).ToArray());
            return int.TryParse(numericPart, out int result) ? result : int.MaxValue; // Non-numeric names are sorted last
        }
        public List<Tile> LoadPaletteFromFile(string folderName)
        {
            // Define the path to the Palette folder inside the specified folder in the mods directory
            string modsFolder = Path.Combine(folderName, "Palette");

            // Create a list to hold the tiles
            List<Tile> palette = new List<Tile>();
            List<string> pName = new List<string>();
            Tile emptyTile = ScriptableObject.CreateInstance<Tile>();
            emptyTile.sprite = null; // No sprite for the empty tile
            palette.Add(emptyTile);

            // Check if the folder exists
            if (Directory.Exists(modsFolder))
            {
                // Get all image files in the folder (e.g., PNG, JPG)
                string[] imageFiles = Directory.GetFiles(modsFolder, "*.*", SearchOption.TopDirectoryOnly);

                // Filter and sort files by name
                imageFiles = imageFiles
                    .Where(filePath =>
                        filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                        filePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        filePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(filePath => GetNumericPart(Path.GetFileNameWithoutExtension(filePath)))
                    .ToArray();


                foreach (string filePath in imageFiles)
                {
                    try
                    {
                        //MelonLogger.Msg($"Loading file: {filePath} to slot " + palette.Count);
                        byte[] fileData = File.ReadAllBytes(filePath);
                        Texture2D texture = new Texture2D(2, 2);
                        texture.LoadImage(fileData); // Ensure the texture is populated
                        texture.filterMode = FilterMode.Point;
                        texture.wrapMode = TextureWrapMode.Clamp;
                        //MelonLogger.Msg($"Texture size: {texture.width}x{texture.height}");
                        //float pixelsPerUnit = texture.width; // Use a constant or configurable value

                        // Create a Sprite from the Texture2D
                        Rect rect = new Rect(0, 0, texture.width, texture.height);
                        Sprite sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f));
                        sprite = Editor.Drawing.ScaleSpriteToUnitSize(sprite);
                        // Create a Tile from the Sprite
                        Tile tile = ScriptableObject.CreateInstance<Tile>();
                        tile.sprite = sprite;

                        // Add the tile to the palette list
                        palette.Add(tile);
                        pName.Add(filePath);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"Failed to load image file: {filePath}, Error: {ex.Message}");
                    }
                }
            }
            else
            {
                MelonLogger.Warning($"Palette folder '{modsFolder}' not found.");
            }
            //MelonLogger.Msg(palette.Count);
            MelonLogger.Msg(string.Join(",", pName));
            return palette;
        }
        public void ClearAllTilesManually(string name)
        {
            //MelonLogger.Msg("ClearAllTilesManually method called.");

            // Try to find the Tilemap GameObject
            GameObject tilemapGameObject = GameObject.Find(name);

            if (tilemapGameObject == null)
            {
                MelonLogger.Error("Tilemap GameObject not found.");
                return;
            }

            // Try to get the Tilemap component
            tileMap = tilemapGameObject.GetComponent<Tilemap>();

            if (tileMap == null)
            {
                MelonLogger.Error("Tilemap component not found on Tilemap GameObject.");
                return;
            }

            // Iterate over all tile positions and set them to null
            BoundsInt bounds = tileMap.cellBounds;
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    for (int z = bounds.zMin; z < bounds.zMax; z++)
                    {
                        tileMap.SetTile(new Vector3Int(x, y, z), null);
                    }
                }
            }

            tileMap.RefreshAllTiles();
            //MelonLogger.Msg("All tiles cleared manually and Tilemap refreshed.");
        }
        public void SetAll()
        {
            //MelonLogger.Msg("SetAll method called.");
            Rules rules = LoadRules();
            SetStartPos(new Vector2(rules.startPos.x, rules.startPos.y));
            ShiftController pp = UnityEngine.Object.FindObjectOfType<ShiftController>();
            pp.activeAbility = getAllowedAbilities(rules.allowedAbility);
            if (rules.allowSwap == false)
            {
                pp.forceAbility = getAllowedAbilities(rules.allowedAbility);
                allowSwap = false;
            }
            else
            {
                pp.forceAbility = -1;
                allowSwap = true;
            }
            for (int i = 0; i < rules.other.Count; i++)
            {
                ProcessLine(rules.other[i]);
            }
            GameObject tilemapGameObject = GameObject.Find("Tilemap Gameobject");
            GameObject coverT = GameObject.Find("Ground Cover");
            GameObject coverTM = GameObject.Find("Ground Cover Metal");
            if (tilemapGameObject == null)
            {
                MelonLogger.Error("Tilemap GameObject not found.");
                return;
            }
            ClearAllTilesManually("Tilemap Gameobject");
            ClearAllTilesManually("Ground Cover");
            ClearAllTilesManually("Ground Cover Metal");

            // Try to get the Tilemap component
            tileMap = tilemapGameObject.GetComponent<Tilemap>();
            Tilemap gct = coverT.GetComponent<Tilemap>();
            Tilemap gctm = coverTM.GetComponent<Tilemap>();

            if (tileMap == null)
            {
                MelonLogger.Error("Tilemap component not found on Tilemap GameObject.");
                return;
            }

            while (tilemapGameObject.transform.childCount > 0)
            {
                GameObject.DestroyImmediate(tilemapGameObject.transform.GetChild(0).gameObject);
            }
            PrepMD();
            GroundCoverController gcc = coverT.GetComponent<GroundCoverController>();
            GroundCoverController gccm = coverTM.GetComponent<GroundCoverController>();
            SetAllLayers(gct, gctm, gcc, gccm);
            SetDeathPlane();
            tileMap.gameObject.transform.position = new Vector2(-0.5f, -0.5f);
            GameObject p = GameObject.Find("Player B3");
            //MelonLogger.Msg("After player objkect");
            Rigidbody2D prb = p.GetComponent<Rigidbody2D>();
            //MelonLogger.Msg("After player rb");
            prb.gravityScale = 2;
            //MelonLogger.Msg(prb.gravityScale);
        }
        public void SetAllLayers(Tilemap gct, Tilemap gctm, GroundCoverController gcc, GroundCoverController gccm)
        {
            foreach (string f in Directory.GetFiles(Path.Combine(dir, "Layers")))
            {
                JsonLayer layer = JsonConvert.DeserializeObject<JsonLayer>(File.ReadAllText(f));
                switch (layer.type)
                {
                    case LayerType.Normal:
                        SetNormal(gct, gcc, layer.map);
                        MelonLogger.Msg("Set Normal");
                        break;
                    case LayerType.NormalGrapple:
                        SetMetal(gctm, gccm, layer.map);
                        MelonLogger.Msg("Set Metal");
                        break;
                    case LayerType.Deadly:
                        SetDeath(layer.map);
                        MelonLogger.Msg("Set Death");
                        break;
                    case LayerType.DeadlyGrapple:
                        SetDeathMetal(layer.map);
                        MelonLogger.Msg("Set Death Grapple");
                        break;
                    case LayerType.Invisible:
                        SetInvis(gct, gcc, layer.map);
                        break;
                    case LayerType.NoColl:
                        SetNoColl(layer.map);
                        break;
                }
            }
        }
        public void SetNormal(Tilemap gct, GroundCoverController gcc, TilePos tilePos)
        {
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    if (i < tilePos.tiles.Count && j < tilePos.tiles[i].Count)
                    {
                        if (tilePos.tiles[i][j] != 0)
                        {
                            tileMap.SetTile(new Vector3Int(j - width / 2, height / 2 - i, 0), pallete[tilePos.tiles[i][j]]);
                            //MelonLogger.Msg("tiles i j is " + tilePos.tiles[i][j]);
                            //MelonLogger.Msg($"Placing tile {pallete[tilePos.tiles[i][j]]} at position ({j}, {i})");
                            gct.SetTile(new Vector3Int(j - width / 2, height / 2 - i, 0), gcc.groundTile);
                            GameObject temp = new GameObject();
                            temp.transform.parent = tileMap.transform;
                            temp.name = "Rubber";
                            temp.transform.localScale = new Vector3(0.5f, 1, 0.5f);
                            temp.tag = "Rubber";
                            temp.layer = 8;
                            temp.transform.position = new Vector3Int(j - width / 2, height / 2 - i, 0);
                        }
                    }
                    else
                    {
                        MelonLogger.Error($"Index out of range: i={i}, j={j}");
                    }
                }
            }

        }
        public void SetMetal(Tilemap gctm, GroundCoverController gccm, TilePos tilePos)
        {
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    if (i < tilePos.tiles.Count && j < tilePos.tiles[i].Count)
                    {
                        if (tilePos.tiles[i][j] != 0)
                        {
                            tileMap.SetTile(new Vector3Int(j - width / 2, height / 2 - i, 0), pallete[tilePos.tiles[i][j]]);
                            gctm.SetTile(new Vector3Int(j - width / 2, height / 2 - i, 0), gccm.groundTile);
                            GameObject temp = new GameObject();
                            temp.transform.parent = tileMap.transform;
                            temp.name = "Metal";
                            temp.transform.localScale = new Vector3(0.5f, 1, 0.5f);
                            temp.tag = "Metal";
                            temp.layer = 8;
                            temp.transform.position = new Vector3Int(j - width / 2, height / 2 - i, 0);
                        }
                    }
                    else
                    {
                        MelonLogger.Error($"Index out of range: i={i}, j={j}");
                    }
                }
            }
        }
        public void SetDeath(TilePos tilePos)
        {
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    if (i < tilePos.tiles.Count && j < tilePos.tiles[i].Count)
                    {
                        if (tilePos.tiles[i][j] != 0)
                        {
                            tileMap.SetTile(new Vector3Int(j - width / 2, height / 2 - i, 0), pallete[tilePos.tiles[i][j]]);
                            GameObject temp = new GameObject();
                            temp.transform.parent = tileMap.transform;
                            temp.name = "Death Rubber";
                            //temp.transform.localScale = new Vector3(0.5f, 1, 0.5f);
                            temp.tag = "Death Rubber";
                            temp.layer = 8;
                            BoxCollider2D box = temp.AddComponent<BoxCollider2D>();
                            box.offset = new Vector2(1f, 1f);
                            temp.transform.position = new Vector3Int(j - width / 2, height / 2 - i, 0);
                        }
                    }
                    else
                    {
                        MelonLogger.Error($"Index out of range: i={i}, j={j}");
                    }
                }
            }
        }
        public void SetDeathMetal(TilePos tilePos)
        {
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    if (i < tilePos.tiles.Count && j < tilePos.tiles[i].Count)
                    {
                        if (tilePos.tiles[i][j] != 0)
                        {
                            tileMap.SetTile(new Vector3Int(j - width / 2, height / 2 - i, 0), pallete[tilePos.tiles[i][j]]);
                            GameObject temp = new GameObject();
                            temp.transform.parent = tileMap.transform;
                            temp.name = "Death Metal";
                            //temp.transform.localScale = new Vector3(0.5f, 1, 0.5f);
                            temp.tag = "Death";
                            temp.layer = 8;
                            BoxCollider2D box = temp.AddComponent<BoxCollider2D>();
                            box.offset = new Vector2(1f, 1f);
                            temp.transform.position = new Vector3Int(j - width / 2, height / 2 - i, 0);
                        }
                    }
                    else
                    {
                        MelonLogger.Error($"Index out of range: i={i}, j={j}");
                    }
                }
            }
        }
        public void SetInvis(Tilemap gct, GroundCoverController gcc, TilePos tilePos)
        {
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    if (i < tilePos.tiles.Count && j < tilePos.tiles[i].Count)
                    {
                        if (tilePos.tiles[i][j] != 0)
                        {
                            tileMap.SetTile(new Vector3Int(j - width / 2, height / 2 - i, 0), invisibleSolidTile);
                            //MelonLogger.Msg("tiles i j is " + tiles[i][j]);
                            //MelonLogger.Msg($"Placing tile {pallete[tiles[i][j]]} at position ({j}, {i})");
                            gct.SetTile(new Vector3Int(j - width / 2, height / 2 - i, 0), gcc.groundTile);
                            GameObject temp = new GameObject();
                            temp.transform.parent = tileMap.transform;
                            temp.name = "Rubber";
                            temp.transform.localScale = new Vector3(0.5f, 1, 0.5f);
                            temp.tag = "Rubber";
                            temp.layer = 8;
                            temp.transform.position = new Vector3Int(j - width / 2, height / 2 - i, 0);
                        }
                    }
                    else
                    {
                        MelonLogger.Error($"Index out of range: i={i}, j={j}");
                    }
                }
            }
        }
        public void SetNoColl(TilePos tilePos)
        {
            Tilemap t = GameObject.Instantiate(Editor.Layer.tilemapPrefab, tileMap.transform.parent).GetComponent<Tilemap>();
            t.transform.position = new Vector3(-0.5f, -0.5f);
            t.GetComponent<TilemapRenderer>().sortingOrder++;
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    if (i < tilePos.tiles.Count && j < tilePos.tiles[i].Count)
                    {
                        if (tilePos.tiles[i][j] != 0)
                        {
                            t.SetTile(new Vector3Int(j - width / 2, height / 2 - i), pallete[tilePos.tiles[i][j]]);
                            //MelonLogger.Msg("tiles i j is " + tiles[i][j]);
                            //MelonLogger.Msg($"Placing tile {pallete[tiles[i][j]]} at position ({j}, {i})");
                        }
                    }
                    else
                    {
                        MelonLogger.Error($"Index out of range: i={i}, j={j}");
                    }
                }
            }
        }
        public void SetDeathPlane()
        {
            for (int x = -1; x <= width; x++)
            {
                tileMap.SetTile(new Vector3Int(x - width / 2, height / 2 + 1, 0), invisibleSolidTile);
                GameObject temp = new GameObject();
                temp.transform.parent = tileMap.transform;
                temp.name = "SuperDeath";
                //temp.transform.localScale = new Vector3(0.5f, 1, 0.5f);
                temp.tag = "SuperDeath";
                temp.layer = 16;
                BoxCollider2D box = temp.AddComponent<BoxCollider2D>();
                box.offset = new Vector2(1f, 1f);
                temp.transform.position = new Vector3Int(x - width / 2, height / 2 + 1, 0);

                tileMap.SetTile(new Vector3Int(x - width / 2, height / 2 - height, 0), invisibleSolidTile);
                GameObject temp2 = new GameObject();
                temp2.transform.parent = tileMap.transform;
                temp2.name = "SuperDeath";
                //temp.transform.localScale = new Vector3(0.5f, 1, 0.5f);
                temp2.tag = "SuperDeath";
                temp2.layer = 16;
                BoxCollider2D box2 = temp2.AddComponent<BoxCollider2D>();
                box2.offset = new Vector2(1f, 1f);
                temp2.transform.position = new Vector3Int(x - width / 2, height / 2 - height, 0);
            }
            for (int y = -1; y <= height; y++)
            {
                tileMap.SetTile(new Vector3Int(0 - width / 2 - 1, height / 2 - y, 0), invisibleSolidTile);
                GameObject temp = new GameObject();
                temp.transform.parent = tileMap.transform;
                temp.name = "SuperDeath";
                //temp.transform.localScale = new Vector3(0.5f, 1, 0.5f);
                temp.tag = "SuperDeath";
                temp.layer = 16;
                BoxCollider2D box = temp.AddComponent<BoxCollider2D>();
                box.offset = new Vector2(1f, 1f);
                temp.transform.position = new Vector3Int(0 - width / 2 - 1, height / 2 - y, 0);

                tileMap.SetTile(new Vector3Int(width - width / 2, height / 2 - y, 0), invisibleSolidTile);
                GameObject temp2 = new GameObject();
                temp2.transform.parent = tileMap.transform;
                temp2.name = "SuperDeath";
                //temp.transform.localScale = new Vector3(0.5f, 1, 0.5f);
                temp2.tag = "SuperDeath";
                temp2.layer = 16;
                BoxCollider2D box2 = temp2.AddComponent<BoxCollider2D>();
                box2.offset = new Vector2(1f, 1f);
                temp2.transform.position = new Vector3Int(width - width / 2, height / 2 - y, 0);
            }
        }
        void ProcessLine((Case caseGiven, List<float> parameters) case_param)
        {
            // Split the line by commas
            // Parse values

            // Handle the command
            switch (case_param.caseGiven)
            {
                case Case.Win:
                    HandleWin(case_param.parameters[0], case_param.parameters[1]);
                    break;
                case Case.Checkpoint:
                    HandleCheckpoint(case_param.parameters[0], case_param.parameters[1]);
                    break;
                default:
                    MelonLogger.Warning($"Unknown command: {case_param.caseGiven}");
                    break;
            }
        }
        void HandleWin(float param1, float param2)
        {
            GameObject temp = GameObject.Instantiate(win);
            temp.transform.position = new Vector2(param1, param2 - 0.5f);
            temp.SetActive(true);
            GameManager gm = GameObject.FindObjectOfType<GameManager>();
            Type gmt = typeof(GameManager);
            FieldInfo winfield = gmt.GetField("win", BindingFlags.NonPublic | BindingFlags.Instance);
            winfield.SetValue(gm, temp.GetComponent<WinController>());
        }

        void HandleCheckpoint(float param1, float param2)
        {
            GameObject cp = GameObject.Instantiate(checkpoint);
            cp.transform.position = new Vector2(param1, param2);
            cp.SetActive(true);
        }
        public static string ExtractArchive(string archivePath)
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDirectory);
            string tempZipPath = Path.Combine(tempDirectory, "temp.zip");
            File.Copy(archivePath, tempZipPath);
            ZipFile.ExtractToDirectory(tempZipPath, tempDirectory);
            //string[] dirs = Directory.GetDirectories(tempDirectory);
            /*if (Path.GetDirectoryName(dirs[0]) != "Palette")
            {
                MelonLogger.Warning("TJL has nested directory");
                return dirs[0];
            }*/
            return tempDirectory;
        }
        public static void CleanupTemporaryDirectory(string tempDir)
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
                Debug.Log($"Temporary directory deleted: {tempDir}");
            }
        }
        public void PrepMD()
        {
            GameObject tiwd = GameObject.Find("Tilemap Invisible Wall Death");
            tiwd.SetActive(false);
            GameObject spm = GameObject.Find("Sky Particles Moon");
            spm.SetActive(false);
            GameObject b = GameObject.Find("Bolt");
            b.SetActive(false);
            GameObject b1 = GameObject.Find("Bolt (1)");
            b1.SetActive(false);
            GameObject b2 = GameObject.Find("Bolt (2)");
            b2.SetActive(false);
            GameObject b3 = GameObject.Find("Bolt (3)");
            b3.SetActive(false);
            GameObject b4 = GameObject.Find("Bolt (4)");
            b4.SetActive(false);
            GameObject psa = GameObject.Find("Prop Sign Arrow");
            psa.SetActive(false);
            GameObject cc = GameObject.Find("Cloud Cannon");
            cc.SetActive(false);
            GameObject cc1 = GameObject.Find("Cloud Cannon (1)");
            cc1.SetActive(false);
            GameObject cc2 = GameObject.Find("Cloud Cannon (2)");
            cc2.SetActive(false);
            GameObject m = GameObject.Find("Metals");
            m.SetActive(false);
            GameObject c = GameObject.Find("Checkpoint");
            c.SetActive(false);
            GameObject c1 = GameObject.Find("Checkpoint (1)");
            c1.SetActive(false);
            GameObject c2 = GameObject.Find("Checkpoint (2)");
            c2.SetActive(false);
            GameObject c3 = GameObject.Find("Checkpoint (3)");
            c3.SetActive(false);
            foreach (var gameObj in GameObject.FindObjectsOfType(typeof(GameObject)) as GameObject[])
            {
                if (gameObj.name == "Cloud Death Projectile(Clone)")
                {
                    GameObject.Destroy(gameObj);
                }
            }
            //MelonLogger.Msg("Before player");
            GameObject p = GameObject.Find("Player B3");
            //MelonLogger.Msg("After player objkect");
            Rigidbody2D prb = p.GetComponent<Rigidbody2D>();
            //MelonLogger.Msg("After player rb");
            prb.gravityScale = 2;
            var playerSwimming = p.GetComponent("PlayerSwimming");
            if (playerSwimming == null)
            {
                MelonLogger.Error("PlayerSwimming component not found on Player B3!");
                return;
            }
            FieldInfo startGravField = playerSwimming.GetType().GetField("startGrav", BindingFlags.NonPublic | BindingFlags.Instance);
            if (startGravField == null)
            {
                MelonLogger.Error("Private field 'startGrav' not found in PlayerSwimming!");
                return;
            }

            // Set the value of the private field
            startGravField.SetValue(playerSwimming, 2f);
            GameObject glider = GameObject.Find("Star Screamer");
            Shift_StarScreamer gs = glider.GetComponent<Shift_StarScreamer>();
            FieldInfo defaultGravField = gs.GetType().GetField("defaultGravity", BindingFlags.NonPublic | BindingFlags.Instance);
            defaultGravField.SetValue(gs, 2f);
            //MelonLogger.Msg(prb == null);
            //MelonLogger.Msg(p == null);
            //MelonLogger.Msg(prb.gravityScale);
            //MelonLogger.Msg("After set GScale");
            PlayerPrefs.SetInt("HasEquipment1", 0);
            PlayerPrefs.SetInt("HasEquipment2", 1);
            PlayerPrefs.SetInt("HasEquipment3", 1);
            PlayerPrefs.SetInt("HasEquipment4", 0);
            PlayerPrefs.SetInt("HasEquipment5", 1);
            PlayerPrefs.SetInt("HasEquipment6", 0);
            PlayerPrefs.SetInt("HasEquipment7", 0);
            PlayerPrefs.SetInt("HasEquipment8", 0);
            MDHasLoaded = true;
        }
        public void StartLoad()
        {
            SceneManager.LoadScene("Moonlight District");
        }

        private void Abort()
        {
            SceneManager.LoadScene(0);
            CleanupTemporaryDirectory(tempfileloc);
            MelonLogger.Msg("Aborted level load");
        }
        public AssetBundle LoadEmbeddedBundle(string resourceName)
        {
            // Get the assembly containing the embedded resource
            Assembly assembly = Assembly.GetExecutingAssembly();
            MelonLogger.Msg("Starting assetbundle load");
            // Resource name format: "Namespace.Folder.filename.extension"
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    MelonLogger.Error($"Could not find embedded resource: {resourceName}");
                    return null;
                }

                // Read stream into byte array
                byte[] bundleData = new byte[stream.Length];
                stream.Read(bundleData, 0, bundleData.Length);
                MelonLogger.Msg("finished loading asetbundle");
                // Load AssetBundle from memory
                return AssetBundle.LoadFromMemory(bundleData);
            }
        }
    }
    [HarmonyPatch(typeof(CheatCodeManager), "Update")]
    static class TurnOffCheats
    {
        public static bool Prefix()
        {
            return false;
        }
    }
    [HarmonyPatch(typeof(ShiftController), "Update")]
    class FixAbilityWheel
    {
        public static ControlWheel CW;
        public static bool allowswap = Program.allowSwap;
        public static bool goodToGo = Program.MDHasLoaded;
        public static bool hasBeenSet;
        public static bool Prefix(ShiftController __instance)
        {
            if (Input.GetKeyDown(KeyCode.H))
            {
                GameObject.FindObjectOfType<GameManager>().ResetEntireLevel();
            }
            goodToGo = Program.MDHasLoaded;
            if (!goodToGo)
            {
                return false;
            }
            allowswap = Program.allowSwap;
            // Finding the ControlWheel component in the parent or its children
            Transform parentTransform = __instance.transform.parent;
            if (!hasBeenSet && parentTransform != null)
            {
                if (parentTransform != null)
                {
                    CW = parentTransform.GetComponentInChildren<ControlWheel>(true);
                }
                if (!allowswap)
                {
                    if (CW.gameObject != null)
                    {
                        CW.gameObject.SetActive(false);
                        GameObject.Destroy(CW.gameObject);
                        CW = null;
                    }
                }
                hasBeenSet = true;
            }
            if (Input.GetKeyDown(KeyCode.LeftControl) && allowswap)
            {
                MelonLogger.Msg("Ability");
                CW.gameObject.SetActive(true);
                CW.ActivatePopup();
            }
            if (Input.GetKeyUp(KeyCode.LeftControl) && allowswap)
            {
                CW.SelectItem();
                CW.gameObject.SetActive(false);
            }
            return false;
        }
    }
    [HarmonyPatch(typeof(GroundPound), "UseAbility")]
    public class FixPoundIntoVoid
    {
        public static bool Prefix(GroundPound __instance)
        {
            Debug.Log("Using ability.");
            if (__instance.paused)
            {
                return false;
            }
            __instance.playerWalking.groundedTimer = 0.2f;
            __instance.grapple.EndGrapple();
            __instance.trail.emitting = false;
            __instance.trail.enabled = false;
            GameObject.Instantiate(__instance.particleCloud, __instance.transform.position, Quaternion.identity);
            RaycastHit2D raycastHit2D = Physics2D.Raycast(__instance.transform.position, -__instance.transform.up, 99f, 1 << LayerMask.NameToLayer("Ground"));
            if (raycastHit2D.collider == null)
            {
                GameObject.FindObjectOfType<PlayerDeath>().Die();
                return false;
            }
            float num = Vector2.Distance(raycastHit2D.point, __instance.transform.position);
            __instance.playerWalking.transform.position = raycastHit2D.point + new Vector2(0f, 0.3f);
            __instance.rb.velocity = new Vector2(__instance.rb.velocity.x, 0f);
            __instance.playerWalking.SetWannaJump();
            GameObject.Instantiate(__instance.particleSparks, __instance.transform.position, Quaternion.identity);
            GameObject.Instantiate(__instance.strikeBlast, __instance.transform.position, Quaternion.identity);
            __instance.strikeAnim.Play("LineFade");
            for (int i = 0; i < 7; i++)
            {
                if (i == 0 || i == 6)
                {
                    __instance.strikeLine.SetPosition(i, __instance.transform.position + new Vector3(0f, 1f, 0f) * ((float)i / 6f) * num);
                    continue;
                }
                Vector3 vector = new Vector3(UnityEngine.Random.Range(0f - __instance.lineXVariation, __instance.lineXVariation) * num, UnityEngine.Random.Range(0f - __instance.lineYVariation, __instance.lineYVariation) * num, 0f);
                __instance.strikeLine.SetPosition(i, __instance.transform.position + new Vector3(0f, 1f, 0f) * ((float)i / 6f) * num + vector);
            }
            GameObject.FindObjectOfType<AudioManager>().Play("Strike", UnityEngine.Random.Range(0.8f, 1.2f));
            __instance.Invoke("EnableTrail", 0.1f);
            return false;
        }
    }
    [HarmonyPatch(typeof(Shift_PogoStick), "OnCollisionEnter2D")]
    public class FixPogoOffDeathPlane
    {
        public static bool Prefix(GroundPound __instance, Collision2D collision)
        {
            MelonLogger.Msg("Tag: " + collision.gameObject.tag + " Layer: " + collision.gameObject.layer);
            if (collision.gameObject.layer == 16 || collision.gameObject.CompareTag("SuperDeath"))
            {
                GameObject.FindObjectOfType<PlayerDeath>().Die();
                return false;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(GameManager), "NextLevel")]
    public class FixGoingToEndScreen
    {
        public static bool Prefix(GameManager __instance)
        {
            if (SceneManager.GetActiveScene().name == "Moonlight District")
            {
                SceneManager.LoadScene(0);
                return false;
            }
            return true;
        }
    }
    public class FixReplayModUpdate
    {
        public static bool Prefix(object __instance)
        {
            string persistentDataPath = Application.persistentDataPath;
            Scene activeScene = SceneManager.GetActiveScene();
            string path;
            if (activeScene.name == "Moonlight District")
            {
                path = Path.Combine(persistentDataPath, "Replay" + (GetChecksum(Melon<Program>.Instance.archiveLoc) + ".txt"));
            }
            else
            {
                path = Path.Combine(persistentDataPath, "Replay" + (activeScene.buildIndex + ".txt"));
            }
            var lrField = __instance.GetType().GetField("loadedReplay", BindingFlags.NonPublic | BindingFlags.Instance);
            var loadedReplay = lrField.GetValue(__instance) as List<List<string>>;
            var replayField = __instance.GetType().GetField("replay", BindingFlags.NonPublic | BindingFlags.Instance);
            var replay = replayField.GetValue(__instance) as List<List<string>>;
            var convertMethod = __instance.GetType().GetMethod("ConvertToString", BindingFlags.NonPublic | BindingFlags.Instance);
            if (loadedReplay.Count() <= 1 || loadedReplay.Count() >= replay.Count())
            {
                string contents = (string)convertMethod.Invoke(__instance, new object[] { replay });
                File.WriteAllText(path, contents);
            }
            return false;
        }
        public static string GetChecksum(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
    public class FixReplayModLoad
    {
        public static bool Prefix(object __instance, ref List<List<string>> __result)
        {
            string persistentDataPath = Application.persistentDataPath;
            Scene activeScene = SceneManager.GetActiveScene();
            var convertMethod = __instance.GetType().GetMethod("ConvertToList", BindingFlags.NonPublic | BindingFlags.Instance);
            string path;
            if (activeScene.name == "Moonlight District")
            {
                path = Path.Combine(persistentDataPath, "Replay" + (FixReplayModUpdate.GetChecksum(Melon<Program>.Instance.archiveLoc) + ".txt"));
            }
            else
            {
                path = Path.Combine(persistentDataPath, "Replay" + (activeScene.buildIndex + ".txt"));
            }
            if (File.Exists(path))
            {
                string data = File.ReadAllText(path);
                __result = (List<List<string>>)convertMethod.Invoke(__instance, new object[] { data });
                return false;
            }
            __result = new List<List<string>>();
            return false;
        }
    }
    //FINISH REPLAY MOD SUPPOT
}
