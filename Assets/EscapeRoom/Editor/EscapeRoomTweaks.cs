using System.Collections.Generic;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Locomotion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EscapeRoom.EditorTools
{
    /// <summary>
    /// Ajustes sobre IsaScene ya configurada (se puede ejecutar varias veces):
    ///  - Zona de agarre (bounds del TouchHandGrab) de los ingredientes cubre toda la malla (arregla la cebolla).
    ///  - El queso también se agarra con pinza/palma (HandGrab) y con control, no solo con touch grab.
    ///  - Radio conectada al progreso del juego (cuadro, cerradura, ojo).
    ///  - La bruja necesita 4 ataques para ganar.
    ///  - Puntos de teleport con el efecto del prefab TeleportationAnchor de VR Beginner.
    ///  - La cáscara exterior de la casa (HouseExteriorShell) bloquea al jugador también desde adentro.
    ///  - Palancas del caldero más fáciles: perilla híbrida, rayo + pinza, ayuda al soltar y avisos.
    /// </summary>
    public static class EscapeRoomTweaks
    {
        private const string AnchorPrefab = "Assets/VR Beginner/Prefabs/EscapeRoom/System/TeleportationAnchor.prefab";
        private const string FxName = "Teleport Anchor FX";
        private const string HotspotPrefix = "Teleport Hotspot";
        // En VR Beginner los TeleportationAnchor están ~8 cm bajo el piso: así el glifo queda a ras del suelo.
        private const float FxHeightOffset = -0.08f;
        private const float BoundsMargin = 0.015f;
        private const int WitchHitsToLose = 4;
        private const string HandGrabTemplateName = "[BuildingBlock] HandGrabInstallationRoutine";
        private const string HandGrabTemplateParent = "[BuildingBlock] Cook Book (Kinematic)";
        private static readonly string[] HandGrabFoods = { "Cheese" };
        private const string ShellName = "HouseExteriorShell";
        private const string ShellInnerName = "HouseExteriorShell (Collider interior)";
        private const string MeshDir = "Assets/EscapeRoom/Meshes";

        private static readonly List<string> Report = new List<string>();

        [MenuItem("Tools/Escape Room/Aplicar ajustes")]
        public static void ApplyMenu() => Debug.Log(Apply(true));

        [MenuItem("Tools/Escape Room/Quitar efecto TeleportationAnchor")]
        public static void RemoveTeleportFxMenu() => Debug.Log(RemoveTeleportFx(true));

        public static string Apply(bool save)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Escape Room: sal del Play Mode antes de aplicar los ajustes.";
            Report.Clear();
            FixIngredientGrabBounds();
            AddFoodHandGrab();
            WireRadio();
            SetWitchHits();
            SetupTeleportFx();
            AddInwardShellCollider();
            ImproveCauldronLevers();
            Finish(save);
            return "Escape Room – ajustes aplicados:\n" + string.Join("\n", Report);
        }

        public static string RemoveTeleportFx(bool save)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Escape Room: sal del Play Mode primero.";
            Report.Clear();
            foreach (var ti in Hotspots())
            {
                var hotspot = ti.transform.parent;
                var fx = hotspot.Find(FxName);
                if (fx != null) Undo.DestroyObjectImmediate(fx.gameObject);
                var visuals = ti.transform.Find("Visuals");
                if (visuals != null)
                {
                    Undo.RecordObject(visuals.gameObject, "Mostrar visual de Meta");
                    visuals.gameObject.SetActive(true);
                }
                Report.Add($"Teleport: {hotspot.name} vuelve al visual original de Meta.");
            }
            Finish(save);
            return "Escape Room:\n" + string.Join("\n", Report);
        }

        private static void Finish(bool save)
        {
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);
        }

        // =====================================================================
        // Ingredientes: la zona de detección del TouchHandGrab debe cubrir toda la malla.
        // Los dedos solo cuentan como "tocando" si están dentro de esa zona Y del collider,
        // así que una zona más chica que el objeto hace que sea difícil agarrarlo.
        // =====================================================================
        private static void FixIngredientGrabBounds()
        {
            var cauldron = Find<CauldronController>().FirstOrDefault();
            if (cauldron == null || cauldron.ingredients == null)
            {
                Report.Add("AVISO: no se encontró el caldero/ingredientes.");
                return;
            }

            foreach (var food in cauldron.ingredients)
            {
                if (food == null) continue;
                foreach (var thg in food.GetComponentsInChildren<TouchHandGrabInteractable>(true))
                {
                    var so = new SerializedObject(thg);
                    var boundsProp = so.FindProperty("_boundsCollider");
                    var colliders = so.FindProperty("_colliders");
                    var bounds = boundsProp.objectReferenceValue as Collider;
                    if (bounds == null) continue;

                    // Esquinas (en mundo) de todos los colliders que se pueden tocar.
                    var corners = new List<Vector3>();
                    for (int i = 0; i < colliders.arraySize; i++)
                    {
                        var c = colliders.GetArrayElementAtIndex(i).objectReferenceValue as Collider;
                        if (c != null) corners.AddRange(WorldCorners(c));
                    }
                    if (corners.Count == 0) continue;

                    Transform bt = bounds.transform;
                    float scale = Mathf.Max(1e-4f, MaxAbs(bt.lossyScale));
                    float margin = BoundsMargin / scale;
                    if (corners.All(p => Inside(bounds, bt.InverseTransformPoint(p), margin * 0.5f)))
                    {
                        Report.Add($"Agarre: {food.name} ya estaba bien.");
                        continue;
                    }

                    // Caja (en el espacio del collider de bounds) que cubre la malla + la zona anterior.
                    var local = new Bounds(bt.InverseTransformPoint(corners[0]), Vector3.zero);
                    foreach (var p in corners) local.Encapsulate(bt.InverseTransformPoint(p));
                    local.Expand(margin * 2f);
                    Bounds previous = LocalBounds(bounds);
                    local.Encapsulate(previous);

                    BoxCollider box = bounds as BoxCollider;
                    if (box == null)
                    {
                        box = Undo.AddComponent<BoxCollider>(bt.gameObject);
                        box.isTrigger = true;
                    }
                    else Undo.RecordObject(box, "Bounds de agarre");
                    box.center = local.center;
                    box.size = local.size;

                    boundsProp.objectReferenceValue = box;
                    so.ApplyModifiedProperties();
                    Report.Add($"Agarre: {food.name} – zona de agarre ampliada a toda la malla ({bt.name}, {Vector3.Scale(local.size, Abs(bt.lossyScale)) * 100f:F1} cm).");
                }
            }
        }

        private static IEnumerable<Vector3> WorldCorners(Collider c)
        {
            Bounds b;
            switch (c)
            {
                case MeshCollider mc when mc.sharedMesh != null: b = mc.sharedMesh.bounds; break;
                case BoxCollider bc: b = new Bounds(bc.center, bc.size); break;
                case SphereCollider sc: b = new Bounds(sc.center, Vector3.one * sc.radius * 2f); break;
                case CapsuleCollider cc: b = new Bounds(cc.center, Vector3.one * Mathf.Max(cc.height, cc.radius * 2f)); break;
                default: yield break;
            }
            for (int i = 0; i < 8; i++)
            {
                var o = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                yield return c.transform.TransformPoint(b.center + Vector3.Scale(b.extents, o));
            }
        }

        private static bool Inside(Collider bounds, Vector3 local, float tolerance)
        {
            switch (bounds)
            {
                case BoxCollider bc:
                    Vector3 d = local - bc.center;
                    Vector3 e = bc.size * 0.5f + Vector3.one * tolerance;
                    return Mathf.Abs(d.x) <= e.x && Mathf.Abs(d.y) <= e.y && Mathf.Abs(d.z) <= e.z;
                case SphereCollider sc:
                    return (local - sc.center).magnitude <= sc.radius + tolerance;
                default:
                    return false;
            }
        }

        private static Bounds LocalBounds(Collider c)
        {
            switch (c)
            {
                case BoxCollider bc: return new Bounds(bc.center, bc.size);
                case SphereCollider sc: return new Bounds(sc.center, Vector3.one * sc.radius * 2f);
                case CapsuleCollider cc: return new Bounds(cc.center, Vector3.one * Mathf.Max(cc.height, cc.radius * 2f));
                default: return new Bounds(Vector3.zero, Vector3.zero);
            }
        }

        // =====================================================================
        // Queso: además del touch grab (difícil con un objeto grande y en el simulador),
        // agarre con pinza/palma (HandGrabInteractable) y con control (GrabInteractable).
        // Usa la misma rutina del Cook Book que ya tienen la llave, la poción, el dial, etc.
        // =====================================================================
        private static void AddFoodHandGrab()
        {
            var cauldron = Find<CauldronController>().FirstOrDefault();
            if (cauldron == null || cauldron.ingredients == null) return;

            GameObject template = null;
            foreach (var t in Find<Transform>())
            {
                if (t.name == HandGrabTemplateName && t.parent != null && t.parent.name == HandGrabTemplateParent)
                {
                    template = t.gameObject;
                    break;
                }
            }
            if (template == null)
            {
                Report.Add("AVISO: no se encontró la rutina HandGrab del Cook Book para clonar.");
                return;
            }

            foreach (var food in cauldron.ingredients)
            {
                if (food == null || !HandGrabFoods.Any(n => food.name.Contains(n))) continue;
                if (food.GetComponentInChildren<HandGrabInteractable>(true) != null)
                {
                    Report.Add($"Agarre: {food.name} ya tenía agarre con pinza/palma.");
                    continue;
                }
                var grab = food.GetComponent<Grabbable>();
                var rb = food.GetComponent<Rigidbody>();
                if (grab == null || rb == null) continue;

                var go = Object.Instantiate(template, food.transform);
                Undo.RegisterCreatedObjectUndo(go, "HandGrab del ingrediente");
                go.name = "HandGrab Interactable";
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                var bb = go.GetComponent("BuildingBlock");
                if (bb != null) Object.DestroyImmediate(bb);
                foreach (var mb in go.GetComponents<MonoBehaviour>())
                {
                    if (!(mb is HandGrabInteractable) && !(mb is GrabInteractable)) continue;
                    var so = new SerializedObject(mb);
                    so.FindProperty("_pointableElement").objectReferenceValue = grab;
                    so.FindProperty("_rigidbody").objectReferenceValue = rb;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                Report.Add($"Agarre: {food.name} ahora también se agarra con pinza/palma y con control.");
            }
        }

        // =====================================================================
        // Casa: la malla HouseExteriorShell tiene las caras hacia afuera. Las consultas de física
        // (las usa el CharacterController de Meta para moverse) ignoran las caras traseras, así que
        // desde adentro la cáscara no frenaba al jugador. Se agrega un MeshCollider (no convexo)
        // con la misma malla pero con los triángulos invertidos: bloquea desde adentro y se puede
        // seguir caminando dentro de la casa.
        // =====================================================================
        private static void AddInwardShellCollider()
        {
            var shell = Find<MeshCollider>().FirstOrDefault(c => c.name == ShellName && c.sharedMesh != null);
            if (shell == null)
            {
                Report.Add("AVISO: no se encontró el MeshCollider de " + ShellName + ".");
                return;
            }

            if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/EscapeRoom", "Meshes");
            string path = MeshDir + "/HouseExteriorShell_Inward.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, path);
            }
            var src = shell.sharedMesh;
            mesh.Clear();
            mesh.name = "HouseExteriorShell_Inward";
            mesh.indexFormat = src.indexFormat;
            mesh.vertices = src.vertices;
            var tris = new List<int>();
            for (int sub = 0; sub < src.subMeshCount; sub++) tris.AddRange(src.GetTriangles(sub));
            for (int i = 0; i + 2 < tris.Count; i += 3)
            {
                int tmp = tris[i + 1];
                tris[i + 1] = tris[i + 2];
                tris[i + 2] = tmp;
            }
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();

            Transform inner = shell.transform.Find(ShellInnerName);
            if (inner == null)
            {
                var go = new GameObject(ShellInnerName);
                Undo.RegisterCreatedObjectUndo(go, "Collider interior de la casa");
                go.transform.SetParent(shell.transform, false);
                go.layer = shell.gameObject.layer;
                GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(shell.gameObject));
                inner = go.transform;
            }
            var col = inner.GetComponent<MeshCollider>();
            if (col == null) col = Undo.AddComponent<MeshCollider>(inner.gameObject);
            col.convex = false;
            col.sharedMesh = null;
            col.sharedMesh = mesh;
            col.sharedMaterial = shell.sharedMaterial;
            Report.Add($"Casa: {ShellName} ahora bloquea al jugador desde adentro (collider con la malla invertida, {tris.Count / 3} triángulos).");
        }

        // =====================================================================
        // Caldero: palancas más fáciles (sobre todo en el simulador).
        //  - La perilla de temperatura gira con la muñeca O moviendo la mano/rayo alrededor del centro.
        //  - Ambas palancas se pueden manejar con rayo + pinza: se apunta, se pinza y se arrastra
        //    sobre el plano del panel (RayInteractable con un PlaneSurface como superficie de arrastre).
        //  - Zona de agarre más grande para el deslizador de fuerza.
        //  - Al soltarlas pasado cierto punto terminan solas el recorrido, y cada paso avisa al completarse
        //    (lógica en CauldronController).
        // =====================================================================
        private static void ImproveCauldronLevers()
        {
            var cauldron = Find<CauldronController>().FirstOrDefault();
            if (cauldron == null || cauldron.temperatureLever == null || cauldron.spinLever == null)
            {
                Report.Add("AVISO: no se encontraron las palancas del caldero.");
                return;
            }

            Undo.RecordObject(cauldron, "Ayuda palancas");
            cauldron.assistLevers = true;
            if (cauldron.stepDoneClip == null)
                cauldron.stepDoneClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/VR Beginner/Audio/SFXCauldronButton02.ogg");
            EditorUtility.SetDirty(cauldron);

            var temp = cauldron.temperatureLever;
            Undo.RecordObject(temp, "Perilla de temperatura");
            temp.mode = AxisRotateTransformer.RotationMode.OrbitOrTwist;
            EditorUtility.SetDirty(temp);

            // Normal del panel (apunta hacia el jugador): el eje de la perilla.
            Vector3 panelNormal = temp.WorldAxis;

            var spin = cauldron.spinLever;
            var spinBounds = spin.transform.Find("GrabBounds") != null ? spin.transform.Find("GrabBounds").GetComponent<BoxCollider>() : null;
            if (spinBounds != null && spinBounds.size.x < 0.13f)
            {
                Undo.RecordObject(spinBounds, "Zona de agarre del deslizador");
                spinBounds.size = Vector3.Max(spinBounds.size, new Vector3(0.13f, 0.16f, 0.15f));
            }

            Vector3 slideAxis = (spin.axisReference != null ? spin.axisReference : spin.transform).TransformDirection(spin.localAxis).normalized;
            AddRayDrag(temp.gameObject, panelNormal);
            AddRayDrag(spin.gameObject, Vector3.ProjectOnPlane(panelNormal, slideAxis).normalized);

            Report.Add("Caldero: perilla de temperatura gira con la muñeca o moviendo la mano alrededor; palancas con rayo + pinza; ayuda al soltar y aviso al completar cada paso.");
        }

        private static void AddRayDrag(GameObject lever, Vector3 planeNormal)
        {
            var grab = lever.GetComponent<Grabbable>();
            var boundsT = lever.transform.Find("GrabBounds");
            var bounds = boundsT != null ? boundsT.GetComponent<Collider>() : null;
            if (grab == null || bounds == null)
            {
                Report.Add($"AVISO: {lever.name} sin Grabbable o GrabBounds; no se agregó el rayo.");
                return;
            }
            if (lever.GetComponentInChildren<RayInteractable>(true) != null) return;

            // Plano de arrastre (no se mueve con la palanca)
            var planeGo = new GameObject(lever.name + " Ray Drag Plane");
            Undo.RegisterCreatedObjectUndo(planeGo, "Plano de arrastre");
            planeGo.transform.SetParent(lever.transform.parent, false);
            planeGo.transform.SetPositionAndRotation(lever.transform.position, Quaternion.LookRotation(planeNormal));
            var plane = planeGo.AddComponent<Oculus.Interaction.Surfaces.PlaneSurface>();
            var pso = new SerializedObject(plane);
            pso.FindProperty("_facing").enumValueIndex = 1; // Forward
            pso.FindProperty("_doubleSided").boolValue = true;
            pso.ApplyModifiedPropertiesWithoutUndo();

            // Interactable de rayo: se apunta a la zona de agarre y se arrastra sobre el plano.
            var rayGo = new GameObject("Ray Interactable");
            Undo.RegisterCreatedObjectUndo(rayGo, "Rayo de la palanca");
            rayGo.transform.SetParent(lever.transform, false);
            var surface = rayGo.AddComponent<Oculus.Interaction.Surfaces.ColliderSurface>();
            var sso = new SerializedObject(surface);
            sso.FindProperty("_collider").objectReferenceValue = bounds;
            sso.ApplyModifiedPropertiesWithoutUndo();
            var ray = rayGo.AddComponent<RayInteractable>();
            var rso = new SerializedObject(ray);
            rso.FindProperty("_surface").objectReferenceValue = surface;
            rso.FindProperty("_selectSurface").objectReferenceValue = plane;
            rso.FindProperty("_pointableElement").objectReferenceValue = grab;
            rso.ApplyModifiedPropertiesWithoutUndo();
        }

        // =====================================================================
        // Radio: referencias al progreso del juego.
        // =====================================================================
        private static void WireRadio()
        {
            var radio = Find<RadioController>().FirstOrDefault();
            if (radio == null)
            {
                Report.Add("AVISO: no se encontró RadioController.");
                return;
            }
            Undo.RecordObject(radio, "Radio por etapas");
            radio.sunPainting = Find<SunPaintingTrap>().FirstOrDefault();
            radio.keyhole = Find<KeyholeLock>().FirstOrDefault();
            radio.eyeReader = Find<EyeBookReader>().FirstOrDefault();
            EditorUtility.SetDirty(radio);
            Report.Add($"Radio: pista según la etapa (cuadro={Name(radio.sunPainting)}, cerradura={Name(radio.keyhole)}, ojo={Name(radio.eyeReader)}).");
        }

        // =====================================================================
        // Bruja: 4 ataques para perder.
        // =====================================================================
        private static void SetWitchHits()
        {
            var manager = Find<EscapeGameManager>().FirstOrDefault();
            if (manager == null)
            {
                Report.Add("AVISO: no se encontró EscapeGameManager.");
                return;
            }
            Undo.RecordObject(manager, "Golpes de la bruja");
            manager.hitsToLose = WitchHitsToLose;
            EditorUtility.SetDirty(manager);
            Report.Add($"Bruja: el jugador pierde con {WitchHitsToLose} ataques.");
        }

        // =====================================================================
        // Teleport: efecto del TeleportationAnchor (VR Beginner) en cada Teleport Hotspot.
        // =====================================================================
        private static void SetupTeleportFx()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AnchorPrefab);
            if (prefab == null)
            {
                Report.Add("AVISO: no se encontró " + AnchorPrefab);
                return;
            }

            foreach (var ti in Hotspots())
            {
                var hotspot = ti.transform.parent;
                if (hotspot.Find(FxName) != null)
                {
                    Report.Add($"Teleport: {hotspot.name} ya tenía el efecto.");
                    continue;
                }

                var fx = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hotspot.gameObject.scene);
                PrefabUtility.UnpackPrefabInstance(fx, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Undo.RegisterCreatedObjectUndo(fx, "Efecto TeleportationAnchor");
                fx.name = FxName;
                fx.transform.SetParent(hotspot, false);
                fx.transform.localPosition = new Vector3(0f, FxHeightOffset, 0f);
                fx.transform.localRotation = Quaternion.identity;
                fx.transform.localScale = prefab.transform.localScale;

                // Solo se usa el efecto visual: fuera el TeleportationAnchor del XR Interaction Toolkit y la física.
                foreach (var mb in fx.GetComponents<MonoBehaviour>()) Object.DestroyImmediate(mb);
                foreach (var col in fx.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
                var rootRenderer = fx.GetComponent<MeshRenderer>();
                if (rootRenderer != null) Object.DestroyImmediate(rootRenderer);
                var rootFilter = fx.GetComponent<MeshFilter>();
                if (rootFilter != null) Object.DestroyImmediate(rootFilter);
                foreach (var t in fx.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = hotspot.gameObject.layer;

                var highlight = fx.AddComponent<TeleportAnchorHighlight>();
                highlight.SetInteractableView(ti);
                highlight.beams = fx.GetComponentsInChildren<TeleporterAnim>(true);

                // Se oculta el visual original de Meta (anillo + flecha animados).
                var visuals = ti.transform.Find("Visuals");
                if (visuals != null)
                {
                    Undo.RecordObject(visuals.gameObject, "Ocultar visual de Meta");
                    visuals.gameObject.SetActive(false);
                }

                Report.Add($"Teleport: {hotspot.name} usa el efecto de TeleportationAnchor ({highlight.beams.Length} rayos).");
            }
        }

        private static IEnumerable<TeleportInteractable> Hotspots() =>
            Find<TeleportInteractable>().Where(t => t.transform.parent != null && t.transform.parent.name.StartsWith(HotspotPrefix));

        // =====================================================================
        // Utilidades
        // =====================================================================
        private static IEnumerable<T> Find<T>() where T : Component
        {
            var scene = SceneManager.GetActiveScene();
            return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
        }

        private static string Name(Object o) => o != null ? o.name : "NO ENCONTRADO";
        private static float MaxAbs(Vector3 v) => Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));
        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
