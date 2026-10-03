using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace AirportSim.EditorTools
{
    /// <summary>抽象空港シーンを生成する。レイアウトを変えたいときはこのスクリプトを直して再実行する。</summary>
    public static class AirportSceneBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/AirportSim.unity";
        const string NavMeshPath = "Assets/_Project/Scenes/AirportNavMesh.asset";
        const string ConfigPath = "Assets/_Project/Settings/SimConfig_Default.asset";

        [MenuItem("AirportSim/Build Airport Scene")]
        public static void Build()
        {
            DestroyIfExists("Airport");
            DestroyIfExists("SimulationManager");

            GameObject root = new GameObject("Airport");

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform);
            floor.transform.position = new Vector3(35f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(90f, 1f, 40f);
            floor.GetComponent<Renderer>().sharedMaterial = Mat("Floor", new Color(0.82f, 0.82f, 0.85f));

            //            名前            種別                 中心                         広さ(x,z)         窓口数 処理秒  色
            CreateZone(root, "Entrance",   ZoneType.Entrance, new Vector3(0f, 0f, 0f),     new Vector2(8f, 16f),  0,    0f,   new Color(0.6f, 0.8f, 0.6f));
            CreateZone(root, "CheckIn_A",  ZoneType.CheckIn,  new Vector3(18f, 0f, -6f),   new Vector2(8f, 8f),   2,    8f,   new Color(0.6f, 0.7f, 0.9f));
            CreateZone(root, "CheckIn_B",  ZoneType.CheckIn,  new Vector3(18f, 0f, 6f),    new Vector2(8f, 8f),   2,    8f,   new Color(0.6f, 0.7f, 0.9f));
            CreateZone(root, "Security_A", ZoneType.Security, new Vector3(38f, 0f, -6f),   new Vector2(8f, 8f),   2,    10f,  new Color(0.9f, 0.7f, 0.5f));
            CreateZone(root, "Security_B", ZoneType.Security, new Vector3(38f, 0f, 6f),    new Vector2(8f, 8f),   2,    10f,  new Color(0.9f, 0.7f, 0.5f));
            CreateZone(root, "Shopping",   ZoneType.Shopping, new Vector3(56f, 0f, 0f),    new Vector2(10f, 24f), 999,  25f,  new Color(0.9f, 0.85f, 0.5f));
            CreateZone(root, "Gate",       ZoneType.Gate,     new Vector3(74f, 0f, 0f),    new Vector2(6f, 16f),  9999, 0f,   new Color(0.8f, 0.6f, 0.8f));

            Transform home1 = CreateEmpty(root, "RobotHome_1", new Vector3(31f, 0f, -2f));
            Transform home2 = CreateEmpty(root, "RobotHome_2", new Vector3(31f, 0f, 2f));

            // NavMesh をベイクしてアセットとして保存する
            NavMeshSurface surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.BuildNavMesh();
            AssetDatabase.DeleteAsset(NavMeshPath);
            if (surface.navMeshData != null && !AssetDatabase.Contains(surface.navMeshData))
                AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            EditorUtility.SetDirty(surface);

            GameObject simGo = new GameObject("SimulationManager");
            SimulationManager sim = simGo.AddComponent<SimulationManager>();
            simGo.AddComponent<KpiLogger>();
            simGo.AddComponent<MetaAIController>();
            sim.configAsset = GetOrCreateConfig();
            sim.robotHomes = new Transform[] { home1, home2 };

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 26f;
                cam.transform.position = new Vector3(35f, 60f, 0f);
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            AssetDatabase.SaveAssets();
            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("Airport scene built: " + ScenePath);
        }

        static void CreateZone(GameObject root, string name, ZoneType type, Vector3 center, Vector2 size, int servers, float serviceTime, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root.transform);
            go.transform.position = center + new Vector3(0f, 0.03f, 0f);
            go.transform.localScale = new Vector3(size.x, 0.05f, size.y);
            go.GetComponent<Renderer>().sharedMaterial = Mat("Zone_" + type, color);
            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            Zone z = go.AddComponent<Zone>();
            z.type = type;
            z.size = size;
            z.servers = servers;
            z.serviceTime = serviceTime;
        }

        static Transform CreateEmpty(GameObject root, string name, Vector3 pos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(root.transform);
            go.transform.position = pos;
            return go.transform;
        }

        static void DestroyIfExists(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }

        static SimConfig GetOrCreateConfig()
        {
            SimConfig c = AssetDatabase.LoadAssetAtPath<SimConfig>(ConfigPath);
            if (c == null)
            {
                c = ScriptableObject.CreateInstance<SimConfig>();
                AssetDatabase.CreateAsset(c, ConfigPath);
            }
            return c;
        }

        static Material Mat(string name, Color color)
        {
            string path = "Assets/_Project/Materials/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = color;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
