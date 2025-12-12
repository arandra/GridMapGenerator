using System;
using System.Collections.Generic;
using System.Linq;
using GridMapGenerator.Data;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GridMapGenerator.Editor
{
    public sealed class WfcRuleGridEditorWindow : EditorWindow
    {
        private const string PrefsKey = "GridMapGenerator.WfcRuleGridEditorWindow.State";
        private const int DefaultWidth = 4;
        private const int DefaultHeight = 4;
        private const float MinCellSize = 0.1f;

        private TileSetData tileSet;
        private WfcTileRules outputRules;

        private int gridWidth = DefaultWidth;
        private int gridHeight = DefaultHeight;
        private float cellSize = 1f;
        private ForwardAxis forwardAxis = ForwardAxis.Up;
        private bool useXZPlane = true;
        private bool previewWithMeshes = true;

        private string[,] grid;
        private int brushIndex = -1; // -1 = empty

        private Vector2 paletteScroll;
        private Vector2 gridScroll;
        private Vector2 previewOrbit = new(30f, 30f); // pitch, yaw
        private float previewDistance = 8f;

        private PreviewRenderUtility previewUtility;
        private Material solidMaterial;
        private Material wireMaterial;
        private readonly List<GameObject> previewObjects = new();

        [MenuItem("Grid Map Generator/WFC Rule Grid Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<WfcRuleGridEditorWindow>();
            window.titleContent = new GUIContent("WFC Rule Grid");
            window.minSize = new Vector2(720, 520);
            window.Show();
        }

        private void OnEnable()
        {
            LoadState();
            CreatePreviewUtility();
        }

        private void OnDisable()
        {
            CleanupPreviewObjects();
            DestroyMaterials();
            DisposePreviewUtility();
            SaveState();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("TileSet을 사용해 샘플 그리드를 편집하고 WFC 규칙을 생성합니다.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            DrawAssetSelectors();
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.MaxWidth(position.width * 0.45f)))
                {
                    DrawGridControls();
                    EditorGUILayout.Space();
                    DrawPalette();
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    DrawPreviewControls();
                    var previewRect = GUILayoutUtility.GetRect(200, 320, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    DrawPreview(previewRect);
                }
            }

            EditorGUILayout.Space();
            DrawGenerateControls();
        }

        private void DrawAssetSelectors()
        {
            tileSet = (TileSetData)EditorGUILayout.ObjectField("Tile Set", tileSet, typeof(TileSetData), false);
            outputRules = (WfcTileRules)EditorGUILayout.ObjectField("Target WFC Rules", outputRules, typeof(WfcTileRules), false);

            using (new EditorGUILayout.HorizontalScope())
            {
                cellSize = Mathf.Max(MinCellSize, EditorGUILayout.FloatField(new GUIContent("Cell Size"), cellSize));
                useXZPlane = EditorGUILayout.ToggleLeft(new GUIContent("XZ Plane", "XZ면을 위에서 보는 좌표"), useXZPlane, GUILayout.Width(90));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                gridWidth = Mathf.Max(1, EditorGUILayout.IntField("Grid Width", gridWidth));
                gridHeight = Mathf.Max(1, EditorGUILayout.IntField("Grid Height", gridHeight));
                if (GUILayout.Button("크기 적용", GUILayout.Width(80)))
                {
                    ResizeGrid(gridWidth, gridHeight);
                    Repaint();
                }
            }

            forwardAxis = (ForwardAxis)EditorGUILayout.EnumPopup(new GUIContent("Forward Axis", "Forward가 위쪽(Up, y+)인지 오른쪽(Right, x+)인지 선택"), forwardAxis);
        }

        private void DrawPalette()
        {
            EditorGUILayout.LabelField("타일 선택", EditorStyles.boldLabel);
            if (tileSet == null || tileSet.Tiles == null || tileSet.Tiles.Count == 0)
            {
                EditorGUILayout.HelpBox("TileSetData에 타일이 없습니다.", MessageType.Info);
                return;
            }

            var options = tileSet.Tiles
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.TypeId))
                .Select(t => t.TypeId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            if (options.Count == 0)
            {
                EditorGUILayout.HelpBox("유효한 TypeId가 없습니다.", MessageType.Info);
                return;
            }

            const float buttonHeight = 22f;
            paletteScroll = EditorGUILayout.BeginScrollView(paletteScroll, GUILayout.Height(120));
            for (int i = 0; i < options.Count; i++)
            {
                var id = options[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool selected = brushIndex == i;
                    if (GUILayout.Toggle(selected, id, "Button", GUILayout.Height(buttonHeight)))
                    {
                        brushIndex = i;
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("지우개", GUILayout.Height(buttonHeight)))
                {
                    brushIndex = -1;
                }

                if (GUILayout.Button("그리드 초기화", GUILayout.Height(buttonHeight)))
                {
                    InitGrid(gridWidth, gridHeight);
                }
            }
        }

        private void DrawGridControls()
        {
            EditorGUILayout.LabelField("샘플 그리드", EditorStyles.boldLabel);
            var forwardLabel = forwardAxis == ForwardAxis.Up ? "앞(+Z)=위, 뒤(-Z)=아래" : "앞(+Z)=오른쪽, 뒤(-Z)=왼쪽";
            EditorGUILayout.HelpBox($"좌(-X) ←→ 우(+X), {forwardLabel}", MessageType.None);

            if (grid == null)
            {
                InitGrid(gridWidth, gridHeight);
            }

            const float cellSizePx = 36f;
            gridScroll = EditorGUILayout.BeginScrollView(gridScroll, GUILayout.Height(260));

            for (int y = gridHeight - 1; y >= 0; y--)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(y.ToString(), GUILayout.Width(24));
                    for (int x = 0; x < gridWidth; x++)
                    {
                        var typeId = grid[x, y];
                        string label = string.IsNullOrEmpty(typeId) ? "." : typeId;
                        var style = new GUIStyle(GUI.skin.button)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            fontSize = 10,
                            wordWrap = false
                        };

                        var content = new GUIContent(label);
                        if (GUILayout.Button(content, style, GUILayout.Width(cellSizePx), GUILayout.Height(cellSizePx)))
                        {
                            ApplyBrush(x, y);
                        }
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(" ", GUILayout.Width(24));
                for (int x = 0; x < gridWidth; x++)
                {
                    GUILayout.Label(x.ToString(), GUILayout.Width(cellSizePx));
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPreviewControls()
        {
            EditorGUILayout.LabelField("프리뷰", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                previewWithMeshes = EditorGUILayout.ToggleLeft(new GUIContent("모델 프리뷰", "Prefab이 없으면 색상 박스로 표시"), previewWithMeshes, GUILayout.Width(110));
                previewDistance = EditorGUILayout.Slider("거리", previewDistance, 2f, 40f);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                previewOrbit.x = EditorGUILayout.Slider("Pitch", previewOrbit.x, -89f, 89f);
                previewOrbit.y = EditorGUILayout.Slider("Yaw", previewOrbit.y, -180f, 180f);
            }
        }

        private void DrawPreview(Rect rect)
        {
            if (Event.current.type == EventType.Repaint)
            {
                EnsurePreviewUtility();
                RenderPreview(rect);
            }
            else
            {
                EditorGUI.DrawRect(rect, Color.black * 0.2f);
            }
        }

        private void RenderPreview(Rect rect)
        {
            CleanupPreviewObjects();

            previewUtility.BeginPreview(rect, GUIStyle.none);
            previewUtility.camera.farClipPlane = 1000f;
            previewUtility.camera.nearClipPlane = 0.1f;
            previewUtility.camera.clearFlags = CameraClearFlags.Color;
            previewUtility.camera.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);

            var center = new Vector3((gridWidth - 1) * 0.5f * cellSize, 0f, (gridHeight - 1) * 0.5f * cellSize);
            var pitchRad = Mathf.Deg2Rad * previewOrbit.x;
            var yawRad = Mathf.Deg2Rad * previewOrbit.y;
            var dir = new Vector3(
                Mathf.Cos(pitchRad) * Mathf.Sin(yawRad),
                Mathf.Sin(pitchRad),
                Mathf.Cos(pitchRad) * Mathf.Cos(yawRad));
            var camPos = center - dir * previewDistance;

            previewUtility.camera.transform.position = camPos;
            previewUtility.camera.transform.LookAt(center, Vector3.up);

            // 기본 조명
            var light0 = previewUtility.lights[0];
            light0.intensity = 1.1f;
            light0.transform.rotation = Quaternion.Euler(50f, 50f, 0f);
            previewUtility.lights[0] = light0;

            if (previewUtility.lights.Length > 1)
            {
                var light1 = previewUtility.lights[1];
                light1.intensity = 0.6f;
                light1.transform.rotation = Quaternion.Euler(340f, 218f, 177f);
                previewUtility.lights[1] = light1;
            }

            RenderPreviewGrid(center);

            previewUtility.camera.Render();
            previewUtility.EndAndDrawPreview(rect);
        }

        private void RenderPreviewGrid(Vector3 center)
        {
            if (grid == null) return;

            var validBindings = GetValidBindings();
            var missingColor = new Color(0.4f, 0.1f, 0.1f, 1f);

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    var typeId = grid[x, y];
                    if (string.IsNullOrWhiteSpace(typeId))
                    {
                        continue;
                    }

                    var pos = ToWorldPosition(x, y);
                    if (previewWithMeshes && validBindings.TryGetValue(typeId, out var binding) && binding.Prefab != null)
                    {
                        var go = (GameObject)Object.Instantiate(binding.Prefab);
                        if (go != null)
                        {
                            go.hideFlags = HideFlags.HideAndDontSave;
                            go.transform.position = pos;
                            go.transform.rotation = Quaternion.identity;
                            previewUtility.AddSingleGO(go);
                            previewObjects.Add(go);
                        }
                    }
                    else
                    {
                        // 색상 박스 렌더
                        var color = Color.HSVToRGB(Mathf.Repeat(typeId.GetHashCode() * 0.01f, 1f), 0.5f, 0.9f);
                        DrawCubeGizmo(pos, cellSize * 0.45f, color);
                        if (previewWithMeshes)
                        {
                            DrawCubeWire(pos, cellSize * 0.5f, missingColor);
                        }
                    }
                }
            }

            DrawGridLines(center);
        }

        private void DrawGridLines(Vector3 center)
        {
            var totalW = gridWidth * cellSize;
            var totalH = gridHeight * cellSize;
            var origin = new Vector3(center.x - (gridWidth - 1) * 0.5f * cellSize, 0f, center.z - (gridHeight - 1) * 0.5f * cellSize);
            var color = new Color(1f, 1f, 1f, 0.2f);
            Handles.color = color;
            for (int x = 0; x <= gridWidth; x++)
            {
                var start = origin + new Vector3((x - 0.5f) * cellSize, 0f, -0.5f * cellSize);
                var end = start + new Vector3(0f, 0f, totalH);
                Handles.DrawLine(start, end);
            }

            for (int y = 0; y <= gridHeight; y++)
            {
                var start = origin + new Vector3(-0.5f * cellSize, 0f, (y - 0.5f) * cellSize);
                var end = start + new Vector3(totalW, 0f, 0f);
                Handles.DrawLine(start, end);
            }
        }

        private void DrawCubeGizmo(Vector3 position, float size, Color color)
        {
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (cube == null) return;
            EnsureMaterials();

            solidMaterial.color = color;
            var matrix = Matrix4x4.TRS(position, useXZPlane ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f), Vector3.one * size * 2f);
            previewUtility.DrawMesh(cube, matrix, solidMaterial, 0);
        }

        private void DrawCubeWire(Vector3 position, float size, Color color)
        {
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (cube == null) return;
            EnsureMaterials();

            wireMaterial.color = color;
            var matrix = Matrix4x4.TRS(position, useXZPlane ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f), Vector3.one * size * 2f);
            previewUtility.DrawMesh(cube, matrix, wireMaterial, 0);
        }

        private void EnsureMaterials()
        {
            if (solidMaterial == null)
            {
                solidMaterial = new Material(Shader.Find("Standard"));
                solidMaterial.SetFloat("_Glossiness", 0.2f);
                solidMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            if (wireMaterial == null)
            {
                wireMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
                wireMaterial.SetFloat("_ZWrite", 0f);
                wireMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                wireMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                wireMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private void ApplyBrush(int x, int y)
        {
            if (grid == null) return;
            grid[x, y] = GetBrushTypeId();
            Repaint();
        }

        private string GetBrushTypeId()
        {
            var options = tileSet?.Tiles?
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.TypeId))
                .Select(t => t.TypeId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            if (options == null || options.Count == 0) return null;
            if (brushIndex < 0 || brushIndex >= options.Count) return null;
            return options[brushIndex];
        }

        private void DrawGenerateControls()
        {
            EditorGUILayout.LabelField("규칙 생성", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("새 WFC Rules 자산 생성", GUILayout.Width(180)))
                {
                    CreateNewRulesAsset();
                }

                if (GUILayout.Button("규칙 생성/덮어쓰기", GUILayout.Height(32)))
                {
                    GenerateRules();
                }
            }
        }

        private void GenerateRules()
        {
            if (outputRules == null)
            {
                EditorUtility.DisplayDialog("대상 없음", "Target WFC Rules 자산을 지정하거나 생성하세요.", "확인");
                return;
            }

            if (grid == null)
            {
                EditorUtility.DisplayDialog("그리드 없음", "그리드를 초기화할 수 없습니다.", "확인");
                return;
            }

            var directional = new Dictionary<string, DirectionalNeighbors>();
            var counts = new Dictionary<string, int>();

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    var typeId = grid[x, y];
                    if (string.IsNullOrWhiteSpace(typeId))
                    {
                        continue;
                    }

                    counts.TryAdd(typeId, 0);
                    counts[typeId]++;

                    foreach (var (nx, ny) in EnumerateNeighbors(x, y))
                    {
                        var neighbor = grid[nx, ny];
                        if (string.IsNullOrWhiteSpace(neighbor))
                        {
                            continue;
                        }

                        var direction = ToDirection(x, y, nx, ny);
                        AddDirectionalNeighbor(directional, typeId, neighbor, direction);
                        AddDirectionalNeighbor(directional, neighbor, typeId, Opposite(direction));
                    }
                }
            }

            if (counts.Count == 0)
            {
                EditorUtility.DisplayDialog("비어 있음", "채워진 셀이 없습니다.", "확인");
                return;
            }

            Undo.RegisterCompleteObjectUndo(outputRules, "Generate WFC Rules");
            outputRules.Tiles.Clear();

            foreach (var kv in counts.OrderBy(k => k.Key))
            {
                directional.TryGetValue(kv.Key, out var allowed);
                var rule = new WfcTileRule
                {
                    TypeId = kv.Key,
                    IsJoker = false,
                    Weight = kv.Value,
                    AllowedLeftNeighbors = allowed?.Left.ToList() ?? new List<string>(),
                    AllowedRightNeighbors = allowed?.Right.ToList() ?? new List<string>(),
                    AllowedForwardNeighbors = allowed?.Forward.ToList() ?? new List<string>(),
                    AllowedBackwardNeighbors = allowed?.Backward.ToList() ?? new List<string>()
                };
                outputRules.Tiles.Add(rule);
            }

            EditorUtility.SetDirty(outputRules);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "생성 완료",
                $"타입 {counts.Count}개, 셀 {counts.Values.Sum()}개에서 규칙을 생성했습니다.",
                "확인");
        }

        private IEnumerable<(int x, int y)> EnumerateNeighbors(int x, int y)
        {
            if (forwardAxis == ForwardAxis.Up)
            {
                if (x > 0) yield return (x - 1, y);
                if (x < gridWidth - 1) yield return (x + 1, y);
                if (y > 0) yield return (x, y - 1);
                if (y < gridHeight - 1) yield return (x, y + 1);
            }
            else // Forward = Right
            {
                if (y > 0) yield return (x, y - 1);           // Left in world becomes Down
                if (y < gridHeight - 1) yield return (x, y + 1); // Right in world becomes Up
                if (x > 0) yield return (x - 1, y);           // Backward
                if (x < gridWidth - 1) yield return (x + 1, y); // Forward
            }
        }

        private void AddDirectionalNeighbor(Dictionary<string, DirectionalNeighbors> map, string source, string neighbor, WfcDirection direction)
        {
            if (!map.TryGetValue(source, out var dirs))
            {
                dirs = new DirectionalNeighbors();
                map[source] = dirs;
            }

            dirs.Add(direction, neighbor);
        }

        private WfcDirection ToDirection(int x, int y, int nx, int ny)
        {
            if (forwardAxis == ForwardAxis.Up)
            {
                if (nx < x) return WfcDirection.Left;
                if (nx > x) return WfcDirection.Right;
                if (ny < y) return WfcDirection.Backward;
                return WfcDirection.Forward;
            }
            else // Forward = Right
            {
                // Forward = +X, Backward = -X, Left = -Y, Right = +Y in grid indices
                if (ny < y) return WfcDirection.Left;
                if (ny > y) return WfcDirection.Right;
                if (nx < x) return WfcDirection.Backward;
                return WfcDirection.Forward;
            }
        }

        private WfcDirection Opposite(WfcDirection direction)
        {
            return direction switch
            {
                WfcDirection.Left => WfcDirection.Right,
                WfcDirection.Right => WfcDirection.Left,
                WfcDirection.Forward => WfcDirection.Backward,
                WfcDirection.Backward => WfcDirection.Forward,
                _ => WfcDirection.Left
            };
        }

        private void CreateNewRulesAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create WFC Tile Rules",
                "WfcTileRules",
                "asset",
                "WFC 규칙 자산을 저장할 위치를 선택하세요.");

            if (string.IsNullOrEmpty(path)) return;

            var asset = CreateInstance<WfcTileRules>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            outputRules = asset;
            Selection.activeObject = asset;
        }

        private void InitGrid(int width, int height)
        {
            gridWidth = Mathf.Max(1, width);
            gridHeight = Mathf.Max(1, height);
            grid = new string[gridWidth, gridHeight];
            SaveState();
        }

        private void ResizeGrid(int newWidth, int newHeight)
        {
            var oldWidth = grid?.GetLength(0) ?? 0;
            var oldHeight = grid?.GetLength(1) ?? 0;

            var newGrid = new string[newWidth, newHeight];
            for (int y = 0; y < Mathf.Min(oldHeight, newHeight); y++)
            {
                for (int x = 0; x < Mathf.Min(oldWidth, newWidth); x++)
                {
                    newGrid[x, y] = grid[x, y];
                }
            }

            gridWidth = newWidth;
            gridHeight = newHeight;
            grid = newGrid;
            SaveState();
        }

        private Vector3 ToWorldPosition(int x, int y)
        {
            Vector3 pos;
            if (forwardAxis == ForwardAxis.Up)
            {
                pos = new Vector3(x * cellSize, 0f, y * cellSize);
                if (!useXZPlane)
                {
                    pos = new Vector3(x * cellSize, y * cellSize, 0f);
                }
            }
            else
            {
                // Forward=Right → swap x/z for preview consistency
                pos = new Vector3(y * cellSize, 0f, x * cellSize);
                if (!useXZPlane)
                {
                    pos = new Vector3(y * cellSize, x * cellSize, 0f);
                }
            }
            return pos;
        }

        private Dictionary<string, TilePrefabBinding> GetValidBindings()
        {
            var result = new Dictionary<string, TilePrefabBinding>();
            if (tileSet == null || tileSet.Tiles == null) return result;

            foreach (var binding in tileSet.Tiles)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.TypeId)) continue;
                if (result.ContainsKey(binding.TypeId)) continue;
                result[binding.TypeId] = binding;
            }

            return result;
        }

        private void EnsurePreviewUtility()
        {
            if (previewUtility == null)
            {
                CreatePreviewUtility();
            }
        }

        private void CreatePreviewUtility()
        {
            DisposePreviewUtility();
            previewUtility = new PreviewRenderUtility();
            previewUtility.camera.fieldOfView = 30f;
            previewUtility.camera.allowMSAA = true;
        }

        #region State Save/Load
        [Serializable]
        private class WindowState
        {
            public int GridWidth;
            public int GridHeight;
            public float CellSize;
            public int ForwardAxis;
            public bool UseXZPlane;
            public bool PreviewWithMeshes;
            public Vector2 PreviewOrbit;
            public float PreviewDistance;
            public int BrushIndex;
            public string[,] Grid;
            public string TileSetGuid;
            public string OutputRulesGuid;
        }

        private void SaveState()
        {
            try
            {
                var state = new WindowState
                {
                    GridWidth = gridWidth,
                    GridHeight = gridHeight,
                    CellSize = cellSize,
                    ForwardAxis = (int)forwardAxis,
                    UseXZPlane = useXZPlane,
                    PreviewWithMeshes = previewWithMeshes,
                    PreviewOrbit = previewOrbit,
                    PreviewDistance = previewDistance,
                    BrushIndex = brushIndex,
                    Grid = grid,
                    TileSetGuid = tileSet != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tileSet)) : null,
                    OutputRulesGuid = outputRules != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(outputRules)) : null
                };

                var json = JsonUtility.ToJson(new SerializableState(state));
                EditorPrefs.SetString(PrefsKey, json);
            }
            catch
            {
                // 저장 실패 시 무시
            }
        }

        private void LoadState()
        {
            var json = EditorPrefs.GetString(PrefsKey, null);
            if (string.IsNullOrEmpty(json))
            {
                InitGrid(gridWidth, gridHeight);
                return;
            }

            try
            {
                var serializable = JsonUtility.FromJson<SerializableState>(json);
                var state = serializable?.ToState();
                if (state == null)
                {
                    InitGrid(gridWidth, gridHeight);
                    return;
                }

                gridWidth = Mathf.Max(1, state.GridWidth);
                gridHeight = Mathf.Max(1, state.GridHeight);
                cellSize = Mathf.Max(MinCellSize, state.CellSize <= 0f ? cellSize : state.CellSize);
                forwardAxis = (ForwardAxis)state.ForwardAxis;
                useXZPlane = state.UseXZPlane;
                previewWithMeshes = state.PreviewWithMeshes;
                previewOrbit = state.PreviewOrbit;
                previewDistance = state.PreviewDistance > 0f ? state.PreviewDistance : previewDistance;
                brushIndex = state.BrushIndex;

                if (state.Grid != null &&
                    state.Grid.GetLength(0) == gridWidth &&
                    state.Grid.GetLength(1) == gridHeight)
                {
                    grid = state.Grid;
                }
                else
                {
                    InitGrid(gridWidth, gridHeight);
                }

                if (!string.IsNullOrEmpty(state.TileSetGuid))
                {
                    var path = AssetDatabase.GUIDToAssetPath(state.TileSetGuid);
                    tileSet = AssetDatabase.LoadAssetAtPath<TileSetData>(path);
                }

                if (!string.IsNullOrEmpty(state.OutputRulesGuid))
                {
                    var path = AssetDatabase.GUIDToAssetPath(state.OutputRulesGuid);
                    outputRules = AssetDatabase.LoadAssetAtPath<WfcTileRules>(path);
                }
            }
            catch
            {
                InitGrid(gridWidth, gridHeight);
            }
        }

        [Serializable]
        private class SerializableState
        {
            public int GridWidth;
            public int GridHeight;
            public float CellSize;
            public bool UseXZPlane;
            public bool PreviewWithMeshes;
            public Vector2 PreviewOrbit;
            public float PreviewDistance;
            public int BrushIndex;
            public List<GridRow> Rows = new();
            public string TileSetGuid;
            public string OutputRulesGuid;
            public int ForwardAxis;

            [Serializable]
            public class GridRow
            {
                public List<string> Cells = new();
            }

            public SerializableState()
            {
            }

            public SerializableState(WindowState state)
            {
                GridWidth = state.GridWidth;
                GridHeight = state.GridHeight;
                CellSize = state.CellSize;
                UseXZPlane = state.UseXZPlane;
                PreviewWithMeshes = state.PreviewWithMeshes;
                PreviewOrbit = state.PreviewOrbit;
                PreviewDistance = state.PreviewDistance;
                BrushIndex = state.BrushIndex;
                ForwardAxis = (int)state.ForwardAxis;
                TileSetGuid = state.TileSetGuid;
                OutputRulesGuid = state.OutputRulesGuid;

                if (state.Grid != null)
                {
                    for (int y = 0; y < state.Grid.GetLength(1); y++)
                    {
                        var row = new GridRow();
                        for (int x = 0; x < state.Grid.GetLength(0); x++)
                        {
                            row.Cells.Add(state.Grid[x, y]);
                        }
                        Rows.Add(row);
                    }
                }
            }

            public WindowState ToState()
            {
                var state = new WindowState
                {
                    GridWidth = GridWidth,
                    GridHeight = GridHeight,
                    CellSize = CellSize,
                    UseXZPlane = UseXZPlane,
                    PreviewWithMeshes = PreviewWithMeshes,
                    PreviewOrbit = PreviewOrbit,
                    PreviewDistance = PreviewDistance,
                    BrushIndex = BrushIndex,
                    ForwardAxis = ForwardAxis,
                    TileSetGuid = TileSetGuid,
                    OutputRulesGuid = OutputRulesGuid
                };

                if (GridWidth > 0 && GridHeight > 0 && Rows != null && Rows.Count == GridHeight)
                {
                    var g = new string[GridWidth, GridHeight];
                    for (int y = 0; y < GridHeight; y++)
                    {
                        var row = Rows[y];
                        for (int x = 0; x < GridWidth && x < row.Cells.Count; x++)
                        {
                            g[x, y] = row.Cells[x];
                        }
                    }
                    state.Grid = g;
                }

                return state;
            }
        }
        #endregion

        private void DisposePreviewUtility()
        {
            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }
        }

        private void DestroyMaterials()
        {
            if (solidMaterial != null)
            {
                DestroyImmediate(solidMaterial);
                solidMaterial = null;
            }

            if (wireMaterial != null)
            {
                DestroyImmediate(wireMaterial);
                wireMaterial = null;
            }
        }

        private void CleanupPreviewObjects()
        {
            foreach (var go in previewObjects.Where(o => o != null))
            {
                DestroyImmediate(go);
            }
            previewObjects.Clear();
        }
    }

    internal sealed class DirectionalNeighbors
    {
        public readonly HashSet<string> Left = new();
        public readonly HashSet<string> Right = new();
        public readonly HashSet<string> Forward = new();
        public readonly HashSet<string> Backward = new();

        public void Add(WfcDirection direction, string typeId)
        {
            var set = direction switch
            {
                WfcDirection.Left => Left,
                WfcDirection.Right => Right,
                WfcDirection.Forward => Forward,
                WfcDirection.Backward => Backward,
                _ => Left
            };
            set.Add(typeId);
        }
    }

    internal enum ForwardAxis
    {
        Up,
        Right
    }
}
