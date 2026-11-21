using System;
using System.Collections.Generic;
using AngryKoala.Services;
using UnityEditor;
using UnityEngine;

namespace AngryKoala.Pooling
{
    public sealed class PoolInspectorEditorWindow : EditorWindow
    {
        private IPoolService _service;

        private string _searchText;

        private bool _autoRefreshEnabled;
        private double _nextAutoRefreshTime;

        private Vector2 _scroll;

        private readonly List<PoolData> _poolData = new();
        private readonly List<RowEntry> _rows = new();
        private readonly float[] _columnWidths = new float[_columnCount];

        private Comparison<RowEntry> _rowComparison;

        private SortField _sortField;
        private bool _sortAscending;

        private int _poolCount;
        private int _totalInUse;
        private int _totalWaiting;
        private string _summaryText = string.Empty;

        private static GUIStyle _headerStyle;
        private static GUIStyle _cellStyle;
        private static GUIStyle _prefabButtonStyle;
        private static GUIStyle _miniButtonStyle;

        private static GUIContent _ascendingArrowContent;
        private static GUIContent _descendingArrowContent;

        private const string _autoRefreshPref = "PoolInspector.AutoRefresh";
        private const string _searchStringPref = "PoolInspector.Search";
        private const string _sortFieldPref = "PoolInspector.SortField";
        private const string _sortAscendingPref = "PoolInspector.SortAscending";

        private const double _autoRefreshInterval = 0.05d;

        private const int _columnCount = 7;
        private const float _prefabColumnWeight = 3f;
        private const float _numberColumnWeight = 1f;
        private const float _ruleColumnWeight = 1.5f;
        private const float _actionsColumnWeight = 1.5f;
        private const float _totalColumnWeight = _prefabColumnWeight + _numberColumnWeight * 4f + _ruleColumnWeight +
                                                 _actionsColumnWeight;

        private const float _headerHeight = 26f;
        private const float _headerLabelLeftPadding = 10f;
        private const float _headerSpacing = 2f;

        private const float _searchInitialSpacing = 7f;
        private const float _searchVerticalSpacing = 6f;

        private const float _rowVerticalSpacing = 2f;
        private const float _rowHeight = 28f;
        private const float _rowInnerPadding = 4f;
        private const float _cellPaddingX = 6f;
        private const float _actionsPadding = 6f;
        private const float _horizontalPadding = 12f;
        private const float _blocksGap = 2f;
        private const float _defaultToolbarHeight = 18f;
        private const float _minimumButtonHeight = 18f;
        private const float _buttonVerticalInset = 6f;
        private const float _arrowRightPadding = 6f;
        private const float _scrollContentExtraHeight = 9f;

        private const string _missingPrefabLabel = "(missing prefab)";
        private const string _noMaximumLabel = "None";

        private static readonly Color _darkColor = new(0.18f, 0.18f, 0.18f, 1f);
        private static readonly Color _lightColor = new(0.26f, 0.26f, 0.26f, 1f);

        private enum SortField
        {
            Prefab,
            InUse,
            Waiting,
            Total,
            Maximum,
            Rule
        }

        private readonly struct RowEntry
        {
            public PoolData Data { get; }
            public string PrefabName { get; }
            public EntityId PrefabEntityId { get; }

            public RowEntry(PoolData data)
            {
                Data = data;
                PrefabName = data.Prefab != null ? data.Prefab.name : string.Empty;
                PrefabEntityId = data.Prefab != null ? data.Prefab.GetEntityId() : EntityId.None;
            }
        }

        private void OnEnable()
        {
            _rowComparison = CompareRows;

            GetService();

            _autoRefreshEnabled = EditorPrefs.GetBool(_autoRefreshPref, true);
            _searchText = EditorPrefs.GetString(_searchStringPref, string.Empty);
            _sortField = (SortField)EditorPrefs.GetInt(_sortFieldPref, (int)SortField.Prefab);
            _sortAscending = EditorPrefs.GetBool(_sortAscendingPref, true);

            _nextAutoRefreshTime = EditorApplication.timeSinceStartup + _autoRefreshInterval;

            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        [MenuItem("Angry Koala/Pooling/Pool Inspector")]
        private static void OpenWindow()
        {
            PoolInspectorEditorWindow window = GetWindow<PoolInspectorEditorWindow>("Pool Inspector");

            window.minSize = new Vector2(760f, 400f);
            window.Show();
        }

        private void GetService()
        {
            if (!EditorApplication.isPlaying)
            {
                _service = null;
                return;
            }

            if (!HasLiveService())
            {
                ServiceLocator.TryGet(out _service);
            }
        }

        private void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (!HasLiveService())
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
                _nextAutoRefreshTime = now + _autoRefreshInterval;
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
                EditorGUILayout.HelpBox("Enter Play Mode to inspect pools.", MessageType.Info);
                return;
            }

