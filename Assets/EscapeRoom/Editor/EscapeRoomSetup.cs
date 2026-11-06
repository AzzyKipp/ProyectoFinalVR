using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EscapeRoom.EditorTools
{
    /// <summary>
    /// Configura IsaScene con toda la lógica del escape room. Se ejecuta una sola vez desde
    /// Tools > Escape Room > Configurar IsaScene (si ya existe [ESCAPE ROOM] no hace nada).
    /// </summary>
    public static class EscapeRoomSetup
    {
        private const string RootName = "[ESCAPE ROOM]";
        private const string AssetDir = "Assets/EscapeRoom";
        private const string FontPath = "Assets/Fonts/AMARANTE-REGULAR SDF.asset";
        private const string Sfx = "Assets/VR Beginner/Audio/";
        private const string ClueDir = "Assets/Additional_Assets/Audios_Radio/";
        private const string WitchFbx = "Assets/Additional_Assets/witch/source/witchanimations.fbx";
        private const string WitchTex = "Assets/Additional_Assets/witch/textures/";
        private const string PokePrefab = "Packages/com.meta.xr.sdk.interaction/Runtime/Prefabs/Poke/PokeInteractable.prefab";

        private static TMP_FontAsset _font;
        private static Material _fontOverlay;
        private static GameObject _handGrabTemplate;
        private static GameObject _touchTemplate;
        private static readonly List<string> Report = new List<string>();

        [MenuItem("Tools/Escape Room/Configurar IsaScene")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            Report.Clear();
            if (FindGo(RootName) != null) return $"Ya existe {RootName}. Bórralo para volver a ejecutar la configuración.";

            EnsureFolder(AssetDir, "Materials");
            EnsureFolder(AssetDir, "Animations");
            EnsureFolder(AssetDir, "Textures");

            _font = Req(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath), FontPath);
            _handGrabTemplate = Req(FindGo("[BuildingBlock] HandGrabInstallationRoutine", "[BuildingBlock] Cook Book (Kinematic)"), "HandGrab template");
            _touchTemplate = Req(FindGo("[BuildingBlock] TouchHandGrabInstallationRoutine", "[BuildingBlock] Cheese"), "TouchHandGrab template");
            Physics.SyncTransforms();

            var root = new GameObject(RootName);
            var head = Req(FindGo("CenterEyeAnchor"), "CenterEyeAnchor").transform;

            // --- Efectos, HUD, contador ---
            var fx = BuildFx(root.transform);
            var hud = BuildHud(root.transform, head);
            var timer = BuildTimerPanel(root.transform);

            // --- Cajonera y cajones ---
            FixDeskAndDrawers(out var lockedDrawer);

            // --- Cuadro del sol + llave + cerradura ---
            var key = BuildKey(root.transform);
            SetupSunPainting(key);
            SetupKeyhole(lockedDrawer, key);

            // --- Libro de recetas + ojo + comida ---
            var food = SetupBookAndFood(root.transform);

            // --- Radio ---
            SetupRadio();

            // --- Caldero + poción ---
            var drinkButton = BuildDrinkButton(root.transform);
            var potion = BuildPotion(root.transform, drinkButton);
            SetupCauldron(root.transform, food, potion);

            // --- Puerta ---
            var door = SetupExitDoor();

            // --- Bruja + libros ---
            var witch = BuildWitch(root.transform, head);
            TagThrowableBooks();

            // --- Manager ---
            var managerGo = new GameObject("Game Manager");
            managerGo.transform.SetParent(root.transform, false);
            var manager = managerGo.AddComponent<EscapeGameManager>();
            manager.hud = hud;
            manager.timerUI = timer;
            manager.witch = witch;
            manager.exitDoor = door;
            manager.playerHead = head;
            manager.sfx = AddAudio(managerGo, 0f);
            manager.hurtClip = Clip(Sfx + "SFXImpactHeavy02.ogg");
            manager.timeUpClip = Clip(Sfx + "SFXCrow.ogg");
            manager.winClip = Clip(Sfx + "SFXEscape.wav");
            manager.loseClip = Clip(Sfx + "SFXFireFlare.ogg");

            // Placeholder vacío del dial que estaba en el origen
            var placeholder = FindGo("[BuildingBlock] Dial 1", "[CONTROLLABLES]");
            if (placeholder != null)
            {
                placeholder.SetActive(false);
                Note("Desactivado placeholder [CONTROLLABLES]/[BuildingBlock] Dial 1 (cubo en el origen).");
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Note("Configuración completada.");
            return string.Join("\n", Report);
        }

        // =====================================================================
        // FX
        // =====================================================================
        private static FxLibrary BuildFx(Transform parent)
        {
            var go = new GameObject("FX");
            go.transform.SetParent(parent, false);
            var lib = go.AddComponent<FxLibrary>();
            var mat = ParticleMaterial();
            lib.poof = MakeBurst(go.transform, "Poof", mat, new Color(0.55f, 0.25f, 0.85f, 0.85f), new Color(0.25f, 0.1f, 0.4f, 0.7f), -0.05f, 50, 0.15f);
            lib.splash = MakeBurst(go.transform, "Splash", mat, new Color(0.45f, 1f, 0.35f, 0.9f), new Color(0.2f, 0.7f, 0.25f, 0.8f), 1.2f, 35, 0.06f);
            return lib;
        }

        private static ParticleSystem MakeBurst(Transform parent, string name, Material mat, Color a, Color b, float gravity, short count, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(size, size * 2.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 200;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            return ps;
        }

        private static Material ParticleMaterial()
        {
            string texPath = AssetDir + "/Textures/SoftParticle.png";
            if (!File.Exists(texPath))
            {
                var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f)) / 32f;
                    float alpha = Mathf.Clamp01(1f - d);
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                tex.Apply();
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath);
                var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
            }
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            return SaveMat(mat, "MagicParticles");
        }

        // =====================================================================
        // HUD + contador
        // =====================================================================
        private static HeadMessageUI BuildHud(Transform parent, Transform head)
        {
            _fontOverlay = new Material(_font.material) { shader = Shader.Find("TextMeshPro/Distance Field Overlay") };
            _fontOverlay = SaveMat(_fontOverlay, "Amarante Overlay");

            var canvas = MakeCanvas(parent, "HUD Message", new Vector2(900f, 300f), 0.001f);
            canvas.sortingOrder = 100;
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var bg = new GameObject("Background", typeof(RectTransform)).AddComponent<Image>();
            bg.transform.SetParent(canvas.transform, false);
            bg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.08f, 0.03f, 0.12f, 0.78f);
            Stretch(bg.rectTransform);

            var label = MakeText(canvas.transform, "Label", "", 56f, Color.white);
            label.fontSharedMaterial = _fontOverlay;
            label.margin = new Vector4(40f, 20f, 40f, 20f);

            var hud = canvas.gameObject.AddComponent<HeadMessageUI>();
            hud.head = head;
            hud.group = group;
            hud.label = label;
            group.alpha = 0f;
            return hud;
        }

        private static CountdownTimerUI BuildTimerPanel(Transform parent)
        {
            var closet3 = Req(FindGo("Closet3"), "Closet3").transform;
            Vector3 flatRight = closet3.right;
            flatRight.y = 0f;
            flatRight.Normalize();
            Vector3 probe = closet3.position + flatRight * 0.16f;
            float topY = 1.22f;
            if (Physics.Raycast(new Vector3(probe.x, 2.4f, probe.z), Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
                topY = hit.point.y;

            var panel = new GameObject("Timer Panel (Kinematic)");
            panel.transform.SetParent(parent, false);
            panel.transform.position = new Vector3(probe.x, topY + 0.11f, probe.z);
            panel.transform.rotation = Quaternion.LookRotation(flatRight, Vector3.up);
            var rb = panel.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(panel.transform, false);
            board.transform.localScale = new Vector3(0.38f, 0.21f, 0.03f);
            board.GetComponent<Renderer>().sharedMaterial = LitMat("Timer Board", new Color(0.16f, 0.08f, 0.05f));

            var trim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trim.name = "Trim";
            trim.transform.SetParent(panel.transform, false);
            trim.transform.localPosition = new Vector3(0f, 0f, -0.006f);
            trim.transform.localScale = new Vector3(0.35f, 0.18f, 0.022f);
            trim.GetComponent<Renderer>().sharedMaterial = LitMat("Timer Face", new Color(0.12f, 0.04f, 0.16f));
            Object.DestroyImmediate(trim.GetComponent<Collider>());

            var canvas = MakeCanvas(panel.transform, "Canvas", new Vector2(340f, 175f), 0.001f);
            canvas.transform.localPosition = new Vector3(0f, 0f, -0.0185f);
            var title = MakeText(canvas.transform, "Title", "TIEMPO RESTANTE", 28f, new Color(0.95f, 0.78f, 0.5f));
            SetAnchors(title.rectTransform, new Vector2(0f, 0.68f), new Vector2(1f, 1f));
            var time = MakeText(canvas.transform, "Time", "08:00", 104f, new Color(1f, 0.86f, 0.45f));
            SetAnchors(time.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.75f));

            var ui = panel.AddComponent<CountdownTimerUI>();
            ui.timeLabel = time;
            Note($"Panel del contador sobre la cajonera en {panel.transform.position}.");
            return ui;
        }

        // =====================================================================
        // Cajonera
        // =====================================================================
        private static void FixDeskAndDrawers(out AxisSlideTransformer lockedDrawer)
        {
            // La cajonera es mueble fijo: no debe poder agarrarse.
            var desk = Req(FindGo("[BuildingBlock] Cajonera"), "Cajonera");
            var deskGrab = desk.GetComponent<Grabbable>();
            if (deskGrab != null) deskGrab.enabled = false;
            var deskRoutine = desk.transform.Find("[BuildingBlock] TouchHandGrabInstallationRoutine");
            if (deskRoutine != null) deskRoutine.gameObject.SetActive(false);
            var deskRb = desk.GetComponent<Rigidbody>();
            if (deskRb != null)
            {
                deskRb.isKinematic = true;
                deskRb.useGravity = false;
            }
            Note("Cajonera: desactivado su Grabbable/TouchHandGrab (era agarrable y se podía mover).");

            // Constraints viejos que apuntaban a sí mismos
            foreach (var pc in Object.FindObjectsByType<PositionConstraint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (pc.name == "Closet1" || pc.name == "Box")
                {
                    Note($"Eliminado PositionConstraint en {pc.name}.");
                    Object.DestroyImmediate(pc);
                }
            }

            // BoxColliders con tamaño negativo en las bandejas de los cajones
            foreach (var box in desk.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BoxCollider>(true)))
            {
                Vector3 s = box.size;
                if (s.x < 0f || s.y < 0f || s.z < 0f)
                {
                    box.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                    Note($"Corregido BoxCollider con tamaño negativo en {box.transform.parent?.name}/{box.name}.");
                }
            }

            lockedDrawer = null;
            string[] roman = { "I", "II", "III", "IV" };
            for (int i = 0; i < roman.Length; i++)
            {
                var drawer = Req(FindGo("[BuildingBlock] Cajon " + roman[i]), "Cajon " + roman[i]);
                var closet = Req(FindGo("Closet" + (i + 1)), "Closet" + (i + 1));
                var rb = drawer.GetComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.None;

                var slide = drawer.AddComponent<AxisSlideTransformer>();
                slide.target = drawer.transform;
                slide.axisReference = closet.transform;
                slide.localAxis = Vector3.left;
                slide.minDistance = 0f;
                slide.maxDistance = 0.36f;

                var grab = drawer.GetComponent<Grabbable>();
                ConfigureGrabbable(grab, drawer.transform, rb, slide, false);
                AddHandGrab(drawer, grab, rb);
                if (i == 2) lockedDrawer = slide;
            }
            Note("Cajones: Rigidbody kinemático + AxisSlideTransformer (solo se deslizan 36 cm hacia afuera).");
        }

        // =====================================================================
        // Llave, cuadro y cerradura
        // =====================================================================
        private static GameObject BuildKey(Transform parent)
        {
            var key = new GameObject("Key (Llave)");
            key.transform.SetParent(parent, false);
            var painting = Req(FindGo("painting2"), "painting2");
            key.transform.position = new Vector3(painting.transform.position.x, 0.05f, painting.transform.position.z + 0.2f);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VR Beginner/Models/Key.FBX"), key.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * 1.6f;
            foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            var rb = key.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            rb.interpolation = RigidbodyInterpolation.None; // el SDK de Meta mueve el transform directamente
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var box = key.AddComponent<BoxCollider>();
            box.size = new Vector3(0.1f, 0.022f, 0.21f);
            var bounds = AddBox(key.transform, "GrabBounds", Vector3.zero, Quaternion.identity, new Vector3(0.17f, 0.1f, 0.28f), true);

            var grab = key.AddComponent<Grabbable>();
            ConfigureGrabbable(grab, key.transform, rb, null, true);
            AddTouch(key, grab, new Collider[] { box }, bounds);
            AddHandGrab(key, grab, rb);
            key.SetActive(false);
            return key;
        }

        private static void SetupSunPainting(GameObject key)
        {
            var painting = Req(FindGo("painting2"), "painting2");
            var mf = painting.GetComponent<MeshFilter>();
            foreach (var mc in painting.GetComponents<MeshCollider>()) mc.enabled = false;
            var box = painting.AddComponent<BoxCollider>();
            box.center = mf.sharedMesh.bounds.center;
            box.size = mf.sharedMesh.bounds.size + new Vector3(0f, 0f, 0.01f);
            var bounds = AddBox(painting.transform, "GrabBounds", mf.sharedMesh.bounds.center, Quaternion.identity,
                mf.sharedMesh.bounds.size + new Vector3(0.08f, 0.08f, 0.14f), true);

            var rb = painting.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.mass = 1.5f;
            rb.interpolation = RigidbodyInterpolation.None; // el SDK de Meta mueve el transform directamente
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var grab = painting.AddComponent<Grabbable>();
            ConfigureGrabbable(grab, painting.transform, rb, null, false);
            AddTouch(painting, grab, new Collider[] { box }, bounds);
            AddHandGrab(painting, grab, rb);

            var trap = painting.AddComponent<SunPaintingTrap>();
            trap.grabbable = grab;
            trap.body = rb;
            trap.key = key;
            trap.floorY = 0.01f;
            trap.audioSource = AddAudio(painting, 1f, 1f, 12f);
            trap.fallClip = Clip(Sfx + "SFXImpactLog01.ogg");
            trap.poofClip = Clip(Sfx + "SFXWandTwinkleZap.wav");
            trap.keyClip = Clip(Sfx + "SFXKeyDrop.wav");
            Note("Cuadro morado del sol (painting2): al agarrarlo cae, desaparece y aparece la llave.");
        }

        private static void SetupKeyhole(AxisSlideTransformer drawer, GameObject key)
        {
            var keyhole = Req(FindGo("Keyhole"), "Keyhole");
            var zone = new GameObject("Keyhole Lock");
            zone.transform.SetParent(keyhole.transform, false);
            zone.transform.position = keyhole.GetComponent<Renderer>().bounds.center;
            zone.transform.rotation = keyhole.transform.rotation;
            var lockComp = zone.AddComponent<KeyholeLock>();
            lockComp.drawer = drawer;
            lockComp.keyRoot = key;
            lockComp.keyCollider = key.GetComponent<BoxCollider>();
            lockComp.outwardLocal = Vector3.left;
            lockComp.audioSource = AddAudio(zone, 1f, 1f, 10f);
            lockComp.unlockClip = Clip(Sfx + "SFXChestLid.ogg");
            lockComp.lockedClip = Clip(Sfx + "SFXImpactDrawer01.ogg");
            Note("Cajón III bloqueado hasta que la llave toque la cerradura.");
        }

        // =====================================================================
        // Libro, ojo y comida
        // =====================================================================
        private static GameObject[] SetupBookAndFood(Transform parent)
        {
            var bookRoot = Req(FindGo("[BuildingBlock] Cook Book (Kinematic)"), "Cook Book");
            var bookMesh = Req(FindGo("BookOfSpells", bookRoot.name), "BookOfSpells");
            var bookCollider = bookMesh.GetComponent<MeshCollider>();
            bookCollider.convex = true;
            var bookGrab = bookRoot.GetComponent<Grabbable>();
            SetBool(bookGrab, "_throwWhenUnselected", false);
            var detach = bookRoot.AddComponent<DetachOnGrab>();
            detach.grabbable = bookGrab;
            detach.newParent = Req(FindGo("[PHYSICS AND TOUCH]"), "[PHYSICS AND TOUCH]").transform;

            string[] foodRoots = { "[BuildingBlock] Pumpkin", "[BuildingBlock] Cheese", "[BuildingBlock] Orange", "[BuildingBlock] Meat", "[BuildingBlock] Onion" };
            var food = new List<GameObject>();
            foreach (var name in foodRoots)
            {
                var f = Req(FindGo(name), name);
                foreach (Transform child in f.transform) child.gameObject.SetActive(true);
                var rb = f.GetComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.interpolation = RigidbodyInterpolation.None; // el SDK de Meta mueve el transform directamente
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                // Un MeshCollider cóncavo se ignora en cuerpos dinámicos (la cebolla atravesaba el piso).
                foreach (var mc in f.GetComponentsInChildren<MeshCollider>(true))
                    if (!mc.isTrigger) mc.convex = true;
                f.SetActive(false);
                food.Add(f);
            }

            var eye = Req(FindGo("EyePainting6"), "EyePainting6");
            var zone = new GameObject("Eye Book Zone");
            zone.transform.SetParent(parent, false);
            zone.transform.position = new Vector3(2.2f, 1.6f, 1.0f);
            var reader = zone.AddComponent<EyeBookReader>();
            reader.bookColliders = new Collider[] { bookCollider };
            reader.zoneSize = new Vector3(1.3f, 1.7f, 1.7f);
            reader.foodToActivate = food.ToArray();
            reader.audioSource = AddAudio(zone, 1f, 1.5f, 15f);
            reader.readClip = Clip(Sfx + "SFXBookOpens.ogg");
            reader.dropClip = Clip(Sfx + "SFXImpactSoft01.ogg");

            var lightGo = new GameObject("Eye Light");
            lightGo.transform.SetParent(zone.transform, false);
            lightGo.transform.position = eye.GetComponent<Renderer>().bounds.center + Vector3.left * 0.25f;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.15f);
            light.range = 3f;
            light.intensity = 0f;
            reader.eyeLight = light;

            Note("Comida (calabaza, queso, naranja, carne, cebolla) oculta hasta que el libro se ponga frente al ojo.");
            return food.ToArray();
        }

        // =====================================================================
        // Radio
        // =====================================================================
        private static void SetupRadio()
        {
            var radio = Req(FindGo("Radio", "_Objects_Wrapper"), "Radio");
            var dial = Req(FindGo("Dial1", "Radio"), "Dial1");
            var mf = dial.GetComponent<MeshFilter>();
            foreach (var mc in dial.GetComponents<MeshCollider>()) mc.enabled = false;
            var box = dial.AddComponent<BoxCollider>();
            box.center = mf.sharedMesh.bounds.center;
            box.size = mf.sharedMesh.bounds.size + new Vector3(0.01f, 0.01f, 0.02f);
            var bounds = AddBox(dial.transform, "GrabBounds", mf.sharedMesh.bounds.center, Quaternion.identity, new Vector3(0.17f, 0.17f, 0.14f), true);

            var rb = dial.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var rot = dial.AddComponent<AxisRotateTransformer>();
            rot.target = dial.transform;
            rot.mode = AxisRotateTransformer.RotationMode.Twist;
            rot.localAxis = Vector3.forward;
            rot.startAngle = 0f;
            rot.minAngle = -300f;
            rot.maxAngle = 300f;

            var grab = dial.AddComponent<Grabbable>();
            ConfigureGrabbable(grab, dial.transform, rb, rot, false);
            AddTouch(dial, grab, new Collider[] { box }, bounds);
            AddHandGrab(dial, grab, rb);

            var logic = new GameObject("Radio Logic");
            logic.transform.SetParent(radio.transform, false);
            var ctrl = logic.AddComponent<RadioController>();
            ctrl.dial = rot;
            ctrl.voiceSource = AddAudio(logic, 0.85f, 1.5f, 15f);
            ctrl.fxSource = AddAudio(logic, 0.85f, 1.5f, 15f);
            ctrl.fxSource.volume = 0.6f;
            ctrl.clueClips = new[]
            {
                Clip(ClueDir + "[Audio_1].mp3"),
                Clip(ClueDir + "[Audio_2].mp3"),
                Clip(ClueDir + "[Audio_3].mp3"),
            };
            ctrl.tuningClip = Clip(Sfx + "FutzedRadioTuning.ogg");
            ctrl.clickClip = Clip(Sfx + "SFXCauldronButton03.ogg");
            Note("Radio: Dial1 gira (twist) y reproduce Audio_1/2/3 en los niveles 1/2/3.");
        }

        // =====================================================================
        // Caldero y poción
        // =====================================================================
        private static PokeButton BuildDrinkButton(Transform parent)
        {
            var root = new GameObject("Drink Potion Button");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(-6f, 1.2f, 0.5f);

            var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "Backplate";
            back.transform.SetParent(root.transform, false);
            back.transform.localPosition = new Vector3(0f, 0f, 0.012f);
            back.transform.localScale = new Vector3(0.22f, 0.095f, 0.02f);
            back.GetComponent<Renderer>().sharedMaterial = LitMat("Drink Button Back", new Color(0.12f, 0.05f, 0.16f));
            Object.DestroyImmediate(back.GetComponent<Collider>());

            var face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.name = "Face";
            face.transform.SetParent(root.transform, false);
            face.transform.localScale = new Vector3(0.19f, 0.07f, 0.014f);
            face.GetComponent<Renderer>().sharedMaterial = LitMat("Drink Button Face", new Color(0.55f, 0.2f, 0.75f), new Color(0.35f, 0.08f, 0.5f));
            Object.DestroyImmediate(face.GetComponent<Collider>());

            var canvas = MakeCanvas(face.transform, "Canvas", new Vector2(190f, 70f), 0.001f);
            canvas.transform.localScale = new Vector3(0.001f / 0.19f, 0.001f / 0.07f, 1f);
            canvas.transform.localPosition = new Vector3(0f, 0f, -0.55f);
            MakeText(canvas.transform, "Label", "Beber poción", 34f, Color.white);

            var frame = new GameObject("PressFrame");
            frame.transform.SetParent(root.transform, false);
            frame.transform.localPosition = new Vector3(0f, 0f, -0.007f);

            var poke = InstantiatePoke(frame.transform, new Vector3(0.19f, 0.07f, 0.05f));
            var button = root.AddComponent<PokeButton>();
            button.pokeInteractable = poke;
            button.pressFrame = frame.transform;
            button.halfSize = new Vector2(0.1f, 0.045f);
            button.pressDepth = 0.01f;
            button.pressVisual = face.transform;
            button.pressTravel = 0.008f;
            button.audioSource = AddAudio(root, 0.5f, 1f, 5f);
            button.pressClip = Clip(Sfx + "SFXCauldronButton02.ogg");
            root.SetActive(false);
            return button;
        }

        private static PotionItem BuildPotion(Transform parent, PokeButton drinkButton)
        {
            var potion = new GameObject("Potion (Poción)");
            potion.transform.SetParent(parent, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VR Beginner/Models/PotionBottle1.FBX"), potion.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var b = ZoneUtil.GetBounds(model);
            model.transform.localPosition = -(b.center - potion.transform.position);

            var rb = potion.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.interpolation = RigidbodyInterpolation.None; // el SDK de Meta mueve el transform directamente
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var capsule = potion.AddComponent<CapsuleCollider>();
            capsule.radius = 0.07f;
            capsule.height = 0.29f;
            capsule.direction = 1;
            var bounds = AddBox(potion.transform, "GrabBounds", Vector3.zero, Quaternion.identity, new Vector3(0.24f, 0.38f, 0.24f), true);

            var grab = potion.AddComponent<Grabbable>();
            ConfigureGrabbable(grab, potion.transform, rb, null, true);
            AddTouch(potion, grab, new Collider[] { capsule }, bounds);
            AddHandGrab(potion, grab, rb);

            var item = potion.AddComponent<PotionItem>();
            item.grabbable = grab;
            item.body = rb;
            item.drinkButton = drinkButton;
            item.audioSource = AddAudio(potion, 0.7f, 1f, 10f);
            item.grabClip = Clip(Sfx + "SFXCorkPop01.ogg");
            item.drinkClip = Clip(Sfx + "SFXLiquidPour.ogg");
            potion.SetActive(false);
            return item;
        }

        private static void SetupCauldron(Transform parent, GameObject[] food, PotionItem potion)
        {
            var water = Req(FindGo("Water"), "Water");

            // Tapa (bisagra)
            var lid = Req(FindGo("CauldronLid"), "CauldronLid");
            var lidMf = lid.GetComponent<MeshFilter>();
            var lidCollider = lid.GetComponent<MeshCollider>();
            lidCollider.convex = true;
            var lidBounds = AddBox(lid.transform, "GrabBounds", lidMf.sharedMesh.bounds.center, Quaternion.identity, lidMf.sharedMesh.bounds.size + new Vector3(0.12f, 0.15f, 0.12f), true);
            var lidRb = lid.AddComponent<Rigidbody>();
            lidRb.isKinematic = true;
            lidRb.useGravity = false;
            var lidRot = lid.AddComponent<AxisRotateTransformer>();
            lidRot.target = lid.transform;
            lidRot.mode = AxisRotateTransformer.RotationMode.Orbit;
            lidRot.localAxis = Vector3.right;
            lidRot.startAngle = NormalizeAngle(lid.transform.localEulerAngles.x);
            lidRot.minAngle = 0f;
            lidRot.maxAngle = 120f;
            var lidGrab = lid.AddComponent<Grabbable>();
            ConfigureGrabbable(lidGrab, lid.transform, lidRb, lidRot, false);
            AddTouch(lid, lidGrab, new Collider[] { lidCollider }, lidBounds);
            AddHandGrab(lid, lidGrab, lidRb);

            // Palanca de fuerza (desliza sobre su base)
            var spinBase = Req(FindGo("SpinForceBase"), "SpinForceBase");
            var spin = Req(FindGo("SpinForceLever"), "SpinForceLever");
            var oldCol = spin.transform.Find("collider");
            if (oldCol != null)
            {
                foreach (var c in oldCol.GetComponents<Collider>()) c.enabled = false;
                Note("SpinForceLever/collider desactivado (era el collider de la base pegado a la palanca).");
            }
            foreach (var mc in spin.GetComponents<MeshCollider>()) mc.enabled = false;
            var spinMf = spin.GetComponent<MeshFilter>();
            var spinBox = spin.AddComponent<BoxCollider>();
            spinBox.center = spinMf.sharedMesh.bounds.center;
            spinBox.size = new Vector3(0.045f, 0.08f, 0.07f);
            var spinBounds = AddBox(spin.transform, "GrabBounds", spinMf.sharedMesh.bounds.center, Quaternion.identity, new Vector3(0.1f, 0.13f, 0.12f), true);
            var spinRb = spin.AddComponent<Rigidbody>();
            spinRb.isKinematic = true;
            spinRb.useGravity = false;
            var spinSlide = spin.AddComponent<AxisSlideTransformer>();
            spinSlide.target = spin.transform;
            spinSlide.axisReference = spinBase.transform;
            spinSlide.localAxis = Vector3.right;
            spinSlide.minDistance = 0f;
            spinSlide.maxDistance = 0.3f;
            var spinGrab = spin.AddComponent<Grabbable>();
            ConfigureGrabbable(spinGrab, spin.transform, spinRb, spinSlide, false);
            AddTouch(spin, spinGrab, new Collider[] { spinBox }, spinBounds);
            AddHandGrab(spin, spinGrab, spinRb);

            // Palanca de temperatura (perilla)
            var temp = Req(FindGo("TemperatureLever"), "TemperatureLever");
            foreach (var mc in temp.GetComponents<MeshCollider>()) mc.enabled = false;
            var tempMf = temp.GetComponent<MeshFilter>();
            var tempBox = temp.AddComponent<BoxCollider>();
            tempBox.center = tempMf.sharedMesh.bounds.center;
            tempBox.size = tempMf.sharedMesh.bounds.size + new Vector3(0.01f, 0.03f, 0.01f);
            var tempBounds = AddBox(temp.transform, "GrabBounds", tempMf.sharedMesh.bounds.center, Quaternion.identity, new Vector3(0.21f, 0.14f, 0.21f), true);
            var tempRb = temp.AddComponent<Rigidbody>();
            tempRb.isKinematic = true;
            tempRb.useGravity = false;
            var tempRot = temp.AddComponent<AxisRotateTransformer>();
            tempRot.target = temp.transform;
            tempRot.mode = AxisRotateTransformer.RotationMode.Twist;
            tempRot.localAxis = Vector3.up;
            tempRot.minAngle = -170f;
            tempRot.maxAngle = 170f;
            var tempGrab = temp.AddComponent<Grabbable>();
            ConfigureGrabbable(tempGrab, temp.transform, tempRb, tempRot, false);
            AddTouch(temp, tempGrab, new Collider[] { tempBox }, tempBounds);
            AddHandGrab(temp, tempGrab, tempRb);

            // Botón BREW (poke)
            var brew = Req(FindGo("BrewButton"), "BrewButton");
            var frame = new GameObject("Brew PressFrame");
            frame.transform.SetParent(brew.transform.parent, false);
            frame.transform.position = brew.transform.TransformPoint(new Vector3(0f, 0.012f, 0f));
            frame.transform.rotation = Quaternion.LookRotation(-brew.transform.up, brew.transform.forward);
            var poke = InstantiatePoke(frame.transform, new Vector3(0.16f, 0.16f, 0.05f));
            var brewButton = frame.AddComponent<PokeButton>();
            brewButton.pokeInteractable = poke;
            brewButton.pressFrame = frame.transform;
            brewButton.halfSize = new Vector2(0.08f, 0.08f);
            brewButton.pressDepth = 0.01f;
            brewButton.pressVisual = brew.transform;
            brewButton.pressTravel = 0.012f;
            brewButton.audioSource = AddAudio(frame, 1f, 1f, 10f);
            brewButton.pressClip = Clip(Sfx + "SFXCauldronButton01.ogg");

            // Lógica
            var logic = new GameObject("Cauldron Logic");
            logic.transform.SetParent(parent, false);
            logic.transform.position = water.transform.position + Vector3.down * 0.16f;
            var spawn = new GameObject("Potion Spawn");
            spawn.transform.SetParent(logic.transform, false);
            spawn.transform.position = water.transform.position + Vector3.up * 0.35f;

            var ctrl = logic.AddComponent<CauldronController>();
            ctrl.ingredients = food;
            ctrl.potZone = logic.transform;
            ctrl.potZoneSize = new Vector3(0.7f, 0.95f, 0.75f);
            ctrl.lid = lidRot;
            ctrl.spinLever = spinSlide;
            ctrl.temperatureLever = tempRot;
            ctrl.brewButton = brewButton;
            ctrl.potion = potion;
            ctrl.potionSpawn = spawn.transform;
            ctrl.potionLaunchVelocity = new Vector3(0f, 3.6f, -1.5f);
            ctrl.sfx = AddAudio(logic, 1f, 1.5f, 15f);
            ctrl.bubbleLoop = AddAudio(logic, 1f, 1.5f, 12f, Clip(Sfx + "SFXCauldronBubbleLoop.ogg"), true);
            ctrl.splashClip = Clip(Sfx + "SFXCauldronBubbles02.wav");
            ctrl.brewClip = Clip(Sfx + "SFXCauldronBrew.ogg");
            ctrl.errorClip = Clip(Sfx + "SFXDoorClosed.ogg");
            ctrl.readyClip = Clip(Sfx + "SFXPotionReady.ogg");
            ctrl.lidClip = Clip(Sfx + "SFXCauldronCreak01.ogg");
            Note($"Caldero: tapa (bisagra, inicia en {lidRot.startAngle:0.#}°), palanca de fuerza, temperatura y botón BREW con poke.");
        }

        // =====================================================================
        // Puerta
        // =====================================================================
        private static ExitDoorController SetupExitDoor()
        {
            var doorRoot = Req(FindGo("ExitDoor", "_Objects_Wrapper"), "ExitDoor");
            if (PrefabUtility.IsPartOfPrefabInstance(doorRoot))
            {
                PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(doorRoot), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
            var leaf = Req(FindGo("ExitDoor", "ExitDoor"), "ExitDoor/ExitDoor");
            var collider = doorRoot.transform.Find("Collider");

            var hinge = new GameObject("DoorHinge");
            hinge.transform.SetParent(doorRoot.transform, false);
            hinge.transform.position = leaf.transform.position;
            hinge.transform.rotation = Quaternion.identity;
            leaf.transform.SetParent(hinge.transform, true);
            if (collider != null) collider.SetParent(hinge.transform, true);
            leaf.isStatic = false;
            foreach (Transform t in leaf.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;

            var leafBounds = leaf.GetComponent<Renderer>().bounds;
            var grabBox = AddBox(hinge.transform, "DoorGrabBox", hinge.transform.InverseTransformPoint(leafBounds.center), Quaternion.identity,
                new Vector3(0.16f, leafBounds.size.y * 0.9f, leafBounds.size.z * 0.95f), false);
            var bounds = AddBox(hinge.transform, "GrabBounds", hinge.transform.InverseTransformPoint(leafBounds.center), Quaternion.identity,
                new Vector3(0.45f, leafBounds.size.y, leafBounds.size.z + 0.1f), true);

            var rb = hinge.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var rot = hinge.AddComponent<AxisRotateTransformer>();
            rot.target = hinge.transform;
            rot.mode = AxisRotateTransformer.RotationMode.Orbit;
            rot.localAxis = Vector3.up;
            rot.startAngle = 0f;
            rot.minAngle = 0f;
            rot.maxAngle = 100f;
            rot.Locked = true;
            var grab = hinge.AddComponent<Grabbable>();
            ConfigureGrabbable(grab, hinge.transform, rb, rot, false);
            AddTouch(hinge, grab, new Collider[] { grabBox }, bounds);
            AddHandGrab(hinge, grab, rb);

            var ctrl = hinge.AddComponent<ExitDoorController>();
            ctrl.hinge = rot;
            ctrl.audioSource = AddAudio(hinge, 1f, 1.5f, 15f);
            ctrl.lockedClip = Clip(Sfx + "SFXDoorClosed.ogg");
            ctrl.unlockClip = Clip(Sfx + "SFXDoorOpen.ogg");
            Note("Puerta de salida: bisagra bloqueada (estática) hasta beber la poción; se abre máx. 100°.");
            return ctrl;
        }

        // =====================================================================
        // Bruja
        // =====================================================================
        private static WitchController BuildWitch(Transform parent, Transform head)
        {
            // Importación: idle y walk en loop, normal map
            var imp = (ModelImporter)AssetImporter.GetAtPath(WitchFbx);
            var clips = imp.defaultClipAnimations;
            foreach (var c in clips)
            {
                string n = c.name.ToLowerInvariant();
                c.loopTime = n.Contains("idle") || n.Contains("walk");
            }
            imp.clipAnimations = clips;
            imp.SaveAndReimport();

            string normalPath = WitchTex + "witchlow_CHECK_Normal.tga.png";
            var nti = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            if (nti != null && nti.textureType != TextureImporterType.NormalMap)
            {
                nti.textureType = TextureImporterType.NormalMap;
                nti.SaveAndReimport();
            }

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(WitchTex + "witchlow_CHECK_BaseColor.tga.png"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(WitchTex + "witchlow_CHECK_Emissive.tga.png"));
            mat.SetColor("_EmissionColor", Color.white);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetFloat("_Smoothness", 0.2f);
            mat = SaveMat(mat, "Witch");

            string ctrlPath = AssetDir + "/Animations/Witch.controller";
            AssetDatabase.DeleteAsset(ctrlPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm = controller.layers[0].stateMachine;
            var allClips = AssetDatabase.LoadAllAssetsAtPath(WitchFbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
            AnimationClip FindClip(string key) => allClips.FirstOrDefault(c => c.name.ToLowerInvariant().Contains(key));
            var idle = sm.AddState("Idle");
            idle.motion = FindClip("idle");
            sm.defaultState = idle;
            sm.AddState("Walk").motion = FindClip("walk");
            sm.AddState("Attack").motion = FindClip("attack");
            sm.AddState("Death").motion = FindClip("death");
            AssetDatabase.SaveAssets();

            // Puntos de aparición
            var spawns = new GameObject("Witch Spawns");
            spawns.transform.SetParent(parent, false);
            Vector3[] points = { new Vector3(3.6f, 0f, -0.8f), new Vector3(-6.3f, 0f, -0.4f), new Vector3(-3.0f, 0f, 0.35f) };
            var spawnList = new List<Transform>();
            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i];
                if (NavMesh.SamplePosition(p, out var hit, 1f, NavMesh.AllAreas)) p = hit.position;
                var sp = new GameObject("Spawn " + (i + 1));
                sp.transform.SetParent(spawns.transform, false);
                sp.transform.position = p;
                spawnList.Add(sp.transform);
            }

            var witch = new GameObject("Witch (Bruja)");
            witch.transform.SetParent(parent, false);
            witch.transform.position = spawnList[0].position;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WitchFbx), witch.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * 0.9f;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var rb = witch.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var capsule = witch.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.95f, 0f);
            capsule.radius = 0.38f;
            capsule.height = 1.9f;

            var agent = witch.AddComponent<NavMeshAgent>();
            agent.agentTypeID = 0;
            agent.radius = 0.3f;
            agent.height = 1.9f;
            agent.speed = 0.6f;
            agent.acceleration = 4f;
            agent.angularSpeed = 180f;
            agent.stoppingDistance = 0.8f;

            var ctrl = witch.AddComponent<WitchController>();
            ctrl.player = head;
            ctrl.animator = animator;
            ctrl.agent = agent;
            ctrl.bodyCollider = capsule;
            ctrl.spawnPoints = spawnList.ToArray();
            ctrl.voice = AddAudio(witch, 1f, 2f, 25f);
            ctrl.sfx = AddAudio(witch, 1f, 1.5f, 15f);
            ctrl.laughClip = Clip(ClueDir + "Witch_Laugh.mp3");
            ctrl.bookHitClip = Clip(Sfx + "SFXImpactBooks01.ogg");
            ctrl.attackClip = Clip(Sfx + "SFXWandShoot.ogg");
            ctrl.appearClip = Clip(Sfx + "SFXFireFlare.ogg");
            witch.SetActive(false);
            Note("Bruja: aparece al acabarse el tiempo, camina hacia el jugador (NavMesh), 2 ataques = derrota, 3 libros = muere.");
            return ctrl;
        }

        private static void TagThrowableBooks()
        {
            var physics = Req(FindGo("[PHYSICS AND TOUCH]"), "[PHYSICS AND TOUCH]");
            int count = 0;
            foreach (Transform child in physics.transform)
            {
                if (!child.name.Contains("Libro")) continue;
                if (child.GetComponent<ThrowableBook>() == null) child.gameObject.AddComponent<ThrowableBook>();
                // Cada libro necesita al menos un collider sólido (LibroRed solo tenía triggers y atravesaba el estante).
                var cols = child.GetComponentsInChildren<Collider>(true);
                if (cols.Length > 0 && cols.All(c => c.isTrigger))
                {
                    var main = cols.FirstOrDefault(c => c.name != "BOUND") ?? cols[0];
                    main.isTrigger = false;
                    Note($"{child.name}: su collider principal era trigger, ahora es sólido.");
                }
                var rb = child.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    rb.interpolation = RigidbodyInterpolation.None; // el SDK de Meta mueve el transform directamente
                }
                count++;
            }
            Note($"{count} libros marcados como lanzables (ThrowableBook).");
        }

        // =====================================================================
        // Helpers
        // =====================================================================
        private static void ConfigureGrabbable(Grabbable grab, Transform target, Rigidbody rb, Object transformer, bool throwOnRelease)
        {
            var so = new SerializedObject(grab);
            so.FindProperty("_targetTransform").objectReferenceValue = target;
            so.FindProperty("_rigidbody").objectReferenceValue = rb;
            so.FindProperty("_oneGrabTransformer").objectReferenceValue = transformer;
            so.FindProperty("_throwWhenUnselected").boolValue = throwOnRelease;
            so.FindProperty("_kinematicWhileSelected").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddHandGrab(GameObject host, Grabbable grab, Rigidbody rb)
        {
            var go = Object.Instantiate(_handGrabTemplate, host.transform);
            go.name = "HandGrab Interactable";
            ResetLocal(go.transform, host.transform);
            RemoveBuildingBlock(go);
            foreach (var mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb is HandGrabInteractable || mb is GrabInteractable)
                {
                    var so = new SerializedObject(mb);
                    so.FindProperty("_pointableElement").objectReferenceValue = grab;
                    so.FindProperty("_rigidbody").objectReferenceValue = rb;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static void AddTouch(GameObject host, Grabbable grab, Collider[] colliders, Collider bounds)
        {
            var go = Object.Instantiate(_touchTemplate, host.transform);
            go.name = "TouchHandGrab Interactable";
            ResetLocal(go.transform, host.transform);
            RemoveBuildingBlock(go);
            var touch = go.GetComponent<TouchHandGrabInteractable>();
            var so = new SerializedObject(touch);
            so.FindProperty("_pointableElement").objectReferenceValue = grab;
            so.FindProperty("_boundsCollider").objectReferenceValue = bounds;
            var list = so.FindProperty("_colliders");
            list.arraySize = colliders.Length;
            for (int i = 0; i < colliders.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = colliders[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static PokeInteractable InstantiatePoke(Transform parent, Vector3 scale)
        {
            var prefab = Req(AssetDatabase.LoadAssetAtPath<GameObject>(PokePrefab), PokePrefab);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = "PokeInteractable";
            go.transform.localPosition = new Vector3(0f, 0f, -0.002f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            var visuals = go.transform.Find("Visuals");
            if (visuals != null) visuals.gameObject.SetActive(false);
            var buttonVisual = go.transform.Find("ButtonVisual");
            if (buttonVisual != null) buttonVisual.gameObject.SetActive(false);
            return go.GetComponent<PokeInteractable>();
        }

        private static void RemoveBuildingBlock(GameObject go)
        {
            var bb = go.GetComponent("BuildingBlock");
            if (bb != null) Object.DestroyImmediate(bb);
        }

        private static void ResetLocal(Transform t, Transform parent)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        /// <summary>Crea un hijo con BoxCollider de tamaño en metros reales (independiente de la escala del padre).</summary>
        private static BoxCollider AddBox(Transform parent, string name, Vector3 localCenter, Quaternion localRot, Vector3 worldSize, bool trigger)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localRotation = localRot;
            Vector3 ps = parent.lossyScale;
            go.transform.localScale = new Vector3(1f / ps.x, 1f / ps.y, 1f / ps.z);
            var box = go.AddComponent<BoxCollider>();
            box.size = worldSize;
            box.isTrigger = trigger;
            return box;
        }

        private static AudioSource AddAudio(GameObject go, float spatial, float minDistance = 1f, float maxDistance = 15f, AudioClip clip = null, bool loop = false)
        {
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = spatial;
            a.minDistance = minDistance;
            a.maxDistance = maxDistance;
            a.dopplerLevel = 0f;
            a.clip = clip;
            a.loop = loop;
            return a;
        }

        private static Canvas MakeCanvas(Transform parent, string name, Vector2 size, float scale)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * scale;
            return canvas;
        }

        private static TextMeshProUGUI MakeText(Transform parent, string name, string text, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            Stretch(t.rectTransform);
            return t;
        }

        private static void Stretch(RectTransform rt) => SetAnchors(rt, Vector2.zero, Vector2.one);

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Material LitMat(string name, Color color, Color? emission = null)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.35f);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            return SaveMat(m, name);
        }

        private static Material SaveMat(Material m, string name)
        {
            string path = $"{AssetDir}/Materials/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.shader = m.shader;
                existing.CopyPropertiesFromMaterial(m);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static AudioClip Clip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) Note("AVISO: no se encontró el audio " + path);
            return clip;
        }

        private static void SetBool(Object target, string prop, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(prop).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static float NormalizeAngle(float a)
        {
            a %= 360f;
            return a > 180f ? a - 360f : a;
        }

        private static GameObject FindGo(string name, string parentName = null)
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != name) continue;
                    if (parentName != null && (t.parent == null || t.parent.name != parentName)) continue;
                    return t.gameObject;
                }
            }
            return null;
        }

        private static T Req<T>(T obj, string what) where T : Object
        {
            if (obj == null) throw new Exception("EscapeRoomSetup: no se encontró " + what);
            return obj;
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(parent).Replace('\\', '/'), Path.GetFileName(parent));
            }
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }

        private static void Note(string s) => Report.Add(s);
    }
}
