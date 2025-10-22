using System;
using System.Collections.Generic;
using System.Linq;
using AngryKoala.Services;
using UnityEditor;
using UnityEngine;

namespace AngryKoala.Coroutines
{
    public sealed class CoroutineInspectorEditorWindow : EditorWindow
    {
        private ICoroutineService _service;

        private string _searchText;
        private string _tagFilterText;

        private bool _autoRefreshEnabled;
        private double _nextAutoRefreshTime;

        private Vector2 _scroll;

        private readonly HashSet<object> _pendingStopped = new();

        private SortField _sortField;

        private enum SortField
        {
            Owner,
            Tag,
            RoutineType,
            ElapsedTime,
            ElapsedRealtime
        }

        private bool _sortAscending;

        private Texture2D _darkTexture;
        private Texture2D _lightTexture;
        private Texture2D _headerDarkTexture;
        private Texture2D _headerLightTexture;

        private const string AutoRefreshPref = "CoroutineInspector.AutoRefresh";
        private const string SearchStringPref = "CoroutineInspector.Search";
        private const string TagSearchStringPref = "CoroutineInspector.TagSearch";
        private const string SortFieldPref = "CoroutineInspector.SortField";
        private const string SortAscendingPref = "CoroutineInspector.SortAscending";

        private const double AutoRefreshInterval = 0.05f;

        private const float HeaderHeight = 26f;
        private const float HeaderLabelLeftPad = 10f;
        private const float HeaderSpacing = 2f;

        private const float FiltersInitialSpacing = 7f;
        private const float FiltersVerticalSpacing = 6f;

        private const float RowVerticalSpacing = 2f;
        private const float RowHeight = 28f;
        private const float RowInnerPad = 4f;
        private const float CellPadX = 6f;
        private const float ActionsGap = 8f;
        private const float HorizontalPadding = 12f;

        private static GUIStyle _headerStyle;
        private static GUIStyle _backgroundStyle;
        private static GUIStyle _miniButtonStyle;

        private void OnEnable()
        {
            GetService();

            _autoRefreshEnabled = EditorPrefs.GetBool(AutoRefreshPref, true);
            _searchText = EditorPrefs.GetString(SearchStringPref, string.Empty);
            _tagFilterText = EditorPrefs.GetString(TagSearchStringPref, string.Empty);
            _sortField = (SortField)EditorPrefs.GetInt(SortFieldPref, (int)SortField.Owner);
            _sortAscending = EditorPrefs.GetBool(SortAscendingPref, true);

            _nextAutoRefreshTime = EditorApplication.timeSinceStartup + AutoRefreshInterval;

            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        [MenuItem("Angry Koala/Coroutines/Coroutine Inspector")]
        private static void OpenWindow()
        {
            CoroutineInspectorEditorWindow window = GetWindow<CoroutineInspectorEditorWindow>("Coroutine Inspector");

            window.minSize = new Vector2(1200f, 600f);
            window.Show();
        }

        private void GetService()
        {
            if (!EditorApplication.isPlaying)
            {
                _service = null;
                return;
            }

            try
            {
                _service ??= ServiceLocator.Get<ICoroutineService>();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Failed to resolve ICoroutineService via ServiceLocator. {exception.Message}");
                _service = null;
            }
        }

        private void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (_service == null)
            {
                GetService();
            }

            if (!_autoRefreshEnabled)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now >= _nextAutoRefreshTime)
            {
                Repaint();
                _nextAutoRefreshTime = now + AutoRefreshInterval;
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    GetService();
                    Repaint();
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    _service = null;
                    Repaint();
                    break;
            }
        }

        private void OnGUI()
        {
            SetGUIStyles();
            DrawToolbar();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect coroutines.", MessageType.Info);
                return;
            }

            if (_service == null)
            {
                EditorGUILayout.HelpBox("CoroutineService not found in the scene.", MessageType.Warning);
                return;
            }