            if (!HasLiveService())
            {
                EditorGUILayout.HelpBox("PoolService not found in the scene.", MessageType.Warning);
                return;
            }

            GUILayout.Space(_searchInitialSpacing);
            DrawSearch();

            if (Event.current.type == EventType.Layout)
            {
                RefreshRows();
            }

            GUILayout.Space(_headerSpacing);

            Rect headerOuter =
                GUILayoutUtility.GetRect(0, _headerHeight + _rowInnerPadding * 2f, GUILayout.ExpandWidth(true));
            headerOuter.x += _horizontalPadding;
            headerOuter.width -= _horizontalPadding * 2f;
            GUI.Box(headerOuter, GUIContent.none, EditorStyles.helpBox);

            var headerInner = new Rect(
                headerOuter.x + _rowInnerPadding,
                headerOuter.y + _rowInnerPadding,
                headerOuter.width - _rowInnerPadding * 2f,
                _headerHeight
            );

            float toolbarHeight = EditorStyles.toolbar.fixedHeight > 0f
                ? EditorStyles.toolbar.fixedHeight
                : _defaultToolbarHeight;
            float searchTotalHeight = EditorGUIUtility.singleLineHeight + EditorStyles.helpBox.padding.top +
                                      EditorStyles.helpBox.padding.bottom + _searchVerticalSpacing;
            float headerTotalHeight = _headerHeight + _rowInnerPadding * 2f;
            float footerHeight = toolbarHeight;
            float viewportHeight = position.height - toolbarHeight - searchTotalHeight - headerTotalHeight -
                                   footerHeight - _blocksGap;

            if (ScrollbarVisible(_rows.Count, viewportHeight))
            {
                float scrollbarWidth = GUI.skin.verticalScrollbar.fixedWidth
                                       + GUI.skin.verticalScrollbar.margin.left
                                       + GUI.skin.verticalScrollbar.margin.right;

                headerInner.width -= scrollbarWidth;
            }

            ComputeColumnWidths(headerInner.width);

            DrawHeaders(headerInner);
            GUILayout.Space(_rowVerticalSpacing);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, false, false, GUILayout.ExpandWidth(true));

