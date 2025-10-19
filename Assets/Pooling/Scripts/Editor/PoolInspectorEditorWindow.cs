using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using AngryKoala.Services;

namespace AngryKoala.Pooling
{
    public sealed class PoolInspectorEditorWindow : EditorWindow
    {
        private IPoolService _poolService;

        private IDictionary _monoPoolsDictionary;
        private IDictionary _objectPoolsDictionary;

        private bool _autoRefresh = true;
        private double _nextRefreshTime;

        private string _monoPoolSearchText = string.Empty;
        private string _objectPoolSearchText = string.Empty;

        private Vector2 _leftScroll;
        private Vector2 _rightScroll;

        private readonly Dictionary<string, bool> _monoPoolFoldoutStates = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _objectPoolFoldoutStates = new(StringComparer.Ordinal);

        private const double AutoRefreshInterval = 0.5;

        private const string MonoPrefKeyPrefix = "PoolInspector.Mono.";
        private const string ObjectPrefKeyPrefix = "PoolInspector.Obj.";

        private static GUIStyle _headerStyle;
        private static GUIStyle _headerLabelStyle;
        private static GUIStyle _backgroundStyle;

        private void OnEnable()
        {
            RefreshTargets();
            RestoreFoldoutPrefsForCurrentKeys();

            _nextRefreshTime = EditorApplication.timeSinceStartup + AutoRefreshInterval;
        }

        [MenuItem("Angry Koala/Pools/Pool Inspector")]
        private static void Open()
        {
            PoolInspectorEditorWindow window = GetWindow<PoolInspectorEditorWindow>("Pool Inspector");

            window.minSize = new Vector2(1000f, 520f);
            window.Show();
        }

        private void RefreshTargets()
        {
            _poolService = ServiceLocator.Get<IPoolService>();

            _monoPoolsDictionary = null;
            _objectPoolsDictionary = null;

            if (_poolService == null)
            {
                return;
            }

            try
            {
                Type poolServiceType = typeof(PoolService);
                FieldInfo monoPoolsFieldInfo =
                    poolServiceType.GetField("_monoPools", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo objectPoolsFieldInfo =
                    poolServiceType.GetField("_objectPools", BindingFlags.NonPublic | BindingFlags.Instance);

                _monoPoolsDictionary = monoPoolsFieldInfo?.GetValue(_poolService) as IDictionary;
                _objectPoolsDictionary = objectPoolsFieldInfo?.GetValue(_poolService) as IDictionary;
            }
            catch
            {
                _monoPoolsDictionary = null;
                _objectPoolsDictionary = null;
            }
        }

        private void RestoreFoldoutPrefsForCurrentKeys()
        {
            _monoPoolFoldoutStates.Clear();
            _objectPoolFoldoutStates.Clear();

            if (_monoPoolsDictionary != null)
            {
                foreach (string key in _monoPoolsDictionary.Keys.Cast<string>())
                {
                    _monoPoolFoldoutStates[key] = EditorPrefs.GetBool(MonoPrefKeyPrefix + key, false);
                }
            }

            if (_objectPoolsDictionary != null)
            {
                foreach (string key in _objectPoolsDictionary.Keys.Cast<string>())
                {
                    _objectPoolFoldoutStates[key] = EditorPrefs.GetBool(_objectPoolsDictionary + key, false);
                }
            }
        }

        private void Update()
        {
            if (!_autoRefresh || !(EditorApplication.timeSinceStartup >= _nextRefreshTime))
            {
                return;
            }

            RefreshTargets();
            Repaint();

            _nextRefreshTime = EditorApplication.timeSinceStartup + AutoRefreshInterval;
        }

        private void OnGUI()
        {
            SetGUIStyles();
            DrawToolbar();

            using (new EditorGUILayout.HorizontalScope())
            {
                float halfWidth = Mathf.Round(position.width * 0.5f);

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(halfWidth)))
                {
                    DrawSectionHeaderWithControls(
                        "Mono Pools",
                        ref _monoPoolSearchText,
                        onExpandAll: () => SetFoldoutsForSide(true, true),
                        onCollapseAll: () => SetFoldoutsForSide(true, false)
                    );

                    _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll, GUILayout.ExpandHeight(true));
                    DrawMonoPoolsPanel();
                    EditorGUILayout.EndScrollView();
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    DrawSectionHeaderWithControls(
                        "Object Pools",
                        ref _objectPoolSearchText,
                        onExpandAll: () => SetFoldoutsForSide(false, true),
                        onCollapseAll: () => SetFoldoutsForSide(false, false)
                    );

                    _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll, GUILayout.ExpandHeight(true));
                    DrawObjectPoolsPanel();
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void SetGUIStyles()
        {
            if (_headerStyle != null && _headerLabelStyle != null && _backgroundStyle != null)
            {
                return;
            }

            _headerStyle = GetGUIStyle("RL Header", new GUIStyle(EditorStyles.toolbar));
            _headerStyle = new GUIStyle(_headerStyle)
            {
                padding = new RectOffset(5, 5, 5, 5),
                margin = new RectOffset(2, 2, 6, 0),
                stretchHeight = false
            };

            _headerLabelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0),
                fontStyle = FontStyle.Bold
            };