            GUILayout.Space(FiltersInitialSpacing);
            DrawFilters();

            IReadOnlyList<CoroutineData> rows = _service.GetData() ?? Array.Empty<CoroutineData>();

            if (_pendingStopped.Count > 0)
            {
                rows = rows.Where(coroutineData =>
                    coroutineData.Coroutine != null && !_pendingStopped.Contains(coroutineData.Coroutine)).ToArray();
            }

            if (!string.IsNullOrWhiteSpace(_tagFilterText))
            {
                string tagString = _tagFilterText.Trim();
                rows = rows.Where(coroutineData =>
                        !string.IsNullOrEmpty(coroutineData.Tag) &&
                        coroutineData.Tag.IndexOf(tagString, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToArray();
            }

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                string searchString = _searchText;
                rows = rows.Where(coroutineData =>
                    (!string.IsNullOrEmpty(coroutineData.RoutineTypeName) &&
                     coroutineData.RoutineTypeName.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(coroutineData.Tag) &&
                     coroutineData.Tag.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (coroutineData.Owner != null &&
                     coroutineData.Owner.name.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0)
                ).ToArray();
            }

            rows = SortRows(rows, _sortField, _sortAscending).ToArray();

            GUILayout.Space(HeaderSpacing);

            Rect headerOuter =
                GUILayoutUtility.GetRect(0, HeaderHeight + RowInnerPad * 2f, GUILayout.ExpandWidth(true));
            headerOuter.x += HorizontalPadding;
            headerOuter.width -= HorizontalPadding * 2f;
            GUI.Box(headerOuter, GUIContent.none, EditorStyles.helpBox);

            Rect headerInner = new Rect(
                headerOuter.x + RowInnerPad,
                headerOuter.y + RowInnerPad,
                headerOuter.width - RowInnerPad * 2f,
                HeaderHeight
            );

            float toolbarHeight = EditorStyles.toolbar.fixedHeight > 0 ? EditorStyles.toolbar.fixedHeight : 18f;
            float singleLineHeight = EditorGUIUtility.singleLineHeight;
            float verticalSpacing = EditorGUIUtility.standardVerticalSpacing;

            float filtersInnerHeight = singleLineHeight * 2f + verticalSpacing;
            int paddingTop = EditorStyles.helpBox.padding.top;
            int paddingBottom = EditorStyles.helpBox.padding.bottom;
            float filtersTotalHeight = filtersInnerHeight + paddingTop + paddingBottom + FiltersVerticalSpacing;

            float headerTotalHeight = HeaderHeight + RowInnerPad * 2f;
            float blocksGap = 2f;
            float viewportHeight = position.height - toolbarHeight - filtersTotalHeight - headerTotalHeight - blocksGap;

            bool scrollbarVisible = ScrollbarVisible(rows.Count, RowHeight, RowVerticalSpacing, viewportHeight);

            if (scrollbarVisible)
            {
                float scrollbarWidth = GUI.skin.verticalScrollbar.fixedWidth
                                       + GUI.skin.verticalScrollbar.margin.left
                                       + GUI.skin.verticalScrollbar.margin.right;

                headerInner.width -= scrollbarWidth;
            }

            float baseColumnWidth = Mathf.Floor(headerInner.width / 6f);
            float actionsColumnWidth = headerInner.width - baseColumnWidth * 5f;

            DrawHeaders(headerInner, baseColumnWidth, actionsColumnWidth);
            GUILayout.Space(RowVerticalSpacing);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, false, false, GUILayout.ExpandWidth(true));

            foreach (CoroutineData row in rows)
            {
                DrawRow(row, baseColumnWidth, actionsColumnWidth);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    Repaint();
                }

                GUILayout.FlexibleSpace();

                bool autoRefreshEnabled =
                    GUILayout.Toggle(_autoRefreshEnabled, "Auto-refresh", EditorStyles.toolbarButton);
                if (autoRefreshEnabled != _autoRefreshEnabled)
                {
                    _autoRefreshEnabled = autoRefreshEnabled;
                    EditorPrefs.SetBool(AutoRefreshPref, _autoRefreshEnabled);
                    _nextAutoRefreshTime = EditorApplication.timeSinceStartup + AutoRefreshInterval;
                }
            }
        }

        private void DrawFilters()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(HorizontalPadding);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Search:", GUILayout.Width(60));
                        string nextSearch =
                            EditorGUILayout.TextField(_searchText ?? string.Empty, GUILayout.MinWidth(220));
                        if (nextSearch != _searchText)
                        {
                            _searchText = nextSearch;
                            EditorPrefs.SetString(SearchStringPref, _searchText);
                        }

                        GUILayout.FlexibleSpace();

                        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_tagFilterText)))
                        {
                            if (GUILayout.Button("Stop All With Tag", GUILayout.Width(160)))
                            {
                                StopAllWithTag(_tagFilterText);
                            }
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Tag Filter:", GUILayout.Width(60));
                        string nextTag =
                            EditorGUILayout.TextField(_tagFilterText ?? string.Empty, GUILayout.MinWidth(220));
                        if (nextTag != _tagFilterText)
                        {
                            _tagFilterText = nextTag;
                            EditorPrefs.SetString(TagSearchStringPref, _tagFilterText);
                        }

                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Stop All", GUILayout.Width(100)))
                        {
                            StopAll();
                        }
                    }
                }

                GUILayout.Space(HorizontalPadding - 3);
            }
        }

        private void StopAllWithTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            string trimmedTag = tag.Trim();

            try
            {
                IReadOnlyList<CoroutineData> all = _service?.GetData();
                if (all == null)
                {
                    return;
                }

                foreach (CoroutineData coroutineData in all)
                {
                    if (coroutineData?.Coroutine == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(coroutineData.Tag) && string.Equals(coroutineData.Tag, trimmedTag,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        _service.Stop(coroutineData.Coroutine);
                        _pendingStopped.Add(coroutineData.Coroutine);
                    }
                }

                GUI.changed = true;
                Repaint();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Stop All With Tag failed: {exception.Message}");
            }
        }

        private void StopAll()
        {
            try
            {
                IReadOnlyList<CoroutineData> coroutines = _service?.GetData();
                if (coroutines == null)
                {
                    return;
                }

                foreach (CoroutineData coroutineData in coroutines)
                {
                    if (coroutineData?.Coroutine == null)
                    {
                        continue;
                    }

                    _service.Stop(coroutineData.Coroutine);
                    _pendingStopped.Add(coroutineData.Coroutine);
                }

                GUI.changed = true;
                Repaint();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Stop All failed: {exception.Message}");
            }
        }

        private void DrawHeaders(Rect rect, float baseColumnWidth, float actionsWidth)
        {
            float x = rect.x;

            DrawHeaderCell("Owner", SortField.Owner, new Rect(x, rect.y, baseColumnWidth, rect.height),
                _headerDarkTexture);
            x += baseColumnWidth;

            DrawHeaderCell("Tag", SortField.Tag, new Rect(x, rect.y, baseColumnWidth, rect.height),
                _headerLightTexture);
            x += baseColumnWidth;

            DrawHeaderCell("Routine Type", SortField.RoutineType, new Rect(x, rect.y, baseColumnWidth, rect.height),
                _headerDarkTexture);
            x += baseColumnWidth;

            DrawHeaderCell("Elapsed (Time)", SortField.ElapsedTime, new Rect(x, rect.y, baseColumnWidth, rect.height),
                _headerLightTexture);
            x += baseColumnWidth;

            DrawHeaderCell("Elapsed (Realtime)", SortField.ElapsedRealtime,
                new Rect(x, rect.y, baseColumnWidth, rect.height),
                _headerDarkTexture);
            x += baseColumnWidth;

            Rect actionsRect = new Rect(x, rect.y, actionsWidth, rect.height);
            GUI.DrawTexture(actionsRect, _headerLightTexture, ScaleMode.StretchToFill);

            Rect actionsLabelRect = new Rect(actionsRect.x + HeaderLabelLeftPad, actionsRect.y,
                actionsRect.width - HeaderLabelLeftPad, actionsRect.height);
            GUI.Label(actionsLabelRect, "Actions", _headerStyle);
        }

        private void DrawHeaderCell(string label, SortField field, Rect rect, Texture2D bg)
        {
            GUI.DrawTexture(rect, bg, ScaleMode.StretchToFill);

            Rect labelRect = new Rect(rect.x + HeaderLabelLeftPad, rect.y, rect.width - HeaderLabelLeftPad,
                rect.height);
            GUI.Label(labelRect, label, _headerStyle);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition) &&
                Event.current.button == 0)
            {
                if (_sortField == field)
                {
                    _sortAscending = !_sortAscending;
                }
                else
                {
                    _sortField = field;
                    _sortAscending = true;
                }

                EditorPrefs.SetInt(SortFieldPref, (int)_sortField);
                EditorPrefs.SetBool(SortAscendingPref, _sortAscending);
                Event.current.Use();
            }

            if (_sortField == field)
            {
                string arrow = _sortAscending ? "▲" : "▼";
                Vector2 size = _headerStyle.CalcSize(new GUIContent(arrow));
                Rect arrowRect = new Rect(rect.xMax - size.x - 6, rect.y + (rect.height - size.y) / 2, size.x, size.y);
                GUI.Label(arrowRect, arrow, _headerStyle);
            }
        }

        private static bool ScrollbarVisible(int rowCount, float rowBoxHeight, float rowVSpace,
            float viewportHeightAvailable)
        {
            float perRow = rowBoxHeight + rowVSpace * 2f;
            float contentHeight = rowCount * perRow + 9f;

            return contentHeight > viewportHeightAvailable;
        }

        private void DrawRow(CoroutineData data, float columnWidth, float actionsWidth)
        {
            Rect reserved =
                GUILayoutUtility.GetRect(0, RowHeight + RowVerticalSpacing * 2f, GUILayout.ExpandWidth(true));

            reserved.x += HorizontalPadding;
            reserved.width -= HorizontalPadding * 2f;

            Rect outer = new Rect(reserved.x, reserved.y + RowVerticalSpacing, reserved.width, RowHeight);
            GUI.Box(outer, GUIContent.none, EditorStyles.helpBox);

            Rect inner = new Rect(outer.x + RowInnerPad, outer.y + RowInnerPad, outer.width - RowInnerPad * 2f,
                outer.height - RowInnerPad * 2f);

            float innerX = inner.x;
            float innerHeight = inner.height;

            DrawCellBackground(new Rect(innerX, inner.y, columnWidth, innerHeight), _darkTexture);
            GUI.Label(new Rect(innerX + CellPadX, inner.y, columnWidth - CellPadX * 2f, innerHeight),
                data.Owner != null ? data.Owner.name : "(no owner)", _backgroundStyle);
            innerX += columnWidth;

            DrawCellBackground(new Rect(innerX, inner.y, columnWidth, innerHeight), _lightTexture);
            GUI.Label(new Rect(innerX + CellPadX, inner.y, columnWidth - CellPadX * 2f, innerHeight),
                string.IsNullOrEmpty(data.Tag) ? "(untagged)" : data.Tag, _backgroundStyle);
            innerX += columnWidth;

            DrawCellBackground(new Rect(innerX, inner.y, columnWidth, innerHeight), _darkTexture);
            GUI.Label(new Rect(innerX + CellPadX, inner.y, columnWidth - CellPadX * 2f, innerHeight),
                string.IsNullOrEmpty(data.RoutineTypeName) ? "-" : data.RoutineTypeName, _backgroundStyle);
            innerX += columnWidth;

            DrawCellBackground(new Rect(innerX, inner.y, columnWidth, innerHeight), _lightTexture);
            GUI.Label(new Rect(innerX + CellPadX, inner.y, columnWidth - CellPadX * 2f, innerHeight),
                $"{data.ElapsedTime:F3}",
                _backgroundStyle);
            innerX += columnWidth;

            DrawCellBackground(new Rect(innerX, inner.y, columnWidth, innerHeight), _darkTexture);
            GUI.Label(new Rect(innerX + CellPadX, inner.y, columnWidth - CellPadX * 2f, innerHeight),
                $"{data.ElapsedRealtime:F3}",
                _backgroundStyle);
            innerX += columnWidth;

            Rect actions = new Rect(innerX, inner.y, actionsWidth, innerHeight);
            DrawCellBackground(actions, _lightTexture);

            float pad = 6f;
            float innerWidth = actions.width - pad * 2f - ActionsGap;
            float buttonHeight = Mathf.Max(18f, innerHeight - 6f);
            float buttonY = actions.y + (innerHeight - buttonHeight) * 0.5f;

            Rect pingRect = new Rect(actions.x + pad, buttonY, innerWidth * 0.5f, buttonHeight);
            Rect stopRect = new Rect(pingRect.xMax + ActionsGap, buttonY, innerWidth * 0.5f, buttonHeight);

            if (data.Owner != null && GUI.Button(pingRect, "Ping Owner", _miniButtonStyle))
            {
                EditorGUIUtility.PingObject(data.Owner);
            }

            if (GUI.Button(stopRect, "Stop", _miniButtonStyle))
            {
                try
                {
                    if (data.Coroutine != null)
                    {
                        _service.Stop(data.Coroutine);
                        _pendingStopped.Add(data.Coroutine);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"CoroutineInspector Stop failed: {exception.Message}");
                }

                GUI.changed = true;
                Repaint();
                EditorApplication.delayCall += Repaint;
            }
        }

        #region Utility

        private void SetGUIStyles()
        {
            if (_headerStyle != null)
            {
                return;
            }

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = Mathf.Max(12, EditorStyles.boldLabel.fontSize + 1)
            };

            _backgroundStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft };
            _miniButtonStyle = new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleCenter };

            _darkTexture = SetTexture(new Color(0.18f, 0.18f, 0.18f, 1f));
            _lightTexture = SetTexture(new Color(0.26f, 0.26f, 0.26f, 1f));
            _headerDarkTexture = SetTexture(new Color(0.18f, 0.18f, 0.18f, 1f));
            _headerLightTexture = SetTexture(new Color(0.26f, 0.26f, 0.26f, 1f));
        }

        private static Texture2D SetTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            
            return texture;
        }

        private static IEnumerable<CoroutineData> SortRows(IEnumerable<CoroutineData> source, SortField field, bool asc)
        {
            IOrderedEnumerable<CoroutineData> ordered = field switch
            {
                SortField.Owner => source.OrderBy(
                    coroutineData => coroutineData.Owner != null ? coroutineData.Owner.name : string.Empty,
                    StringComparer.OrdinalIgnoreCase),

                SortField.Tag => source.OrderBy(coroutineData => coroutineData.Tag ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase),

                SortField.RoutineType => source.OrderBy(coroutineData => coroutineData.RoutineTypeName ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase),

                SortField.ElapsedTime => source.OrderBy(coroutineData => coroutineData.ElapsedTime),

                SortField.ElapsedRealtime => source.OrderBy(coroutineData => coroutineData.ElapsedRealtime),
                _ => source.OrderBy(_ => 0)
            };

            return asc ? ordered : ordered.Reverse();
        }

        private static void DrawCellBackground(Rect rect, Texture2D texture)
        {
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill);
        }

        #endregion
    }
}