            foreach (RowEntry row in _rows)
            {
                DrawRow(row);
            }

            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    Repaint();
                }

                EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying || !HasLiveService());

                if (GUILayout.Button("Return All Pools", EditorStyles.toolbarButton, GUILayout.Width(110)))
                {
                    ReturnAllPools();
                }

                EditorGUI.EndDisabledGroup();

                GUILayout.FlexibleSpace();

                bool autoRefreshEnabled =
                    GUILayout.Toggle(_autoRefreshEnabled, "Auto-refresh", EditorStyles.toolbarButton);
                if (autoRefreshEnabled != _autoRefreshEnabled)
                {
                    _autoRefreshEnabled = autoRefreshEnabled;
                    EditorPrefs.SetBool(_autoRefreshPref, _autoRefreshEnabled);
                    _nextAutoRefreshTime = EditorApplication.timeSinceStartup + _autoRefreshInterval;
                }
            }
        }

        private void ReturnAllPools()
        {
            if (HasLiveService())
            {
                _service.ReturnAll();
            }

            Repaint();
        }

        private void DrawSearch()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(_horizontalPadding);

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
                            EditorPrefs.SetString(_searchStringPref, _searchText);
                        }
                    }
                }

                GUILayout.Space(_horizontalPadding - 3f);
            }
        }

        private void RefreshRows()
        {
            _service.GetData(_poolData);

            _rows.Clear();
            foreach (PoolData data in _poolData)
            {
                _rows.Add(new RowEntry(data));
            }

            UpdateSummary();
            FilterRows();
            _rows.Sort(_rowComparison);
        }

        private void UpdateSummary()
        {
            int totalInUse = 0;
            int totalWaiting = 0;

            foreach (RowEntry row in _rows)
            {
                totalInUse += row.Data.InUseCount;
                totalWaiting += row.Data.WaitingCount;
            }

            if (_rows.Count == _poolCount && totalInUse == _totalInUse && totalWaiting == _totalWaiting)
            {
                return;
            }

            _poolCount = _rows.Count;
            _totalInUse = totalInUse;
            _totalWaiting = totalWaiting;
            _summaryText = $"Pools: {_poolCount}    In Use: {_totalInUse}    Waiting: {_totalWaiting}";
        }

        private void FilterRows()
        {
            if (string.IsNullOrWhiteSpace(_searchText))
            {
                return;
            }

            for (int i = _rows.Count - 1; i >= 0; i--)
            {
                if (!Contains(_rows[i].PrefabName, _searchText))
                {
                    _rows.RemoveAt(i);
                }
            }
        }

        private int CompareRows(RowEntry left, RowEntry right)
        {
            PoolData leftData = left.Data;
            PoolData rightData = right.Data;

            int result = _sortField switch
            {
                SortField.Prefab => string.Compare(left.PrefabName, right.PrefabName,
                    StringComparison.OrdinalIgnoreCase),
                SortField.InUse => leftData.InUseCount.CompareTo(rightData.InUseCount),
                SortField.Waiting => leftData.WaitingCount.CompareTo(rightData.WaitingCount),
                SortField.Total => GetTotalCount(leftData).CompareTo(GetTotalCount(rightData)),
                SortField.Maximum => GetMaximumSortValue(leftData).CompareTo(GetMaximumSortValue(rightData)),
                SortField.Rule => leftData.ExhaustionRule.CompareTo(rightData.ExhaustionRule),
                _ => 0
            };

            if (result != 0)
            {
                return _sortAscending ? result : -result;
            }

            result = string.Compare(left.PrefabName, right.PrefabName, StringComparison.OrdinalIgnoreCase);
            if (result == 0)
            {
                result = left.PrefabEntityId.CompareTo(right.PrefabEntityId);
            }

            return result;
        }

        private void ComputeColumnWidths(float totalWidth)
        {
            float unit = Mathf.Floor(totalWidth / _totalColumnWeight);

            _columnWidths[(int)SortField.Prefab] = Mathf.Floor(unit * _prefabColumnWeight);
            _columnWidths[(int)SortField.InUse] = unit * _numberColumnWeight;
            _columnWidths[(int)SortField.Waiting] = unit * _numberColumnWeight;
            _columnWidths[(int)SortField.Total] = unit * _numberColumnWeight;
            _columnWidths[(int)SortField.Maximum] = unit * _numberColumnWeight;
            _columnWidths[(int)SortField.Rule] = Mathf.Floor(unit * _ruleColumnWeight);

            float used = 0f;
            for (int i = 0; i < _columnCount - 1; i++)
            {
                used += _columnWidths[i];
            }

            _columnWidths[_columnCount - 1] = totalWidth - used;
        }

        private void DrawHeaders(Rect rect)
        {
            float x = rect.x;

            for (int i = 0; i < _columnCount - 1; i++)
            {
                var field = (SortField)i;
                Color background = i % 2 == 0 ? _darkColor : _lightColor;

                DrawHeaderCell(GetHeaderLabel(field), field, new Rect(x, rect.y, _columnWidths[i], rect.height),
                    background);
                x += _columnWidths[i];
            }

            var actionsRect = new Rect(x, rect.y, _columnWidths[_columnCount - 1], rect.height);
            EditorGUI.DrawRect(actionsRect, _lightColor);

            var actionsLabelRect = new Rect(actionsRect.x + _headerLabelLeftPadding, actionsRect.y,
                actionsRect.width - _headerLabelLeftPadding, actionsRect.height);
            GUI.Label(actionsLabelRect, "Actions", _headerStyle);
        }

        private void DrawHeaderCell(string label, SortField field, Rect rect, Color background)
        {
            EditorGUI.DrawRect(rect, background);

            var labelRect = new Rect(rect.x + _headerLabelLeftPadding, rect.y, rect.width - _headerLabelLeftPadding,
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

                EditorPrefs.SetInt(_sortFieldPref, (int)_sortField);
                EditorPrefs.SetBool(_sortAscendingPref, _sortAscending);
                Event.current.Use();
            }

            if (_sortField == field)
            {
                GUIContent arrow = _sortAscending ? _ascendingArrowContent : _descendingArrowContent;
                Vector2 size = _headerStyle.CalcSize(arrow);
                var arrowRect = new Rect(rect.xMax - size.x - _arrowRightPadding, rect.y + (rect.height - size.y) / 2f,
                    size.x, size.y);
                GUI.Label(arrowRect, arrow, _headerStyle);
            }
        }

        private void DrawRow(RowEntry row)
        {
            PoolData data = row.Data;

            Rect reserved =
                GUILayoutUtility.GetRect(0, _rowHeight + _rowVerticalSpacing * 2f, GUILayout.ExpandWidth(true));

            reserved.x += _horizontalPadding;
            reserved.width -= _horizontalPadding * 2f;

            var outer = new Rect(reserved.x, reserved.y + _rowVerticalSpacing, reserved.width, _rowHeight);
            GUI.Box(outer, GUIContent.none, EditorStyles.helpBox);

            var inner = new Rect(outer.x + _rowInnerPadding, outer.y + _rowInnerPadding, outer.width - _rowInnerPadding * 2f,
                outer.height - _rowInnerPadding * 2f);

            bool hasPrefab = data.Prefab != null;
            float x = inner.x;

            for (int i = 0; i < _columnCount; i++)
            {
                var cell = new Rect(x, inner.y, _columnWidths[i], inner.height);
                EditorGUI.DrawRect(cell, i % 2 == 0 ? _darkColor : _lightColor);

                var content = new Rect(cell.x + _cellPaddingX, cell.y, cell.width - _cellPaddingX * 2f, cell.height);

                switch (i)
                {
                    case (int)SortField.Prefab:
                        DrawPrefabCell(content, row, hasPrefab);
                        break;

                    case (int)SortField.InUse:
                        GUI.Label(content, data.InUseCount.ToString(), _cellStyle);
                        break;

                    case (int)SortField.Waiting:
                        GUI.Label(content, data.WaitingCount.ToString(), _cellStyle);
                        break;

                    case (int)SortField.Total:
                        GUI.Label(content, GetTotalCount(data).ToString(), _cellStyle);
                        break;

                    case (int)SortField.Maximum:
                        GUI.Label(content, data.MaximumSize == 0 ? _noMaximumLabel : data.MaximumSize.ToString(),
                            _cellStyle);
                        break;

                    case (int)SortField.Rule:
                        GUI.Label(content, GetRuleLabel(data.ExhaustionRule), _cellStyle);
                        break;

                    default:
                        DrawActionsCell(cell, data, hasPrefab);
                        break;
                }

                x += _columnWidths[i];
            }
        }

        private void DrawPrefabCell(Rect content, RowEntry row, bool hasPrefab)
        {
            if (!hasPrefab)
            {
                GUI.Label(content, _missingPrefabLabel, _cellStyle);
                return;
            }

            if (GUI.Button(content, row.PrefabName, _prefabButtonStyle))
            {
                EditorGUIUtility.PingObject(row.Data.Prefab);
            }
        }

        private void DrawActionsCell(Rect cell, PoolData data, bool hasPrefab)
        {
            float buttonHeight = Mathf.Max(_minimumButtonHeight, cell.height - _buttonVerticalInset);
            var buttonRect = new Rect(cell.x + _actionsPadding, cell.y + (cell.height - buttonHeight) * 0.5f,
                cell.width - _actionsPadding * 2f, buttonHeight);

            EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying || !hasPrefab);

            if (GUI.Button(buttonRect, "Return All", _miniButtonStyle))
            {
                ReturnAllOfPrefab(data.Prefab);
            }

            EditorGUI.EndDisabledGroup();
        }

        private void ReturnAllOfPrefab(GameObject prefab)
        {
            if (prefab == null || !HasLiveService())
            {
                return;
            }

            _service.ReturnAll(prefab.transform);

            Repaint();
        }

        private void DrawFooter()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(_summaryText, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
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

            _cellStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft };
            _prefabButtonStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold
            };
            _miniButtonStyle = new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleCenter };

            _ascendingArrowContent = new GUIContent("▲");
            _descendingArrowContent = new GUIContent("▼");
        }

        private bool HasLiveService()
        {
            return _service is UnityEngine.Object serviceObject ? serviceObject != null : _service != null;
        }

        private static bool ScrollbarVisible(int rowCount, float viewportHeightAvailable)
        {
            float perRow = _rowHeight + _rowVerticalSpacing * 2f;
            float contentHeight = rowCount * perRow + _scrollContentExtraHeight;

            return contentHeight > viewportHeightAvailable;
        }

        private static string GetHeaderLabel(SortField field)
        {
            return field switch
            {
                SortField.Prefab => "Prefab",
                SortField.InUse => "In Use",
                SortField.Waiting => "Waiting",
                SortField.Total => "Total",
                SortField.Maximum => "Maximum",
                SortField.Rule => "Rule",
                _ => string.Empty
            };
        }

        private static string GetRuleLabel(PoolExhaustionRule rule)
        {
            return rule switch
            {
                PoolExhaustionRule.Grow => "Grow",
                PoolExhaustionRule.ReturnNull => "Return Null",
                PoolExhaustionRule.RecycleOldest => "Recycle Oldest",
                _ => "-"
            };
        }

        private static int GetTotalCount(PoolData data)
        {
            return data.InUseCount + data.WaitingCount;
        }

        private static int GetMaximumSortValue(PoolData data)
        {
            return data.MaximumSize == 0 ? int.MaxValue : data.MaximumSize;
        }

        private static bool Contains(string text, string value)
        {
            return !string.IsNullOrEmpty(text) && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion
    }
}
