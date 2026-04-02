using UnityEngine;
using System.IO;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using MelonLoader;
using System;
using System.Linq;
using System.Threading.Tasks;
using Harmony;

namespace JohnBoltsLevelWarehouse.Editor
{
    public class FileBrowser
    {
        readonly Dictionary<string, string> iconFromFileEnding = new Dictionary<string, string>
        {
            { ".png",  "Icon_PictoIcon_Photo" },
            { ".jpg",  "Icon_PictoIcon_Photo" },
            { ".jpeg", "Icon_PictoIcon_Photo" },
            { ".mp4",  "Icon_PictoIcon_Media" },
            { ".mov",  "Icon_PictoIcon_Media" },
            { ".mp3",  "Icon_PictoIcon_Music_2" },
            { ".tjl",  "Icon_PictoIcon_Energy" },
            { ".dir",  "Icon_PictoIcon_Folder" },
            { ".txt",  "Icon_PictoIcon_Paper" },
            { ".sh",   "Icon_PictoIcon_Paper" },
            { ".bat",  "Icon_PictoIcon_Paper" }
        };

        public GameObject FileExplorer;
        public static GameObject FolderContentsItem;
        public static GameObject FolderPickerItem;
        public static GameObject FileExplorerPrefab;
        public GameObject FolderContents;
        public GameObject FolderPicker;
        public TMP_InputField PathField;
        public List<string> previousPaths = new List<string>();
        public string currentLoadedPath;
        public List<(string, Image)> currentSelected = new List<(string, Image)>();
        public string[] Filter;
        public bool MultiSelect;
        private TaskCompletionSource<string[]> filePickerTask;