            _backgroundStyle = GetGUIStyle("RL Background", new GUIStyle(EditorStyles.helpBox));
            _backgroundStyle = new GUIStyle(_backgroundStyle)
            {
                padding = new RectOffset(6, 6, 6, 6),
                margin = new RectOffset(2, 2, 0, 2),
                stretchHeight = false
            };
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                RefreshTargets();
                RestoreFoldoutPrefsForCurrentKeys();
                Repaint();
            }

            GUILayout.FlexibleSpace();
            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto-refresh", EditorStyles.toolbarButton);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSectionHeaderWithControls(string title, ref string searchText, Action onExpandAll,
            Action onCollapseAll)
        {
            const float headerHeight = 22f;
            const float expandAllButtonWidth = 84f;
            const float collapseAllButtonWidth = 90f;
            const float paddingLeft = 4f;
            const float paddingRight = 4f;
            const float spacing = 6f;
            const float verticalPadding = 4f;

            GUILayout.Space(verticalPadding);

            Rect headerRect = EditorGUILayout.GetControlRect(false, headerHeight);
            headerRect.y = Mathf.Round(headerRect.y);

            GUI.Box(headerRect, GUIContent.none, EditorStyles.helpBox);

            Rect labelRect = new Rect(
                headerRect.x + paddingLeft,
                headerRect.y + ((headerHeight - EditorGUIUtility.singleLineHeight) * 0.5f),
                headerRect.width - (expandAllButtonWidth + collapseAllButtonWidth + paddingLeft + paddingRight +
                                    (spacing * 2f) + 6f),
                EditorGUIUtility.singleLineHeight);

            float buttonsY = headerRect.y + ((headerHeight - EditorGUIUtility.singleLineHeight) * 0.5f);

            Rect collapseAllButton = new Rect(
                headerRect.xMax - paddingRight - collapseAllButtonWidth,
                buttonsY,
                collapseAllButtonWidth,
                EditorGUIUtility.singleLineHeight);

            Rect expandAllButton = new Rect(
                collapseAllButton.x - spacing - expandAllButtonWidth,
                buttonsY,
                expandAllButtonWidth,
                EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(labelRect, title, EditorStyles.boldLabel);

            if (GUI.Button(expandAllButton, "Expand all", EditorStyles.miniButton))
            {
                onExpandAll?.Invoke();
            }

            if (GUI.Button(collapseAllButton, "Collapse all", EditorStyles.miniButton))
            {
                onCollapseAll?.Invoke();
            }

            GUILayout.Space(verticalPadding);

            using (new EditorGUILayout.HorizontalScope())
            {
                float previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 54f;

                searchText = EditorGUILayout.TextField(
                    "Search",
                    searchText,
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));

                GUILayout.Space(10);

                EditorGUIUtility.labelWidth = previousLabelWidth;
            }

            GUILayout.Space(2);
        }

        private void SetFoldoutsForSide(bool isMono, bool state)
        {
            if (isMono)
            {
                if (_monoPoolsDictionary != null)
                {
                    foreach (string key in _monoPoolsDictionary.Keys.Cast<string>())
                    {
                        SetPoolFoldoutState(true, key, state);
                    }
                }
            }
            else
            {
                if (_objectPoolsDictionary != null)
                {
                    foreach (string key in _objectPoolsDictionary.Keys.Cast<string>())
                    {
                        SetPoolFoldoutState(false, key, state);
                    }
                }
            }
        }

        private void SetPoolFoldoutState(bool isMono, string key, bool expanded)
        {
            if (isMono)
            {
                _monoPoolFoldoutStates[key] = expanded;
                EditorPrefs.SetBool(MonoPrefKeyPrefix + key, expanded);
            }
            else
            {
                _objectPoolFoldoutStates[key] = expanded;
                EditorPrefs.SetBool(ObjectPrefKeyPrefix + key, expanded);
            }
        }

        private void DrawMonoPoolsPanel()
        {
            if (_poolService == null || _monoPoolsDictionary == null)
            {
                EditorGUILayout.HelpBox("PoolService not found in the open scenes, or reflection failed.",
                    MessageType.Warning);
                return;
            }

            string[] poolKeys = _monoPoolsDictionary.Keys.Cast<string>()
                .Where(poolKey => ContainsSubstringIgnoreCase(poolKey, _monoPoolSearchText))
                .OrderBy(poolKey => poolKey, StringComparer.Ordinal)
                .ToArray();

            if (poolKeys.Length == 0)
            {
                EditorGUILayout.HelpBox("No MonoPools registered.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                for (int i = 0; i < poolKeys.Length; i++)
                {
                    string key = poolKeys[i];
                    MonoPool pool = _monoPoolsDictionary[key] as MonoPool;

                    if (pool == null)
                    {
                        continue;
                    }

                    bool expanded = GetPoolFoldoutState(true, key);

                    float headerHeight = Mathf.Max((_headerStyle.fixedHeight > 0 ? _headerStyle.fixedHeight : 20f),
                        EditorGUIUtility.singleLineHeight + 6f);
                    Rect headerRect = GUILayoutUtility.GetRect(1, headerHeight, GUILayout.ExpandWidth(true));

                    if (Event.current.type == EventType.Repaint)
                    {
                        _headerStyle.Draw(headerRect, GUIContent.none, false, false, false, false);
                    }

                    float lift = -1.0f;

                    float foldoutWidth = 16f;
                    Rect foldoutRect = new Rect(
                        headerRect.x + 6f,
                        headerRect.y + ((headerRect.height - EditorGUIUtility.singleLineHeight) * 0.5f) + lift,
                        foldoutWidth,
                        EditorGUIUtility.singleLineHeight);

                    bool newExpanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, true);

                    Rect labelRect = new Rect(
                        foldoutRect.xMax + 4f,
                        headerRect.y + ((headerRect.height - EditorGUIUtility.singleLineHeight) * 0.5f) + lift - 1f,
                        headerRect.width - 12f,
                        EditorGUIUtility.singleLineHeight);

                    EditorGUI.LabelField(labelRect, $"{key} Pool", _headerLabelStyle);

                    if (Event.current.type == EventType.MouseDown &&
                        new Rect(headerRect.x, headerRect.y, headerRect.width, headerRect.height)
                            .Contains(Event.current.mousePosition) &&
                        Event.current.button == 0)
                    {
                        newExpanded = !expanded;
                        Event.current.Use();
                    }

                    if (newExpanded != expanded)
                    {
                        expanded = newExpanded;
                        SetPoolFoldoutState(true, key, expanded);
                    }

                    GUILayout.Space(-(EditorGUIUtility.standardVerticalSpacing + 2));

                    if (expanded)
                    {
                        using (new GUILayout.VerticalScope(_backgroundStyle, GUILayout.ExpandWidth(true)))
                        {
                            int prevIndent = EditorGUI.indentLevel;
                            EditorGUI.indentLevel = 0;

                            int available = GetPrivateCollectionCount(pool, "_availableQueue");
                            int active = GetPrivateCollectionCount(pool, "_active");
                            int totalCreated = GetPrivateInt(pool, "_totalCreatedCount");
                            int initialSize = GetPrivateInt(pool, "_initialSize");
                            int maxSize = GetPrivateInt(pool, "_maxSize");

                            GameObject pooledPrefab = GetPrivateObject<GameObject>(pool, "_prefab");
                            Transform container = GetPrivateObject<Transform>(pool, "_container");

                            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                            {
                                EditorGUILayout.LabelField($"Pool Key: {key}");
                            }

                            GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);

                            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                            {
                                EditorGUILayout.LabelField($"Available: {available}");
                                EditorGUILayout.LabelField($"Active: {active}");
                                EditorGUILayout.LabelField($"Total Created: {totalCreated}");
                                EditorGUILayout.LabelField($"Initial Size: {initialSize}");
                                EditorGUILayout.LabelField($"Max Size: {maxSize}");
                            }

                            GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);

                            using (new EditorGUILayout.HorizontalScope())
                            {
                                using (new EditorGUI.DisabledScope(pool == null))
                                {
                                    if (GUILayout.Button("Select Pool", GUILayout.ExpandWidth(true)))
                                    {
                                        Selection.activeObject = pool;
                                        EditorGUIUtility.PingObject(pool);
                                    }
                                }

                                using (new EditorGUI.DisabledScope(container == null))
                                {
                                    if (GUILayout.Button("Select Container", GUILayout.ExpandWidth(true)))
                                    {
                                        Selection.activeObject = container;
                                        EditorGUIUtility.PingObject(container);
                                    }
                                }

                                using (new EditorGUI.DisabledScope(pooledPrefab == null))
                                {
                                    if (GUILayout.Button("Select Pooled Prefab", GUILayout.ExpandWidth(true)))
                                    {
                                        Selection.activeObject = pooledPrefab;
                                        EditorGUIUtility.PingObject(pooledPrefab);
                                    }
                                }

                                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                                {
                                    if (GUILayout.Button("Return All", GUILayout.ExpandWidth(true)))
                                    {
                                        try
                                        {
                                            pool.ReturnAll();
                                        }
                                        catch
                                        {
                                        }
                                    }
                                }
                            }

                            EditorGUI.indentLevel = prevIndent;
                        }

                        GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
                    }
                    else
                    {
                        const float capHeight = 5f;
                        const float overlap = 0f;
                        const float inset = 0f;

                        Rect capRect = GUILayoutUtility.GetRect(1, capHeight, GUILayout.ExpandWidth(true));
                        capRect = new Rect(capRect.x + inset, capRect.y - overlap, capRect.width - (inset * 2f),
                            capRect.height + overlap);

                        if (Event.current.type == EventType.Repaint)
                        {
                            _backgroundStyle.Draw(capRect, GUIContent.none, false, false, false, false);
                        }

                        GUILayout.Space(EditorGUIUtility.standardVerticalSpacing + 2);
                    }
                }
            }
        }

        private bool GetPoolFoldoutState(bool isMono, string key)
        {
            if (isMono)
            {
                if (_monoPoolFoldoutStates.TryGetValue(key, out bool v))
                {
                    return v;
                }

                bool storedPref = EditorPrefs.GetBool(MonoPrefKeyPrefix + key, false);
                _monoPoolFoldoutStates[key] = storedPref;
                return storedPref;
            }
            else
            {
                if (_objectPoolFoldoutStates.TryGetValue(key, out bool v))
                {
                    return v;
                }

                bool storedPref = EditorPrefs.GetBool(ObjectPrefKeyPrefix + key, false);
                _objectPoolFoldoutStates[key] = storedPref;
                return storedPref;
            }
        }

        private void DrawObjectPoolsPanel()
        {
            if (_poolService == null || _objectPoolsDictionary == null)
            {
                EditorGUILayout.HelpBox("PoolService not found in the open scenes, or reflection failed.",
                    MessageType.Warning);
                return;
            }

            string[] keys = _objectPoolsDictionary.Keys.Cast<string>()
                .Where(poolKey => ContainsSubstringIgnoreCase(poolKey, _objectPoolSearchText))
                .OrderBy(poolKey => poolKey, StringComparer.Ordinal)
                .ToArray();

            if (keys.Length == 0)
            {
                EditorGUILayout.HelpBox("No ObjectPools registered.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    string key = keys[i];
                    object objectPool = _objectPoolsDictionary[key];
                    if (objectPool == null)
                    {
                        continue;
                    }

                    bool expanded = GetPoolFoldoutState(false, key);

                    float headerHeight = Mathf.Max((_headerStyle.fixedHeight > 0 ? _headerStyle.fixedHeight : 20f),
                        EditorGUIUtility.singleLineHeight + 6f);
                    Rect headerRect = GUILayoutUtility.GetRect(1, headerHeight, GUILayout.ExpandWidth(true));

                    if (Event.current.type == EventType.Repaint)
                    {
                        _headerStyle.Draw(headerRect, GUIContent.none, false, false, false, false);
                    }

                    float lift = -1.0f;

                    float foldoutWidth = 16f;
                    Rect foldoutRect = new Rect(
                        headerRect.x + 6f,
                        headerRect.y + ((headerRect.height - EditorGUIUtility.singleLineHeight) * 0.5f) + lift,
                        foldoutWidth,
                        EditorGUIUtility.singleLineHeight);

                    bool newExpanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, true);

                    Rect labelRect = new Rect(
                        foldoutRect.xMax + 4f,
                        headerRect.y + ((headerRect.height - EditorGUIUtility.singleLineHeight) * 0.5f) + lift - 1f,
                        headerRect.width - 12f,
                        EditorGUIUtility.singleLineHeight);

                    EditorGUI.LabelField(labelRect, $"{key} Pool", _headerLabelStyle);

                    if (Event.current.type == EventType.MouseDown &&
                        new Rect(headerRect.x, headerRect.y, headerRect.width, headerRect.height)
                            .Contains(Event.current.mousePosition) &&
                        Event.current.button == 0)
                    {
                        newExpanded = !expanded;
                        Event.current.Use();
                    }

                    if (newExpanded != expanded)
                    {
                        expanded = newExpanded;
                        SetPoolFoldoutState(false, key, expanded);
                    }

                    GUILayout.Space(-(EditorGUIUtility.standardVerticalSpacing + 2));
                    if (expanded)
                    {
                        using (new GUILayout.VerticalScope(_backgroundStyle, GUILayout.ExpandWidth(true)))
                        {
                            int prevIndent = EditorGUI.indentLevel;
                            EditorGUI.indentLevel = 0;

                            int available = SafeGetPropertyInt(objectPool, "AvailableCount");
                            int totalCreated = SafeGetPropertyInt(objectPool, "TotalCreated");
                            int initialSize = GetPrivateInt(objectPool, "_initialSize");
                            int maxSize = SafeGetPropertyInt(objectPool, "MaxSize");

                            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                            {
                                EditorGUILayout.LabelField($"Pool Key: {key}");
                            }

                            GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);

                            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                            {
                                EditorGUILayout.LabelField($"Key: {key}");
                                EditorGUILayout.LabelField($"Available: {available}");
                                EditorGUILayout.LabelField($"Total Created: {totalCreated}");
                                EditorGUILayout.LabelField($"Initial Size: {initialSize}");
                                EditorGUILayout.LabelField($"Max Size: {maxSize}");
                            }

                            GUILayout.Space(2);

                            using (new EditorGUILayout.HorizontalScope())
                            {
                                if (GUILayout.Button("Deregister", GUILayout.Width(110)))
                                {
                                    try
                                    {
                                        MethodInfo deregisterMethodInfo = typeof(PoolService).GetMethod(
                                            "DeregisterObjectPool",
                                            BindingFlags.Public | BindingFlags.Instance,
                                            null,
                                            new[] { typeof(string) },
                                            null);
                                        deregisterMethodInfo?.Invoke(_poolService, new object[] { key });

                                        RefreshTargets();
                                        RestoreFoldoutPrefsForCurrentKeys();
                                    }
                                    catch
                                    {
                                    }
                                }

                                GUILayout.FlexibleSpace();
                            }

                            EditorGUI.indentLevel = prevIndent;
                        }

                        GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
                    }
                    else
                    {
                        const float capHeight = 5f;
                        const float overlap = 0f;
                        const float inset = 0f;

                        Rect capRect = GUILayoutUtility.GetRect(1, capHeight, GUILayout.ExpandWidth(true));
                        capRect = new Rect(capRect.x + inset, capRect.y - overlap, capRect.width - (inset * 2f),
                            capRect.height + overlap);

                        if (Event.current.type == EventType.Repaint)
                        {
                            _backgroundStyle.Draw(capRect, GUIContent.none, false, false, false, false);
                        }

                        GUILayout.Space(EditorGUIUtility.standardVerticalSpacing + 2);
                    }
                }
            }
        }

        #region Utility

        private static GUIStyle GetGUIStyle(string styleName, GUIStyle fallback)
        {
            GUIStyle guiStyle = GUI.skin != null ? GUI.skin.FindStyle(styleName) : null;
            if (guiStyle != null)
            {
                return guiStyle;
            }

            guiStyle = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector)?.FindStyle(styleName);
            if (guiStyle != null)
            {
                return guiStyle;
            }

            guiStyle = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Scene)?.FindStyle(styleName);
            if (guiStyle != null)
            {
                return guiStyle;
            }

            guiStyle = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Game)?.FindStyle(styleName);
            if (guiStyle != null)
            {
                return guiStyle;
            }

            return fallback;
        }
        
        private static bool ContainsSubstringIgnoreCase(string stringToSearch, string substring)
        {
            if (string.IsNullOrEmpty(substring))
            {
                return true;
            }

            if (string.IsNullOrEmpty(stringToSearch))
            {
                return false;
            }

            return stringToSearch.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int GetPrivateCollectionCount(object targetObject, string fieldName)
        {
            if (targetObject == null)
            {
                return 0;
            }

            try
            {
                FieldInfo fieldInfo = targetObject.GetType()
                    .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                object collectionObject = fieldInfo?.GetValue(targetObject);
                if (collectionObject == null)
                {
                    return 0;
                }

                // Works for Queue<T>, List (non-generic ICollection)
                if (collectionObject is ICollection nonGeneric)
                {
                    return nonGeneric.Count;
                }

                // Works for HashSet<T>, ICollection<T>, IReadOnlyCollection<T> etc.
                PropertyInfo countPropertyInfo =
                    collectionObject.GetType().GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);

                if (countPropertyInfo != null && countPropertyInfo.PropertyType == typeof(int))
                {
                    object value = countPropertyInfo.GetValue(collectionObject);
                    if (value is int countInt)
                    {
                        return countInt;
                    }
                }
            }
            catch
            {
            }

            return 0;
        }

        private static int GetPrivateInt(object targetObject, string fieldName)
        {
            if (targetObject == null)
            {
                return 0;
            }

            try
            {
                FieldInfo fieldInfo = targetObject.GetType()
                    .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);

                object value = fieldInfo?.GetValue(targetObject);
                if (value is int valueInt)
                {
                    return valueInt;
                }
            }
            catch
            {
            }

            return 0;
        }

        private static T GetPrivateObject<T>(object targetObject, string fieldName) where T : UnityEngine.Object
        {
            if (targetObject == null)
            {
                return null;
            }

            try
            {
                FieldInfo fieldInfo = targetObject.GetType()
                    .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                return fieldInfo?.GetValue(targetObject) as T;
            }
            catch
            {
                return null;
            }
        }

        private static int SafeGetPropertyInt(object targetObject, string propertyName)
        {
            if (targetObject == null)
            {
                return 0;
            }

            try
            {
                PropertyInfo propertyInfo = targetObject.GetType()
                    .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                object value = propertyInfo?.GetValue(targetObject);
                if (value is int valueInt)
                {
                    return valueInt;
                }
            }
            catch
            {
            }

            return 0;
        }

        #endregion
    }
}