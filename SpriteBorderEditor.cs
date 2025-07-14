using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Game.EditorExtensions
{
public class SpriteBorderEditor : EditorWindow
{
    private const string FOLDER_PATH_KEY    = "target_folder_path";
    private const string PREFIX_FILTER_KEY  = "prefix_filter";
    private const string POSTFIX_FILTER_KEY = "postfix_filter";
    private const string REGEX_FILTER_KEY   = "regex_filter";
    
    private PivotUnitMode   _pivotUnitMode = PivotUnitMode.Normalized;
    private Vector4         _border        = new (25f, 25f, 25f, 25f);
    private Vector2         _customPivot   = Vector2.zero;
    private SpriteAlignment _spritePivot   = SpriteAlignment.Center;
    
    private string _targetFolderPath;
    private bool   _guiStylesCreated;
    private bool   _settingsFoldoutState;
    private bool   _filterFoldoutState;

    private bool _prefixFilterToggle;
    private bool _postfixFilterToggle;
    private bool _regexFilterToggle;

    private string _prefixFilter;
    private string _postfixFilter;
    private string _regexFilter;

    private Vector2 _scrollPosition;

    private readonly List<string> _updatedSprites = new ();

    private GUIStyle _headerStyle;
    private GUIStyle _updatedSpritesStyle;

    [MenuItem("Tools/Sprite Border Editor")]
    public static void ShowWindow()
    {
        GetWindow<SpriteBorderEditor>("Sprite Border Editor");
    }

    private void OnEnable()
    {
        _targetFolderPath = EditorPrefs.GetString(FOLDER_PATH_KEY, "Assets/");
        _prefixFilter     = EditorPrefs.GetString(PREFIX_FILTER_KEY);
        _postfixFilter    = EditorPrefs.GetString(POSTFIX_FILTER_KEY);
        _regexFilter      = EditorPrefs.GetString(REGEX_FILTER_KEY);
    }

    private void CreateGUIStyles()
    {
        _headerStyle = new GUIStyle("label")
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter
        };
        
        _updatedSpritesStyle = new GUIStyle("label")
        {
            fontSize  = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
    }

    private void OnGUI()
    {
        if (!_guiStylesCreated) CreateGUIStyles();

        GUILayout.Label("Sprite Border Editor", _headerStyle);

        GUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
            GUILayout.Label("Sprites Folder:", GUILayout.Width(90));
            _targetFolderPath = EditorGUILayout.TextField(_targetFolderPath);
        if(EditorGUI.EndChangeCheck())
            EditorPrefs.SetString(FOLDER_PATH_KEY, _targetFolderPath);
        EditorGUILayout.EndHorizontal();

        _filterFoldoutState = EditorGUILayout.BeginFoldoutHeaderGroup(_filterFoldoutState, "Filter");
        if (_filterFoldoutState)
        {
            FilterGroup();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        
        _settingsFoldoutState = EditorGUILayout.BeginFoldoutHeaderGroup(_settingsFoldoutState, "Settings");
            if (_settingsFoldoutState)
            {
                ExtensionsGroup();
                GUILayout.Space(3);
                PivotUnitModeGroup();
                GUILayout.Space(3);
                BorderGroup();
                GUILayout.Space(3);
                PivotGroup();
            }
        EditorGUILayout.EndFoldoutHeaderGroup();

        if(_updatedSprites.Count > 0)
        {
            GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
            GUILayout.Label("Updated sprites", _updatedSpritesStyle);

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, "OL Box");
            foreach (var sprite in _updatedSprites)
            {
                GUILayout.Label(sprite);
                GUILayout.Space(3);
            }

            GUILayout.EndScrollView();
        }

        GUILayout.FlexibleSpace();
            if (GUILayout.Button("Update Borders", GUILayout.Height(35)))
            {
                try
                {
                    UpdateSpriteBorders();
                }
                catch (Exception e)
                {
                    Debug.LogError(e.Message);
                }
            }
    }

    private void PivotGroup()
    {
        EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Pivot:", GUILayout.Width(84));
            _spritePivot = (SpriteAlignment)EditorGUILayout.EnumPopup("", _spritePivot);
            if (_spritePivot == SpriteAlignment.Custom)
                _customPivot = EditorGUILayout.Vector2Field("", _customPivot);
        EditorGUILayout.EndHorizontal();
    }

    private void BorderGroup()
    {
        EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Border:", GUILayout.Width(84));
            _border = EditorGUILayout.Vector4Field("", _border);
            if (_border.x < 0) _border.x = 0;
            if (_border.y < 0) _border.y = 0;
            if (_border.z < 0) _border.z = 0;
            if (_border.w < 0) _border.w = 0;
        EditorGUILayout.EndHorizontal();
    }

    private void PivotUnitModeGroup()
    {
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.BeginHorizontal();
            GUILayout.Label("UnitMode:", GUILayout.Width(84));
            _pivotUnitMode = (PivotUnitMode)EditorGUILayout.EnumPopup("", _pivotUnitMode);
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();
    }

    private void ExtensionsGroup()
    {
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Extensions:", GUILayout.Width(84));
            EditorGUILayout.TextField(".png");
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();
    }
    
    private void UpdateSpriteBorders()
    {
        string[] spritePaths = Directory.GetFiles(_targetFolderPath, "*.png", SearchOption.AllDirectories);
        
        var filteredPaths = spritePaths.Where(path => 
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            
            if (_regexFilterToggle && !string.IsNullOrEmpty(_regexFilter))
            {
                try
                {
                    return Regex.IsMatch(fileName, _regexFilter);
                }
                catch (ArgumentException)
                {
                    Debug.LogError($"Invalid regex pattern: {_regexFilter}");
                    return false;
                }
            }
            
            bool prefixMatch = !_prefixFilterToggle || 
                             fileName.StartsWith(_prefixFilter ?? "", StringComparison.OrdinalIgnoreCase);
            
            bool postfixMatch = !_postfixFilterToggle || 
                              fileName.EndsWith(_postfixFilter ?? "", StringComparison.OrdinalIgnoreCase);
            
            return prefixMatch && postfixMatch;
        }).ToArray();
    
        _updatedSprites.Clear();
        foreach (string path in filteredPaths)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            TextureImporterSettings settings = new TextureImporterSettings();
    
            if (importer != null && importer.spriteImportMode == SpriteImportMode.Single)
            {
                importer.ReadTextureSettings(settings);
    
                settings.spriteBorder = _border;
                settings.spriteAlignment = (int)_spritePivot;
    
                if (_spritePivot == SpriteAlignment.Custom)
                    settings.spritePivot = _customPivot;
    
                importer.SetTextureSettings(settings);
    
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
    
                _updatedSprites.Add(Path.GetFileNameWithoutExtension(path));
            }
        }
    
        Debug.Log($"Updated {_updatedSprites.Count} sprites.");
    }
    
    private void FilterGroup()
    {
        EditorGUILayout.BeginHorizontal();
        _prefixFilterToggle = EditorGUILayout.Toggle(_prefixFilterToggle, GUILayout.Width(16));
        if (_prefixFilterToggle) _regexFilterToggle = false;
            EditorGUI.BeginDisabledGroup(!_prefixFilterToggle);
                GUILayout.Label("By prefix", GUILayout.Width(65));
                EditorGUI.BeginChangeCheck();
                    _prefixFilter = EditorGUILayout.TextField(_prefixFilter);
                if(EditorGUI.EndChangeCheck()) EditorPrefs.SetString(PREFIX_FILTER_KEY, _prefixFilter);
            EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        _postfixFilterToggle = EditorGUILayout.Toggle(_postfixFilterToggle, GUILayout.Width(16));
        if (_postfixFilterToggle) _regexFilterToggle = false;
            EditorGUI.BeginDisabledGroup(!_postfixFilterToggle);
                GUILayout.Label("By postfix", GUILayout.Width(65));
                EditorGUI.BeginChangeCheck();
                    _postfixFilter = EditorGUILayout.TextField(_postfixFilter);
                if(EditorGUI.EndChangeCheck()) EditorPrefs.SetString(POSTFIX_FILTER_KEY, _postfixFilter);
            EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        _regexFilterToggle = EditorGUILayout.Toggle(_regexFilterToggle, GUILayout.Width(16));
        if (_regexFilterToggle) { _prefixFilterToggle  = false; _postfixFilterToggle = false; }
            EditorGUI.BeginDisabledGroup(!_regexFilterToggle);
                GUILayout.Label("By regex", GUILayout.Width(65));
                EditorGUI.BeginChangeCheck();
                    _regexFilter = EditorGUILayout.TextField(_regexFilter);
                if(EditorGUI.EndChangeCheck()) EditorPrefs.SetString(REGEX_FILTER_KEY, _regexFilter);
            EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
    }

    private enum PivotUnitMode
    {
        Normalized,
        Pixels
    }
}
}