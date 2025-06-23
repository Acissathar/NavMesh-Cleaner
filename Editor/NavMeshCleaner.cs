#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.AI.Navigation.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace NavMesh_Cleaner.Editor
{
    public class NavMeshCleaner : MonoBehaviour
    {
        [SerializeField] private List<Vector3> walkablePoints = new List<Vector3>();
        [SerializeField] private float generatedMeshHeight = 1.0f;
        [SerializeField] private float generatedMeshOffset;
        [SerializeField] private int midLayerCount = 3;
        [SerializeField] private Material generatedMeshMaterial;
        [SerializeField] private int generatedMeshNavMeshArea = 1;
        private readonly List<GameObject> _childObjects = new List<GameObject>();

        private void Awake()
        {
            SetMeshVisible(false);
        }

        public void Reset()
        {
            Undo.RecordObject(this, "Reset");

            foreach (var child in _childObjects)
            {
                Undo.DestroyObjectImmediate(child);
            }

            _childObjects.Clear();
        }

        private void SetMeshVisible(bool visible)
        {
            foreach (var child in _childObjects)
                child.SetActive(visible);
        }

        private bool HasMesh()
        {
            return _childObjects.Count != 0;
        }

        private bool MeshVisible()
        {
            if (_childObjects.Count > 0)
            {
                return _childObjects[0].activeSelf;
            }

            return false;
        }

        private void Build(bool bakeWalkableMesh)
        {
            var createdMesh = CreateMesh(bakeWalkableMesh);
            
            if (!TryGetComponent(out NavMeshSurface surface))
            {
                surface = FindFirstObjectByType<NavMeshSurface>();
            }

            Undo.RegisterCreatedObjectUndo(this, "build");

            for (var i = 0; i < createdMesh.Length || i == 0; i++)
            {
                GameObject newObject;
                if (i >= _childObjects.Count)
                {
                    newObject = new GameObject
                    {
                        name = gameObject.name + "_Mesh(DontSave)",
                        tag = "EditorOnly"
                    };

                    newObject.AddComponent<MeshFilter>();

                    var meshRenderer = newObject.AddComponent<MeshRenderer>();
                    meshRenderer.sharedMaterial = generatedMeshMaterial;

                    newObject.transform.parent = transform;
                    newObject.transform.localScale = Vector3.one;
                    newObject.transform.localPosition = Vector3.zero;
                    newObject.transform.localRotation = Quaternion.identity;
                    var navMeshModifier = newObject.AddComponent<NavMeshModifier>();
                    navMeshModifier.overrideArea = true;
                    navMeshModifier.area = generatedMeshNavMeshArea;

                    _childObjects.Add(newObject);
                    Undo.RegisterCreatedObjectUndo(newObject, "");
                }
                else
                {
                    newObject = _childObjects[i].gameObject;
                }

                newObject.hideFlags =
                    i == 0 ? HideFlags.DontSave | HideFlags.HideInHierarchy : _childObjects[0].gameObject.hideFlags;

                var meshFilter = _childObjects[i].GetComponent<MeshFilter>();
                Undo.RecordObject(meshFilter, "MeshUpdate");
                meshFilter.sharedMesh = createdMesh.Length == 0 ? null : createdMesh[i];

                if(surface) 
                {
                    if (surface.useGeometry == NavMeshCollectGeometry.PhysicsColliders)
                    {
                        if (!TryGetComponent(out MeshCollider meshCollider))
                        {
                            meshCollider = newObject.AddComponent<MeshCollider>();
                        }

                        meshCollider.sharedMesh = meshFilter.sharedMesh;
                    }
                    
                    newObject.layer = Mathf.RoundToInt(Mathf.Log(surface.layerMask, 2));  
                }
            }

            while (_childObjects.Count > createdMesh.Length)
            {
                Undo.DestroyObjectImmediate(_childObjects[^1]);
                _childObjects.RemoveAt(_childObjects.Count - 1);
            }
        }

        private static int Find(Vector3[] vtx, int left, int right, Vector3 v, float key)
        {
            var center = (left + right) / 2;

            if (center == left)
            {
                for (var i = left; i < vtx.Length && vtx[i].x <= key + 0.002f; i++)
                {
                    if (Vector3.Magnitude(vtx[i] - v) <= 0.01f)
                        return i;
                }

                return -1;
            }

            if (key <= vtx[center].x)
            {
                return Find(vtx, left, center, v, key);
            }

            return Find(vtx, center, right, v, key);
        }

        private static bool Find(Edge[] edge, int left, int right, int i1, int i2)
        {
            var center = (left + right) / 2;

            if (center == left)
            {
                for (var i = left; i < edge.Length && edge[i].I1 <= i1; i++)
                {
                    if (edge[i].I1 == i1 && edge[i].I2 == i2)
                        return true;
                }

                return false;
            }

            if (i1 <= edge[center].I1)
            {
                return Find(edge, left, center, i1, i2);
            }

            return Find(edge, center, right, i1, i2);
        }

        private Mesh[] CreateMesh(bool bakeWalkableMesh)
        {
            var triangulatedNavMesh = NavMesh.CalculateTriangulation();

            var navVertices = triangulatedNavMesh.vertices;
            var vertices = new List<Vector3>();
            vertices.AddRange(navVertices);
            vertices.Sort((v1, v2) =>
                Mathf.Approximately(v1.x, v2.x)
                    ? Mathf.Approximately(v1.z, v2.z) ? 0 : v1.z < v2.z ? -1 : 1
                    : v1.x < v2.x
                        ? -1
                        : 1);

            var v = vertices.ToArray();

            var table = new int[triangulatedNavMesh.vertices.Length];

            for (var i = 0; i < table.Length; i++)
            {
                table[i] = Find(v, 0, vertices.Count, navVertices[i], navVertices[i].x - 0.001f);
                if (i % 100 == 0)
                {
                    EditorUtility.DisplayProgressBar($"Export Nav-Mesh (Phase #1/3) {i}/{table.Length}", "Weld Vertex",
                        Mathf.InverseLerp(0, table.Length, i));
                }
            }

            var navTriangles = triangulatedNavMesh.indices;

            var tri = new List<Tri>();
            for (var i = 0; i < navTriangles.Length; i += 3)
            {
                tri.Add(new Tri(table[navTriangles[i + 0]], table[navTriangles[i + 1]], table[navTriangles[i + 2]]));
            }

            tri.Sort((t1, t2) => t1.Min == t2.Min ? 0 : t1.Min < t2.Min ? -1 : 1);

            var boundMin = new int[(tri.Count + 127) / 128];
            var boundMan = new int[boundMin.Length];

            for (int i = 0, c = 0; i < tri.Count; i += 128, c++)
            {
                var min = tri[i].Min;
                var max = tri[i].Max;
                for (var j = 1; j < 128 && i + j < tri.Count; j++)
                {
                    min = Mathf.Min(tri[i + j].Min, min);
                    max = Mathf.Max(tri[i + j].Max, max);
                }

                boundMin[c] = min;
                boundMan[c] = max;
            }

            var triangles = new int[navTriangles.Length];
            for (var i = 0; i < triangles.Length; i += 3)
            {
                triangles[i + 0] = tri[i / 3].I1;
                triangles[i + 1] = tri[i / 3].I2;
                triangles[i + 2] = tri[i / 3].I3;
            }

            var groundIndex = new List<int>();
            var groupCount = new List<int>();

            var group = new int[triangles.Length / 3];

            for (var i = 0; i < triangles.Length; i += 3)
            {
                var groupID = -1;
                var max = Mathf.Max(triangles[i], triangles[i + 1], triangles[i + 2]);
                var min = Mathf.Min(triangles[i], triangles[i + 1], triangles[i + 2]);

                for (int b = 0, c = 0; b < i; b += 3 * 128, c++)
                {
                    if (boundMin[c] > max || boundMan[c] < min)
                    {
                        continue;
                    }

                    for (var j = b; j < i && j < b + 3 * 128; j += 3)
                    {
                        if (tri[j / 3].Min > max)
                        {
                            break;
                        }

                        if (tri[j / 3].Max < min)
                        {
                            continue;
                        }

                        if (groundIndex[group[j / 3]] == groupID)
                        {
                            continue;
                        }

                        for (var k = 0; k < 3; k++)
                        {
                            var vi = triangles[j + k];
                            if (triangles[i] == vi || triangles[i + 1] == vi || triangles[i + 2] == vi)
                            {
                                if (groupID == -1)
                                {
                                    groupID = groundIndex[group[j / 3]];
                                    group[i / 3] = groupID;
                                }
                                else
                                {
                                    var currentGround = groundIndex[group[j / 3]];
                                    for (var l = 0; l < groundIndex.Count; l++)
                                    {
                                        if (groundIndex[l] == currentGround)
                                        {
                                            groundIndex[l] = groupID;
                                        }
                                    }
                                }

                                break;
                            }
                        }
                    }
                }

                if (groupID == -1)
                {
                    groupID = groundIndex.Count;
                    group[i / 3] = groupID;
                    groundIndex.Add(groupID);
                    groupCount.Add(0);
                }

                if (i / 3 % 100 == 0)
                {
                    EditorUtility.DisplayProgressBar("Collect (Phase #2/3)", "Classification Group",
                        Mathf.InverseLerp(0, triangles.Length, i));
                }
            }

            for (var i = 0; i < triangles.Length; i += 3)
            {
                group[i / 3] = groundIndex[group[i / 3]];
                groupCount[group[i / 3]]++;
            }

            var result = new List<Mesh>();

            var vtx = new List<Vector3>();
            var indices = new List<int>();

            var newTable = new int[vertices.Count];
            for (var i = 0; i < newTable.Length; i++)
            {
                newTable[i] = -1;
            }

            var walkPoint = walkablePoints.ToArray();

            for (var g = 0; g < groupCount.Count; g++)
            {
                if (groupCount[g] == 0)
                {
                    continue;
                }

                var isolateVertex = new List<Vector3>();
                var isolateIndex = new List<int>();

                for (var i = 0; i < triangles.Length; i += 3)
                {
                    if (group[i / 3] == g)
                    {
                        for (var j = 0; j < 3; j++)
                        {
                            var idx = triangles[i + j];
                            if (newTable[idx] == -1)
                            {
                                newTable[idx] = isolateVertex.Count;
                                isolateVertex.Add(
                                    transform.InverseTransformPoint(vertices[idx] + Vector3.up * generatedMeshOffset));
                            }
                        }

                        isolateIndex.Add(newTable[triangles[i + 0]]);
                        isolateIndex.Add(newTable[triangles[i + 1]]);
                        isolateIndex.Add(newTable[triangles[i + 2]]);
                    }
                }

                if (bakeWalkableMesh)
                {
                    if (Contains(isolateVertex.ToArray(), isolateIndex.ToArray(), walkPoint))
                    {
                        var h = -transform.InverseTransformVector(Vector3.up * generatedMeshHeight);
                        var vertexOffset = vtx.Count;
                        var layer = 2;
                        foreach (var t in isolateVertex)
                        {
                            for (var j = 0; j < layer; j++)
                            {
                                vtx.Add(t + (j == 0 ? h : Vector3.zero));
                            }
                        }

                        for (var i = 0; i < isolateIndex.Count; i += 3)
                        {
                            for (var j = 0; j < layer; j++)
                            {
                                if (j == 0)
                                {
                                    indices.AddRange(new[]
                                    {
                                        vertexOffset + isolateIndex[i] * layer + j, vertexOffset + isolateIndex[i + 2] * layer + j,
                                        vertexOffset + isolateIndex[i + 1] * layer + j
                                    });
                                }
                                else
                                {
                                    indices.AddRange(new[]
                                    {
                                        vertexOffset + isolateIndex[i] * layer + j, vertexOffset + isolateIndex[i + 1] * layer + j,
                                        vertexOffset + isolateIndex[i + 2] * layer + j
                                    });
                                }
                            }
                        }

                        if (generatedMeshHeight > 0)
                        {
                            var edge = new List<Edge>();
                            for (var i = 0; i < isolateIndex.Count; i += 3)
                            {
                                edge.Add(new Edge(isolateIndex[i + 0], isolateIndex[i + 1]));
                                edge.Add(new Edge(isolateIndex[i + 1], isolateIndex[i + 2]));
                                edge.Add(new Edge(isolateIndex[i + 2], isolateIndex[i + 0]));
                            }

                            edge.Sort((e1, e2) => e1.I1 == e2.I1 ? 0 : e1.I1 < e2.I1 ? -1 : 1);
                            var e = edge.ToArray();

                            for (var i = 0; i < isolateIndex.Count; i += 3)
                            {
                                for (int i1 = 2, i2 = 0; i2 < 3; i1 = i2++)
                                {
                                    var v1 = isolateIndex[i + i1];
                                    var v2 = isolateIndex[i + i2];

                                    if (!Find(e, 0, edge.Count, v2, v1))
                                    {
                                        indices.AddRange(new[]
                                        {
                                            vtx.Count, vtx.Count + 3, vtx.Count + 1, vtx.Count, vtx.Count + 2, vtx.Count + 3
                                        });
                                        vtx.AddRange(new[]
                                        {
                                            isolateVertex[v1], isolateVertex[v2], isolateVertex[v1] + h, isolateVertex[v2] + h
                                        });
                                    }
                                }

                                if (i % 600 == 0)
                                {
                                    EditorUtility.DisplayProgressBar("Collect (Phase #3/3)", "Create Mesh",
                                        Mathf.InverseLerp(0, groupCount.Count * 100,
                                            g * 100 + i * 100 / (i - isolateIndex.Count)));
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (Contains(isolateVertex.ToArray(), isolateIndex.ToArray(), walkPoint))
                    {
                        continue;
                    }

                    var maxVertex = 32768;

                    if (vtx.Count > maxVertex || vtx.Count + isolateVertex.Count * (2 + midLayerCount) >= 65536)
                    {
                        result.Add(CreateMesh(vtx.ToArray(), indices.ToArray()));
                        vtx.Clear();
                        indices.Clear();
                    }

                    var h = transform.InverseTransformVector(Vector3.up * generatedMeshHeight);
                    var vertexOffset = vtx.Count;
                    var layer = 2 + midLayerCount;
                    foreach (var t in isolateVertex)
                    {
                        for (var j = 0; j < layer; j++)
                            vtx.Add(t + h * ((float)j / (layer - 1)));
                    }

                    for (var i = 0; i < isolateIndex.Count; i += 3)
                    {
                        for (var j = 0; j < layer; j++)
                        {
                            if (j == 0)
                            {
                                indices.AddRange(new[]
                                {
                                    vertexOffset + isolateIndex[i] * layer + j, vertexOffset + isolateIndex[i + 2] * layer + j,
                                    vertexOffset + isolateIndex[i + 1] * layer + j
                                });
                            }
                            else
                            {
                                indices.AddRange(new[]
                                {
                                    vertexOffset + isolateIndex[i] * layer + j, vertexOffset + isolateIndex[i + 1] * layer + j,
                                    vertexOffset + isolateIndex[i + 2] * layer + j
                                });
                            }
                        }
                    }

                    if (generatedMeshHeight > 0)
                    {
                        var edge = new List<Edge>();
                        for (var i = 0; i < isolateIndex.Count; i += 3)
                        {
                            edge.Add(new Edge(isolateIndex[i + 0], isolateIndex[i + 1]));
                            edge.Add(new Edge(isolateIndex[i + 1], isolateIndex[i + 2]));
                            edge.Add(new Edge(isolateIndex[i + 2], isolateIndex[i + 0]));
                        }

                        edge.Sort((e1, e2) => e1.I1 == e2.I1 ? 0 : e1.I1 < e2.I1 ? -1 : 1);
                        var e = edge.ToArray();

                        for (var i = 0; i < isolateIndex.Count; i += 3)
                        {
                            for (int i1 = 2, i2 = 0; i2 < 3; i1 = i2++)
                            {
                                var v1 = isolateIndex[i + i1];
                                var v2 = isolateIndex[i + i2];

                                if (!Find(e, 0, edge.Count, v2, v1))
                                {
                                    if (vtx.Count + 4 >= 65536)
                                    {
                                        result.Add(CreateMesh(vtx.ToArray(), indices.ToArray()));
                                        vtx.Clear();
                                        indices.Clear();
                                    }

                                    indices.AddRange(new[]
                                    {
                                        vtx.Count, vtx.Count + 1, vtx.Count + 3, vtx.Count, vtx.Count + 3, vtx.Count + 2
                                    });
                                    vtx.AddRange(new[]
                                    {
                                        isolateVertex[v1], isolateVertex[v2], isolateVertex[v1] + h, isolateVertex[v2] + h
                                    });
                                }
                            }

                            if (i % 600 == 0)
                            {
                                EditorUtility.DisplayProgressBar("Collect (Phase #3/3)", "Create Mesh",
                                    Mathf.InverseLerp(0, groupCount.Count * 100,
                                        g * 100 + i * 100.0f / (i - isolateIndex.Count)));
                            }
                        }
                    }
                }

                EditorUtility.DisplayProgressBar("Collect (Phase #3/3)", "Create Mesh",
                    Mathf.InverseLerp(0, groupCount.Count, g));
            }

            if (vtx.Count > 0)
            {
                result.Add(CreateMesh(vtx.ToArray(), indices.ToArray()));
            }

            EditorUtility.ClearProgressBar();
            return result.ToArray();
        }

        private static Mesh CreateMesh(Vector3[] vtx, int[] indices)
        {
            var createdMesh = new Mesh
            {
                hideFlags = HideFlags.DontSave,
                vertices = vtx
            };
            createdMesh.SetIndices(indices, MeshTopology.Triangles, 0);
            createdMesh.RecalculateNormals();
            createdMesh.RecalculateBounds();
            return createdMesh;
        }

        private static bool Contains(Vector3[] vtx, int[] indices, Vector3[] points)
        {
            foreach (var p in points)
            {
                for (var i = 0; i < indices.Length; i += 3)
                {
                    if (indices[i] == indices[i + 1] || indices[i] == indices[i + 2] ||
                        indices[i + 1] == indices[i + 2])
                    {
                        continue;
                    }

                    if (PointInTriangle(vtx[indices[i]], vtx[indices[i + 2]], vtx[indices[i + 1]], p))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool PointInTriangle(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 p)
        {
            var up = Vector3.Cross(v3 - v1, v2 - v1);

            if (Vector3.Dot(Vector3.Cross(p - v1, v2 - v1), up) > 0 &&
                Vector3.Dot(Vector3.Cross(p - v2, v3 - v2), up) > 0 &&
                Vector3.Dot(Vector3.Cross(p - v3, v1 - v3), up) > 0)
            {
                return true;
            }

            return false;
        }

        private class Tri
        {
            public readonly int I1;
            public readonly int I2;
            public readonly int I3;
            public readonly int Max;
            public readonly int Min;

            public Tri(int i1, int i2, int i3)
            {
                I1 = i1;
                I2 = i2;
                I3 = i3;
                Min = Mathf.Min(i1, i2, i3);
                Max = Mathf.Max(i1, i2, i3);
            }
        }

        private class Edge
        {
            public readonly int I1;
            public readonly int I2;

            public Edge(int i1, int i2)
            {
                I1 = i1;
                I2 = i2;
            }
        }

        [CustomEditor(typeof(NavMeshCleaner))]
        public class NavMeshCleanerEditor : UnityEditor.Editor
        {
            private const float Epsilon = 0.000001f;

            private bool _bakeWalkableMesh;
            private SerializedProperty _generatedMeshHeightProp;
            private SerializedProperty _generatedMeshMaterialProp;
            private SerializedProperty _generatedMeshNavMeshAreaProp;
            private SerializedProperty _generatedMeshOffsetProp;
            private SerializedProperty _midLayerCountProp;

            private int _overPoint = -1;
            private NavMeshCleaner _target;

            private SerializedProperty _walkablePointsProp;

            private void OnEnable()
            {
                _target = (NavMeshCleaner)target;

                _walkablePointsProp = serializedObject.FindProperty("walkablePoints");
                _generatedMeshHeightProp = serializedObject.FindProperty("generatedMeshHeight");
                _generatedMeshOffsetProp = serializedObject.FindProperty("generatedMeshOffset");
                _midLayerCountProp = serializedObject.FindProperty("midLayerCount");
                _generatedMeshMaterialProp = serializedObject.FindProperty("generatedMeshMaterial");
                _generatedMeshNavMeshAreaProp = serializedObject.FindProperty("generatedMeshNavMeshArea");

                Undo.undoRedoPerformed += OnUndoOrRedo;
            }

            private void OnDisable()
            {
                Undo.undoRedoPerformed -= OnUndoOrRedo;
            }

            private void OnSceneGUI()
            {
                var sceneView = SceneView.currentDrawingSceneView;

                var guiEvent = Event.current;

                if (guiEvent.type == EventType.Repaint)
                {
                    for (var i = 0; i < _target.walkablePoints.Count; i++)
                    {
                        var p = _target.transform.TransformPoint(_target.walkablePoints[i]);
                        var unitSize = WorldSize(1.0f, sceneView.camera, p);

                        Handles.color = Color.black;
                        DrawDisc(p, Vector3.up, unitSize * 15);

                        Handles.color = i == _overPoint ? Color.red : Color.green;
                        Handles.DrawSolidDisc(p, Vector3.up, unitSize * 10);
                        Handles.DrawLine(p, p + Vector3.up * (unitSize * 200.0f));
                    }
                }

                if (guiEvent.type == EventType.Layout && guiEvent.control)
                {
                    HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                }

                if (guiEvent.control)
                {
                    EditorGUIUtility.AddCursorRect(new Rect(0, 0, Screen.width, Screen.height),
                        _overPoint == -1 ? MouseCursor.ArrowPlus : MouseCursor.ArrowMinus);
                }

                if ((guiEvent.type == EventType.MouseDown || guiEvent.type == EventType.MouseDrag ||
                     guiEvent.type == EventType.MouseMove ||
                     guiEvent.type == EventType.MouseUp) && guiEvent.button == 0)
                {
                    MouseEvent(guiEvent.type, guiEvent.mousePosition, guiEvent.modifiers == EventModifiers.Control);
                }
            }

            private void OnUndoOrRedo()
            {
                Repaint();
            }

            public override void OnInspectorGUI()
            {
                serializedObject.Update();

                EditorGUILayout.HelpBox(
                    _overPoint != -1
                        ? "Press Control and click to remove the point."
                        : "Press Control and click to add a walkable point.",
                    _target.walkablePoints.Count == 0 ? MessageType.Warning : MessageType.Info);
                EditorGUILayout.PropertyField(_walkablePointsProp, new GUIContent("Walkable Points"));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Generated Mesh Settings");
                _bakeWalkableMesh = EditorGUILayout.Toggle(new GUIContent("Bake Walkable Mesh",
                        "When checked, instead of generating a mesh to block inaccessible areas, this will instead generate a mesh to match the walkable areas. \n" +
                        "When using this option, it is recommended to not use the total bake and instead bake separately to specify this new mesh only."),
                    _bakeWalkableMesh);
                EditorGUILayout.PropertyField(_generatedMeshHeightProp, new GUIContent("Generated Mesh Height"));
                EditorGUILayout.PropertyField(_generatedMeshOffsetProp, new GUIContent("Generated Mesh Offset"));
                EditorGUILayout.PropertyField(_generatedMeshMaterialProp, new GUIContent("Generated Mesh Material"));
                EditorGUILayout.PropertyField(_midLayerCountProp, new GUIContent("Mid Layer Count"));
                EditorGUILayout.HelpBox(
                    _bakeWalkableMesh
                        ? "This should be set to a walkable area type."
                        : "This should be set to a non-walkable area type.", MessageType.Info);
                NavMeshComponentsGUIUtility.AreaPopup("Mesh Area Type", _generatedMeshNavMeshAreaProp);

                var navMeshCleaner = (NavMeshCleaner)target;

                if (navMeshCleaner._childObjects.Count > 0)
                {
                    EditorGUI.BeginChangeCheck();
                    var hideInHierarchy = EditorGUILayout.Toggle("Hide Temp Mesh Object In Hierarchy",
                        (navMeshCleaner._childObjects[0].gameObject.hideFlags & HideFlags.HideInHierarchy) != 0);
                    if (EditorGUI.EndChangeCheck())
                    {
                        foreach (var child in navMeshCleaner._childObjects)
                        {
                            child.gameObject.hideFlags = hideInHierarchy
                                ? child.gameObject.hideFlags | HideFlags.HideInHierarchy
                                : child.gameObject.hideFlags & ~HideFlags.HideInHierarchy;
                        }

                        try
                        {
                            EditorApplication.RepaintHierarchyWindow();
                            EditorApplication.DirtyHierarchyWindowSorting();
                        }
                        catch (UnityException exception)
                        {
                            Debug.LogError(exception);
                        }
                    }
                }

                if (_target.walkablePoints.Count > 0)
                {
                    EditorGUILayout.HelpBox("Manual mesh generation and options.", MessageType.Info);
                    if (GUILayout.Button(navMeshCleaner.HasMesh() ? "Recalculate Mesh" : "Calculate Mesh", GUILayout.Height(20.0f)))
                    {
                        navMeshCleaner.Build(_bakeWalkableMesh);
                        navMeshCleaner.SetMeshVisible(true);
                        SceneView.RepaintAll();
                    }

                    if (navMeshCleaner.HasMesh() &&
                        GUILayout.Button(navMeshCleaner.MeshVisible() ? "Hide Mesh" : "Show Mesh", GUILayout.Height(20.0f)))
                    {
                        var enabled = !navMeshCleaner.MeshVisible();
                        navMeshCleaner.SetMeshVisible(enabled);
                        SceneView.RepaintAll();
                    }

                    if (navMeshCleaner.HasMesh() && GUILayout.Button("Remove Mesh", GUILayout.Height(20.0f)))
                    {
                        navMeshCleaner.Reset();
                        SceneView.RepaintAll();
                    }

                    if (navMeshCleaner.HasMesh() && GUILayout.Button("Reset WalkablePoints", GUILayout.Height(20.0f)))
                    {
                        Undo.RecordObject(target, "reset");
                        _target.walkablePoints.Clear();
                        SceneView.RepaintAll();
                    }

                    serializedObject.ApplyModifiedProperties();
                }
                else
                {
                    EditorGUILayout.HelpBox("At least one walkable point must be specified to generate a mesh.", MessageType.Error);
                }
            }

            private void DrawDisc(Vector3 p, Vector3 n, float radius)
            {
                var v = new Vector3[20];
                var tm = Matrix4x4.TRS(p, Quaternion.LookRotation(n), Vector3.one * radius);
                for (var i = 0; i < 20; i++)
                {
                    v[i] = tm.MultiplyPoint3x4(new Vector3(Mathf.Cos(Mathf.PI * 2 * i / (20 - 1)),
                        Mathf.Sin(Mathf.PI * 2 * i / (20 - 1)), 0));
                }

                Handles.DrawAAPolyLine(v);
            }

            private void MouseEvent(EventType type, Vector2 mousePosition, bool controlDown)
            {
                var sceneView = SceneView.currentDrawingSceneView;

                var mouseRay = HandleUtility.GUIPointToWorldRay(mousePosition);

                if (type == EventType.MouseMove)
                {
                    var pointIndex = -1;

                    for (var i = 0; i < _target.walkablePoints.Count; i++)
                    {
                        var p = _target.transform.TransformPoint(_target.walkablePoints[i]);
                        var size = WorldSize(10.0f, sceneView.camera, p) * 1.5f;
                        if (DistanceRayVsPoint(mouseRay, p) < size)
                        {
                            pointIndex = i;
                            break;
                        }
                    }

                    if (pointIndex != _overPoint)
                    {
                        _overPoint = pointIndex;
                        HandleUtility.Repaint();
                    }
                }

                if (type == EventType.MouseDown && controlDown)
                {
                    if (_overPoint != -1)
                    {
                        Undo.RecordObject(_target, "Remove Point");
                        _target.walkablePoints.RemoveAt(_overPoint);
                        _overPoint = -1;
                    }
                    else
                    {
                        var mint = 1000.0f;

                        if (Physics.Raycast(mouseRay, out var hit, mint))
                        {
                            Undo.RecordObject(_target, "Add Point");
                            _target.walkablePoints.Add(_target.transform.InverseTransformPoint(hit.point));
                        }
                        else
                        {
                            var triangulatedNavMesh = NavMesh.CalculateTriangulation();

                            var navVertices = triangulatedNavMesh.vertices;
                            var indices = triangulatedNavMesh.indices;

                            var outNormal = Vector3.up;
                            for (var i = 0; i < indices.Length; i += 3)
                            {
                                mint = IntersectTest(mouseRay, navVertices[indices[i]], navVertices[indices[i + 1]],
                                    navVertices[indices[i + 2]], mint,
                                    ref outNormal);
                            }

                            if (mint < 1000.0f)
                            {
                                Undo.RecordObject(_target, "Add Point");
                                var point = mouseRay.origin + mouseRay.direction * mint;
                                _target.walkablePoints.Add(_target.transform.InverseTransformPoint(point));
                            }
                        }
                    }

                    HandleUtility.Repaint();
                }
            }

            // https://en.wikipedia.org/wiki/Möller–Trumbore_intersection_algorithm
            private static float IntersectTest(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2, float mint,
                ref Vector3 outNormal)
            {
                // edges from v1 & v2 to v0.     
                var e1 = v1 - v0;
                var e2 = v2 - v0;

                var h = Vector3.Cross(ray.direction, e2);
                var a = Vector3.Dot(e1, h);
                if (a is > -Epsilon and < Epsilon)
                {
                    return mint;
                }

                var f = 1.0f / a;
                var s = ray.origin - v0;
                var u = f * Vector3.Dot(s, h);
                if (u < 0.0f || u > 1.0f)
                {
                    return mint;
                }

                var q = Vector3.Cross(s, e1);
                var v = f * Vector3.Dot(ray.direction, q);
                if (v < 0.0f || u + v > 1.0f)
                {
                    return mint;
                }

                var t = f * Vector3.Dot(e2, q);
                if (t > Epsilon && t < mint)
                {
                    outNormal = Vector3.Normalize(Vector3.Cross(e1.normalized, e2.normalized));
                    return t;
                }

                return mint;
            }

            private static float WorldSize(float screenSize, Camera camera, Vector3 p)
            {
                if (!camera.orthographic)
                {
                    var localPos = camera.transform.InverseTransformPoint(p);
                    var height = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * localPos.z;
                    return height * screenSize / camera.pixelHeight;
                }

                return camera.orthographicSize * screenSize / camera.pixelHeight;
            }

            private static float DistanceRayVsPoint(Ray mouseRay, Vector3 pos)
            {
                var v = pos - mouseRay.origin;
                return Mathf.Sqrt(Vector3.Dot(v, v) -
                                  Vector3.Dot(mouseRay.direction, v) * Vector3.Dot(mouseRay.direction, v));
            }
        }
    }
}
#endif