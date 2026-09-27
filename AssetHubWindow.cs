using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class AssetHubWindow : EditorWindow
{
    public enum Language { English, Russian, Japanese }

    private string searchFilter = "";
    private Vector2 scrollPosition;
    private static string GlobalHubPath;

    private HashSet<string> expandedFolders = new HashSet<string>();
    private HashSet<string> selectedItems = new HashSet<string>();

    private Language currentLanguage = Language.English;

    [MenuItem("Tools/Global Asset Hub")]
    public static void ShowWindow()
    {
        GetWindow<AssetHubWindow>("Global Asset Hub");
    }

    private void OnEnable()
    {
        GlobalHubPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "UnityGlobalAssetHub"
        );

        if (!Directory.Exists(GlobalHubPath))
        {
            Directory.CreateDirectory(GlobalHubPath);
        }

        currentLanguage = (Language)EditorPrefs.GetInt("GlobalAssetHub_Lang", (int)Language.English);
    }

    private void OnGUI()
    {
        GUILayout.Space(10);

        // ВЕРХНЯЯ ПАНЕЛЬ С ВЫБОРОМ ЯЗЫКА
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label("🌐", GUILayout.Width(20));
        Language newLang = (Language)EditorGUILayout.EnumPopup(currentLanguage, GUILayout.Width(90));
        if (newLang != currentLanguage)
        {
            currentLanguage = newLang;
            EditorPrefs.SetInt("GlobalAssetHub_Lang", (int)currentLanguage);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        DrawDropArea();

        GUILayout.Space(15);

        // 1. ПОИСК
        GUILayout.BeginHorizontal();
        GUILayout.Label(L("Search"), GUILayout.Width(60));
        searchFilter = EditorGUILayout.TextField(searchFilter);
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        // 2. ПАНЕЛЬ МАССОВОГО ИМПОРТА
        GUI.enabled = selectedItems.Count > 0;
        if (GUILayout.Button($"{L("ImportSelected")} ({selectedItems.Count})", GUILayout.Height(30)))
        {
            ImportSelectedItems();
        }
        GUI.enabled = true;

        GUILayout.Space(10);
        DrawHubItems();
    }

    private void DrawDropArea()
    {
        Event evt = Event.current;
        Rect dropArea = GUILayoutUtility.GetRect(0f, 60f, GUILayout.ExpandWidth(true));

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11,
            fontStyle = FontStyle.Bold
        };

        GUI.Box(dropArea, L("DropArea"), boxStyle);

        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropArea.Contains(evt.mousePosition))
                    break;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    foreach (Object draggedObject in DragAndDrop.objectReferences)
                    {
                        string assetPath = AssetDatabase.GetAssetPath(draggedObject);
                        SaveAssetToGlobalHub(assetPath);
                    }
                }
                Event.current.Use();
                break;
        }
    }

    private void DrawHubItems()
    {
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        string[] items = Directory.GetFileSystemEntries(GlobalHubPath);

        if (items.Length == 0)
        {
            EditorGUILayout.HelpBox(L("EmptyStorage"), MessageType.Info);
        }

        foreach (string itemPath in items)
        {
            if (itemPath.EndsWith(".meta")) continue;
            DrawItem(itemPath);
        }

        GUILayout.EndScrollView();
    }

    private void DrawItem(string itemPath)
    {
        string itemName = Path.GetFileName(itemPath);
        bool isDirectory = Directory.Exists(itemPath);

        bool matchesSearch = string.IsNullOrEmpty(searchFilter) || itemName.ToLower().Contains(searchFilter.ToLower());

        if (!matchesSearch) return;

        GUILayout.BeginHorizontal("box");

        bool isSelected = selectedItems.Contains(itemPath);
        bool toggleState = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));

        if (toggleState != isSelected)
        {
            if (toggleState) selectedItems.Add(itemPath);
            else selectedItems.Remove(itemPath);
        }

        if (isDirectory)
        {
            bool isExpanded = expandedFolders.Contains(itemPath);
            isExpanded = EditorGUILayout.Foldout(isExpanded, itemName, true);

            if (isExpanded) expandedFolders.Add(itemPath);
            else expandedFolders.Remove(itemPath);
        }
        else
        {
            GUILayout.Label(itemName, GUILayout.ExpandWidth(true));
        }

        if (GUILayout.Button(L("Import"), GUILayout.Width(75)))
        {
            ImportAssetToCurrentProject(itemPath, itemName);
        }

        // КНОПКА УДАЛЕНИЯ С ПОДТВЕРЖДЕНИЕМ
        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            string deleteMsg = string.Format(L("DeleteMsg"), itemName);
            
            if (EditorUtility.DisplayDialog(L("DeleteTitle"), deleteMsg, L("Yes"), L("Cancel")))
            {
                selectedItems.Remove(itemPath);
                if (isDirectory) Directory.Delete(itemPath, true);
                else File.Delete(itemPath);

                GUIUtility.ExitGUI();
            }
        }

        GUILayout.EndHorizontal();

        if (isDirectory && expandedFolders.Contains(itemPath))
        {
            EditorGUI.indentLevel++;
            string[] children = Directory.GetFileSystemEntries(itemPath);

            foreach (string child in children)
            {
                if (child.EndsWith(".meta")) continue;
                DrawItem(child);
            }
            EditorGUI.indentLevel--;
        }
    }

    private void ImportSelectedItems()
    {
        foreach (string itemPath in selectedItems)
        {
            if (File.Exists(itemPath) || Directory.Exists(itemPath))
            {
                string fileName = Path.GetFileName(itemPath);
                ImportAssetToCurrentProject(itemPath, fileName);
            }
        }

        selectedItems.Clear();
        AssetDatabase.Refresh();
        Debug.Log(L("LogAllImported"));
    }

    private void SaveAssetToGlobalHub(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return;

        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
        string fileName = Path.GetFileName(relativePath);
        string destinationPath = Path.Combine(GlobalHubPath, fileName);

        if (File.Exists(fullPath))
        {
            File.Copy(fullPath, destinationPath, true);
            Debug.Log($"{L("LogSaved")} {fileName}");
        }
        else if (Directory.Exists(fullPath))
        {
            CopyDirectory(fullPath, destinationPath);
            Debug.Log($"{L("LogSaved")} {fileName}");
        }
    }

    private void ImportAssetToCurrentProject(string sourceFilePath, string fileName)
    {
        string targetDirectory = Path.Combine(Application.dataPath, "ImportedFromHub");

        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        string destinationPath = Path.Combine(targetDirectory, fileName);

        if (File.Exists(sourceFilePath))
        {
            File.Copy(sourceFilePath, destinationPath, true);
        }
        else if (Directory.Exists(sourceFilePath))
        {
            CopyDirectory(sourceFilePath, destinationPath);
        }

        AssetDatabase.Refresh();
        Debug.Log($"{L("LogImported")} {fileName}");
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            if (file.EndsWith(".meta")) continue;
            string dest = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, dest, true);
        }

        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            string dest = Path.Combine(destinationDir, Path.GetFileName(dir));
            CopyDirectory(dir, dest);
        }
    }

    // СЛОВАРЬ ЛОКАЛИЗАЦИИ
    private string L(string key)
    {
        return currentLanguage switch
        {
            Language.Russian => key switch
            {
                "Search" => "Поиск:",
                "DropArea" => "Перетащите сюда скрипты или папки из окна Project",
                "ImportSelected" => "Импортировать выбранное",
                "EmptyStorage" => "Хранилище пусто. Перетащите сюда ассеты!",
                "Import" => "Импорт",
                "LogAllImported" => "[AssetHub] Все выбранные элементы импортированы!",
                "LogSaved" => "[AssetHub] Сохранено в хаб:",
                "LogImported" => "[AssetHub] Импортировано в проект:",
                "DeleteTitle" => "Удаление ассета",
                "DeleteMsg" => "Вы уверены, что хотите удалить «{0}» из глобального хранилища?",
                "Yes" => "Да",
                "Cancel" => "Отмена",
                _ => key
            },
            Language.Japanese => key switch
            {
                "Search" => "検索:",
                "DropArea" => "Projectからスクリプトやフォルダをドロップ",
                "ImportSelected" => "選択した項目をインポート",
                "EmptyStorage" => "ストレージは空です。アセットをドロップしてください！",
                "Import" => "インポート",
                "LogAllImported" => "[AssetHub] 選択したすべての項目をインポートしました！",
                "LogSaved" => "[AssetHub] ハブに保存完了:",
                "LogImported" => "[AssetHub] プロジェクトにインポート完了:",
                "DeleteTitle" => "アセットの削除",
                "DeleteMsg" => "「{0}」をグローバルハブから削除してもよろしいですか？",
                "Yes" => "はい",
                "Cancel" => "キャンセル",
                _ => key
            },
            _ => key switch // English (Default)
            {
                "Search" => "Search:",
                "DropArea" => "Drag & drop scripts or folders from Project window here",
                "ImportSelected" => "Import Selected",
                "EmptyStorage" => "Storage is empty. Drag and drop assets here!",
                "Import" => "Import",
                "LogAllImported" => "[AssetHub] All selected items imported successfully!",
                "LogSaved" => "[AssetHub] Saved to Hub:",
                "LogImported" => "[AssetHub] Imported to project:",
                "DeleteTitle" => "Delete Asset",
                "DeleteMsg" => "Are you sure you want to delete '{0}' from the Global Hub?",
                "Yes" => "Yes",
                "Cancel" => "Cancel",
                _ => key
            }
        };
    }
}