        public FileBrowser(LevelEditorFrontend creator)
        {
            FileExplorer = GameObject.Find("Canvas").transform.GetChild(4).gameObject;
            FolderContents = FileExplorer.transform.GetChild(3).gameObject;
            //FolderPicker = FileExplorer.transform.GetChild(5).gameObject;
            Button ImportImage = GameObject.Find("Canvas").transform.GetChild(0).GetChild(2).GetComponent<Button>();
            ImportImage.onClick.AddListener(async () =>
            {
                if (!FileExplorer.activeSelf)
                {
                    creator.allowPanZoom = false;
                    string[] files = await OpenFilePicker(new string[] { ".png", ".jpg", ".jpeg" }, GetPicturesPath(), true);
                    Drawing.instance.LoadFilesToPalette(files);
                }
            });
            Button CloseButton = FileExplorer.transform.GetChild(0).GetChild(1).GetComponent<Button>();
            CloseButton.onClick.AddListener(() =>
            {
                FileExplorer.SetActive(false);
                creator.allowPanZoom = true;
            });

            PathField = FileExplorer.transform.GetChild(1).GetComponent<TMP_InputField>();
            PathField.textComponent.alignment = TextAlignmentOptions.Left;
            PathField.onValueChanged.AddListener((string value) =>
            {
                if (value.Length > 62)
                {
                    PathField.textComponent.alignment = TextAlignmentOptions.Right;
                }
                else
                {
                    PathField.textComponent.alignment = TextAlignmentOptions.Left;
                }
            });
            PathField.onEndEdit.AddListener((string value) =>
            {
                if (Directory.Exists(value))
                {
                    LoadDirectoryToFolderContents(value);
                }
                else
                {
                    PathField.SetTextWithoutNotify(currentLoadedPath);
                }
            });

            Button BackButton = FileExplorer.transform.GetChild(9).GetComponent<Button>();
            BackButton.onClick.AddListener(() =>
            {
                //MelonLogger.Msg(previousPaths[previousPaths.Count - 1]);
                if (previousPaths.Count != 0 && previousPaths[previousPaths.Count - 1] != "" && previousPaths[previousPaths.Count - 1] != null)
                {
                    LoadDirectoryToFolderContents(previousPaths[previousPaths.Count - 1], false);
                    previousPaths.RemoveAt(previousPaths.Count - 1);
                }
            });
            Button SelectButton = FileExplorer.transform.GetChild(7).GetComponent<Button>();
            SelectButton.onClick.AddListener(() =>
            {
                ConfirmSelection();
                FileExplorer.SetActive(false);
                creator.allowPanZoom = true;
            });
            //LoadDirectoryToFolderContents("/home/frenchy/.local/share/Steam/steamapps/common/Thunder Jumper/");
        }
        public FileBrowser(GameObject FileExplorerObj, string title)
        {
            FileExplorer = FileExplorerObj;
            FolderContents = FileExplorer.transform.GetChild(3).gameObject;
            //FolderPicker = FileExplorer.transform.GetChild(5).gameObject;
            TMP_Text TitleText = FileExplorer.transform.GetChild(0).GetChild(0).GetComponent<TMP_Text>();
            TitleText.text = title;
            Button CloseButton = FileExplorer.transform.GetChild(0).GetChild(1).GetComponent<Button>();
            CloseButton.onClick.AddListener(() =>
            {
                FileExplorer.SetActive(false);
            });

            PathField = FileExplorer.transform.GetChild(1).GetComponent<TMP_InputField>();
            PathField.textComponent.alignment = TextAlignmentOptions.Left;
            PathField.onValueChanged.AddListener((string value) =>
            {
                if (value.Length > 62)
                {
                    PathField.textComponent.alignment = TextAlignmentOptions.Right;
                }
                else
                {
                    PathField.textComponent.alignment = TextAlignmentOptions.Left;
                }
            });
            PathField.onEndEdit.AddListener((string value) =>
            {
                if (Directory.Exists(value))
                {
                    LoadDirectoryToFolderContents(value);
                }
                else
                {
                    PathField.SetTextWithoutNotify(currentLoadedPath);
                }
            });

            Button BackButton = FileExplorer.transform.GetChild(9).GetComponent<Button>();
            BackButton.onClick.AddListener(() =>
            {
                //MelonLogger.Msg(previousPaths[previousPaths.Count - 1]);
                if (previousPaths.Count != 0 && previousPaths[previousPaths.Count - 1] != "" && previousPaths[previousPaths.Count - 1] != null)
                {
                    LoadDirectoryToFolderContents(previousPaths[previousPaths.Count - 1], false);
                    previousPaths.RemoveAt(previousPaths.Count - 1);
                }
            });
            Button SelectButton = FileExplorer.transform.GetChild(7).GetComponent<Button>();
            SelectButton.onClick.AddListener(() =>
            {
                ConfirmSelection();
                FileExplorer.SetActive(false);
            });
            //LoadDirectoryToFolderContents("/home/frenchy/.local/share/Steam/steamapps/common/Thunder Jumper/");
        }
        public async Task<string[]> OpenFilePicker(string[] filter, string startPath, bool multiSelect)
        {
            FileExplorer.SetActive(true);
            MultiSelect = multiSelect;
            Filter = filter;
            LoadDirectoryToFolderContents(startPath);
            filePickerTask = new TaskCompletionSource<string[]>();
            return await filePickerTask.Task;
        }
        private void ConfirmSelection()
        {
            List<string> temp = new List<string>();
            foreach ((string, Image) file in currentSelected)
            {
                temp.Add(file.Item1);
            }
            string[] tempArr = temp.ToArray();
            string[] selectedFiles = tempArr; // whatever your selection logic is
            filePickerTask?.SetResult(selectedFiles);
        }
        public void LoadDirectoryToFolderContents(string path, bool addToPrevious = true)
        {
            if (addToPrevious)
            {
                previousPaths.Add(currentLoadedPath);
            }

            //MelonLogger.Msg(ListToString(previousPaths));
            currentLoadedPath = path;
            currentSelected.Clear();
            for (int i = FolderContents.transform.GetChild(0).GetChild(0).transform.childCount - 1; i >= 0; i--)
            {
                GameObject.Destroy(FolderContents.transform.GetChild(0).GetChild(0).transform.GetChild(i).gameObject);
            }
            PathField.SetTextWithoutNotify(path);
            foreach (string dir in Directory.GetDirectories(path))
            {
                GameObject dirItem = GameObject.Instantiate(FolderContentsItem, FolderContents.transform.GetChild(0).GetChild(0));
                //MelonLogger.Msg(dirItem.name);
                Image icon = dirItem.transform.GetChild(1).GetComponent<Image>();
                icon.sprite = LevelEditorFrontend.LoadSpriteFromEmbeddedResource("JohnBoltsLevelWarehouse.Resources.Icons.Icon_PictoIcon_Folder.Png");
                TMP_Text filename = dirItem.transform.GetChild(2).GetComponent<TMP_Text>();
                filename.text = Path.GetFileName(dir);
                TMP_Text filesize = dirItem.transform.GetChild(3).GetComponent<TMP_Text>();
                filesize.text = "";
                TMP_Text lastmodified = dirItem.transform.GetChild(4).GetComponent<TMP_Text>();
                lastmodified.text = "";
                Button button = dirItem.transform.GetChild(5).GetComponent<Button>();
                //MelonLogger.Msg(button.gameObject.name);
                button.onClick.AddListener(() =>
                {
                    //MelonLogger.Msg("click");
                    LoadDirectoryToFolderContents(dir);
                });
            }
            foreach (string file in Directory.GetFiles(path))
            {
                if (!Filter.Contains(Path.GetExtension(file).ToLower()))
                {
                    //MelonLogger.Msg("File not shown because extension is" + Path.GetExtension(file).ToLower());
                    continue;
                }
                GameObject dirItem = GameObject.Instantiate(FolderContentsItem, FolderContents.transform.GetChild(0).GetChild(0));
                Image icon = dirItem.transform.GetChild(1).GetComponent<Image>();
                icon.sprite = LevelEditorFrontend.LoadSpriteFromEmbeddedResource("JohnBoltsLevelWarehouse.Resources.Icons." + iconFromFileEnding.GetValueOrDefault(Path.GetExtension(file), "Icon_PictoIcon_Document") + ".Png");
                TMP_Text filename = dirItem.transform.GetChild(2).GetComponent<TMP_Text>();
                filename.text = Path.GetFileName(file);
                TMP_Text filesize = dirItem.transform.GetChild(3).GetComponent<TMP_Text>();
                filesize.text = SizeSuffix(long.Parse(new FileInfo(file).Length.ToString()), 2);
                TMP_Text lastmodified = dirItem.transform.GetChild(4).GetComponent<TMP_Text>();
                lastmodified.text = File.GetLastAccessTime(file).ToShortDateString();
                Button button = dirItem.transform.GetChild(5).GetComponent<Button>();
                Image img = dirItem.transform.GetChild(0).GetComponent<Image>();
                button.onClick.AddListener(() =>
                {
                    if (Input.GetKey(KeyCode.LeftControl))
                    {
                        if (MultiSelect)
                        {
                            currentSelected.Add((path, img));
                        }
                        else
                        {
                            foreach ((string, Image img) item in currentSelected)
                            {
                                item.img.color = Color.black;
                            }
                            currentSelected.Clear();
                            currentSelected.Add((file, img));
                        }
                    }
                    else
                    {
                        foreach ((string, Image img) item in currentSelected)
                        {
                            item.img.color = Color.black;
                        }
                        currentSelected.Clear();
                        currentSelected.Add((file, img));
                    }
                    img.color = Color.gray;
                });
            }
            FolderContents.GetComponent<ScrollRect>().verticalNormalizedPosition = 0;
        }
        static string GetPicturesPath()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            return path;
        }
        static readonly string[] SizeSuffixes =
                  { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

        static string SizeSuffix(Int64 value, int decimalPlaces = 1)
        {
            if (value < 0) { return "-" + SizeSuffix(-value, decimalPlaces); }

            int i = 0;
            decimal dValue = (decimal)value;
            while (Math.Round(dValue, decimalPlaces) >= 1000)
            {
                dValue /= 1024;
                i++;
            }

            return string.Format("{0:n" + decimalPlaces + "} {1}", dValue, SizeSuffixes[i]);
        }
        public static string ListToString(List<String> list)
        {
            string ret = "{";
            foreach (string s in list)
            {
                if (s == null || s == " " || s == "")
                {
                    ret += "null, ";
                    continue;
                }
                ret = ret + s + ", ";
            }
            ret = ret.Substring(0, ret.Length - 2);
            ret += "}";
            return ret;
        }
    }
}