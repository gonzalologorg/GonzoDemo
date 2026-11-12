using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

public class DungeonWallMaker : EditorWindow
{
    private float wallHeight = 3f;
    private Color wallColor = Color.gray;
    private bool createRoof = true;
    private Color roofColor = Color.gray;

    private void OnEnable() => Selection.selectionChanged += Repaint;
    private void OnDisable() => Selection.selectionChanged -= Repaint;

    [MenuItem("Tools/Dungeon/Wall Maker")]
    private static void Open() => GetWindow<DungeonWallMaker>("Wall Maker");

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Selecciona los bloques ProBuilder. Los muros se generan desde sus caras laterales " +
            "y el techo desde sus caras superiores; no se usa un bounding box global.", MessageType.Info);

        wallHeight = EditorGUILayout.FloatField("Altura del muro", wallHeight);
        wallColor = EditorGUILayout.ColorField("Color del muro", wallColor);
        createRoof = EditorGUILayout.Toggle("Crear techo", createRoof);
        using (new EditorGUI.DisabledScope(!createRoof))
            roofColor = EditorGUILayout.ColorField("Color del techo", roofColor);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Objetos seleccionados", Selection.gameObjects.Length.ToString());
        using (new EditorGUI.DisabledScope(Selection.gameObjects.Length == 0 || wallHeight <= 0f))
        {
            if (GUILayout.Button("Crear walls"))
                CreateWalls();
        }
    }

    private void CreateWalls()
    {
        List<ProBuilderMesh> sources = GetSelectedProBuilderMeshes();
        if (sources.Count == 0)
        {
            EditorUtility.DisplayDialog("Wall Maker", "Selecciona al menos un objeto con ProBuilderMesh.", "OK");
            return;
        }

        var wallBuilder = new ProBuilderBuilder();
        var roofBuilder = new ProBuilderBuilder();
        var sideFaces = new Dictionary<string, List<SideCandidate>>();

        foreach (ProBuilderMesh source in sources)
        {
            AddExposedSideFaces(source, sideFaces);
            if (createRoof)
                AddRoofFaces(source, roofBuilder, wallHeight);
        }

        foreach (List<SideCandidate> candidates in sideFaces.Values)
        {
            bool sharedInteriorSide = candidates.Count > 1 &&
                candidates.Any(a => candidates.Any(b => Vector3.Dot(a.normal, b.normal) < -0.9f));
            if (sharedInteriorSide)
                continue;

            SideCandidate side = candidates[0];
            wallBuilder.AddQuad(side.a, side.b,
                side.b + Vector3.up * wallHeight,
                side.a + Vector3.up * wallHeight,
                -side.normal);
        }

        if (wallBuilder.FaceCount == 0)
        {
            EditorUtility.DisplayDialog("Wall Maker", "No se encontraron caras laterales verticales en la selección.", "OK");
            return;
        }

        GameObject root = new GameObject("walls");
        Undo.RegisterCreatedObjectUndo(root, "Create Dungeon Walls");
        CreateSurface(root.transform, "Walls", wallBuilder, wallColor);
        if (createRoof && roofBuilder.FaceCount > 0)
            CreateSurface(root.transform, "Roof", roofBuilder, roofColor);

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        AssetDatabase.SaveAssets();
    }

    private static List<ProBuilderMesh> GetSelectedProBuilderMeshes()
    {
        var result = new HashSet<ProBuilderMesh>();
        foreach (GameObject selected in Selection.gameObjects)
        {
            if (selected == null || !selected.scene.IsValid())
                continue;

            ProBuilderMesh mesh = selected.GetComponent<ProBuilderMesh>();
            if (mesh != null)
                result.Add(mesh);
        }

        return result.ToList();
    }

    private static void AddExposedSideFaces(ProBuilderMesh source, Dictionary<string, List<SideCandidate>> sides)
    {
        IList<Vector3> positions = source.positions;
        foreach (Face face in source.faces)
        {
            if (face.indexes.Count < 3)
                continue;

            Vector3 a = source.transform.TransformPoint(positions[face.indexes[0]]);
            Vector3 b = source.transform.TransformPoint(positions[face.indexes[1]]);
            Vector3 c = source.transform.TransformPoint(positions[face.indexes[2]]);
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            if (Mathf.Abs(normal.y) > 0.1f)
                continue;

            List<Vector3> corners = face.distinctIndexes
                .Select(index => source.transform.TransformPoint(positions[index]))
                .Distinct(new PositionComparer())
                .ToList();
            if (corners.Count < 4)
                continue;

            float baseY = corners.Max(point => point.y);
            List<Vector3> edge = corners
                .Where(point => Mathf.Abs(point.y - baseY) < 0.001f)
                .ToList();
            if (edge.Count != 2)
                continue;

            Vector3 p0 = new Vector3(edge[0].x, baseY, edge[0].z);
            Vector3 p1 = new Vector3(edge[1].x, baseY, edge[1].z);
            string key = MakeEdgeKey(p0, p1);
            if (!sides.TryGetValue(key, out List<SideCandidate> candidates))
            {
                candidates = new List<SideCandidate>();
                sides.Add(key, candidates);
            }
            candidates.Add(new SideCandidate(p0, p1, normal));
        }
    }

    private static void AddRoofFaces(ProBuilderMesh source, ProBuilderBuilder builder, float height)
    {
        IList<Vector3> positions = source.positions;
        foreach (Face face in source.faces)
        {
            if (face.indexes.Count < 3)
                continue;

            Vector3 a = source.transform.TransformPoint(positions[face.indexes[0]]);
            Vector3 b = source.transform.TransformPoint(positions[face.indexes[1]]);
            Vector3 c = source.transform.TransformPoint(positions[face.indexes[2]]);
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(normal, Vector3.up) < 0.9f)
                continue;

            List<Vector3> polygon = face.distinctIndexes
                .Select(index => source.transform.TransformPoint(positions[index]))
                .Distinct(new PositionComparer())
                .ToList();
            if (polygon.Count < 3)
                continue;

            Vector3 center = polygon.Aggregate(Vector3.zero, (sum, point) => sum + point) / polygon.Count;
            polygon = polygon.OrderBy(point => Mathf.Atan2(point.z - center.z, point.x - center.x)).ToList();
            for (int i = 0; i < polygon.Count; i++)
                polygon[i] += Vector3.up * height;
            builder.AddPolygon(polygon, Vector3.down);
        }
    }

    private static string MakeEdgeKey(Vector3 a, Vector3 b)
    {
        string first = PointKey(a);
        string second = PointKey(b);
        return string.CompareOrdinal(first, second) <= 0 ? first + "|" + second : second + "|" + first;
    }

    private static string PointKey(Vector3 point) =>
        point.x.ToString("F3") + "," + point.y.ToString("F3") + "," + point.z.ToString("F3");

    private static void CreateSurface(Transform parent, string name, ProBuilderBuilder builder, Color color)
    {
        ProBuilderMesh mesh = ProBuilderMesh.Create(builder.Vertices, builder.Faces);
        mesh.gameObject.name = name;
        Undo.RegisterCreatedObjectUndo(mesh.gameObject, "Create Dungeon " + name);
        mesh.transform.SetParent(parent, false);
        mesh.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial(name + " Material", color);
        mesh.ToMesh();
        mesh.Refresh();
    }

    private static Material CreateMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader) { name = name, color = color };
        string folder = EnsureGeneratedFolder();
        string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name.Replace(" ", "") + ".mat");
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static string EnsureGeneratedFolder()
    {
        const string parent = "Assets/Demo";
        const string folder = parent + "/GeneratedDungeonWalls";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(parent, "GeneratedDungeonWalls");
        return folder;
    }

    private sealed class PositionComparer : IEqualityComparer<Vector3>
    {
        public bool Equals(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 0.000001f;
        public int GetHashCode(Vector3 point) => 0;
    }

    private readonly struct SideCandidate
    {
        public readonly Vector3 a;
        public readonly Vector3 b;
        public readonly Vector3 normal;

        public SideCandidate(Vector3 a, Vector3 b, Vector3 normal)
        {
            this.a = a;
            this.b = b;
            this.normal = normal;
        }
    }

    private sealed class ProBuilderBuilder
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Face> Faces = new List<Face>();
        public int FaceCount => Faces.Count;

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            AddPolygon(new List<Vector3> { a, b, c, d }, normal);
        }

        public void AddPolygon(List<Vector3> polygon, Vector3 normal)
        {
            int start = Vertices.Count;
            Vector3 winding = Vector3.Cross(polygon[1] - polygon[0], polygon[2] - polygon[0]);
            if (Vector3.Dot(winding, normal) < 0f)
                polygon.Reverse();

            Vertices.AddRange(polygon);
            var indices = new List<int>();
            for (int i = 1; i < polygon.Count - 1; i++)
            {
                indices.Add(start);
                indices.Add(start + i);
                indices.Add(start + i + 1);
            }
            Faces.Add(new Face(indices));
        }
    }
}
