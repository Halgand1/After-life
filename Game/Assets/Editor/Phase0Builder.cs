using System;
using System.IO;
using System.Linq;
using AfterCity.Game;
using AfterCity.Game.Phase0;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using FreeCamera = AfterCity.Game.Phase0.FreeCamera;

namespace AfterCity.EditorTools
{
    /// <summary>
    /// Outils de la phase 0, lances en ligne de commande :
    ///   Unity.exe -batchmode -quit -projectPath Game -executeMethod AfterCity.EditorTools.Phase0Builder.SetupProject
    ///   Unity.exe -batchmode -quit -projectPath Game -executeMethod AfterCity.EditorTools.Phase0Builder.BuildScene
    ///   Unity.exe -batchmode -quit -projectPath Game -executeMethod AfterCity.EditorTools.Phase0Builder.BuildPlayer
    /// Aussi disponibles dans le menu After City de l'editeur.
    /// </summary>
    public static class Phase0Builder
    {
        const string SettingsDir = "Assets/Settings";
        const string PipelinePath = SettingsDir + "/URP_AfterCity.asset";
        const string RendererPath = SettingsDir + "/URP_AfterCity_Renderer.asset";
        const string ScenePath = "Assets/Scenes/Phase0.unity";
        const string TerrainMatPath = "Assets/Materials/MAT_Terrain.mat";
        const string PostProfilePath = SettingsDir + "/PP_AfterCity.asset";

        [MenuItem("After City/Phase 0/1. Configurer le projet (URP)")]
        public static void SetupProject()
        {
            Directory.CreateDirectory(SettingsDir);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            pipeline.shadowDistance = 160f;
            pipeline.shadowCascadeCount = 2;
            pipeline.msaaSampleCount = 4;
            pipeline.supportsHDR = true;
            EditorUtility.SetDirty(pipeline);
            EnsureSsao(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath));

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);

            PlayerSettings.companyName = "Sam";
            PlayerSettings.productName = "After City";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            SetInputHandlerBoth();
            CharacterImport.PrepareFolder();

            AssetDatabase.SaveAssets();
            Debug.Log("[AfterCity] Projet configure : URP " + PipelinePath);
        }

        /// <summary>Ajoute l'occlusion ambiante (SSAO) au renderer URP, qualite moyenne, sans « After Opaque ».</summary>
        static ScriptableRendererFeature EnsureSsao(UniversalRendererData renderer)
        {
            if (renderer == null) { Debug.LogWarning("[AfterCity] Renderer URP introuvable, pas de SSAO"); return null; }
            ScriptableRendererFeature feature = renderer.rendererFeatures.FirstOrDefault(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion");
            if (feature == null)
            {
                var type = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
                feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
                feature.name = "SSAO";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
                AssetDatabase.SaveAssets();
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
                var rso = new SerializedObject(renderer);
                var map = rso.FindProperty("m_RendererFeatureMap");
                if (map != null)
                {
                    map.InsertArrayElementAtIndex(map.arraySize);
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                    rso.ApplyModifiedProperties();
                }
            }
            var so = new SerializedObject(feature);
            SetProp(so, "m_Settings.AfterOpaque", p => p.boolValue = false);
            SetProp(so, "m_Settings.Downsample", p => p.boolValue = false);
            SetProp(so, "m_Settings.Intensity", p => p.floatValue = 0.9f);
            SetProp(so, "m_Settings.DirectLightingStrength", p => p.floatValue = 0.25f);
            SetProp(so, "m_Settings.Radius", p => p.floatValue = 0.6f);
            SetProp(so, "m_Settings.Samples", p => p.enumValueIndex = 1); // Medium
            so.ApplyModifiedProperties();
            feature.SetActive(true);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(feature);
            AssetDatabase.SaveAssets();
            Debug.Log("[AfterCity] SSAO actif sur " + RendererPath);
            return feature;
        }

        static void SetProp(SerializedObject so, string path, Action<SerializedProperty> set)
        {
            var p = so.FindProperty(path);
            if (p == null) { Debug.LogWarning("[AfterCity] Reglage SSAO absent : " + path); return; }
            set(p);
        }

        /// <summary>Profil de post-traitement : ACES, couleurs chaudes un peu desaturees, ombres froides, bloom, vignette, grain.</summary>
        static VolumeProfile CreatePostProfile()
        {
            AssetDatabase.DeleteAsset(PostProfilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostProfilePath);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.25f);
            ca.contrast.Override(14f);
            ca.saturation.Override(-40f);
            ca.colorFilter.Override(new Color(0.90f, 0.94f, 0.93f)); // gris-vert apocalyptique
            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.90f, 0.96f, 1.10f, 0f));
            smh.highlights.Override(new Vector4(1.05f, 1.0f, 0.93f, 0f));
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.0f);
            bloom.scatter.Override(0.6f);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.38f);
            vig.smoothness.Override(0.45f);
            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.12f);
            grain.response.Override(0.8f);

            foreach (var comp in profile.components) AssetDatabase.AddObjectToAsset(comp, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>Ancien Input Manager + Input System (0 = ancien, 1 = nouveau, 2 = les deux).</summary>
        static void SetInputHandlerBoth()
        {
            var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (ps == null) return;
            var so = new SerializedObject(ps);
            var prop = so.FindProperty("activeInputHandler");
            if (prop != null && prop.intValue == 1)
            {
                prop.intValue = 2;
                so.ApplyModifiedProperties();
            }
        }

        [MenuItem("After City/Phase 0/2. Construire la scene Phase0")]
        public static void BuildScene()
        {
            var terrainMat = EnsureTerrainMaterial();
            var villager = EnsureCharacter("Villager");
            var zombie = EnsureCharacter("Zombie");
            var survivorModels = EnsurePersonnages(SurvivorModels, "Humain_", "AC_Humain");
            var zombieModels = EnsurePersonnages(ZombieModels, "Zombie_", "AC_Zombie");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ambiance : ciel voile, brume legere
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.66f, 0.70f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.52f, 0.47f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.27f, 0.23f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.68f, 0.67f, 0.62f);
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 280f;

            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.93f, 0.80f);
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = light;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.68f, 0.67f, 0.62f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;
            cam.fieldOfView = 40f;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;

            var postGo = new GameObject("PostFX");
            var volume = postGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = CreatePostProfile();
            var toggle = camGo.AddComponent<PostFxToggle>();
            toggle.volume = volume;
            toggle.cameraData = camData;
            toggle.ssao = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath)?.rendererFeatures
                .FirstOrDefault(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion");
            var freeCam = camGo.AddComponent<FreeCamera>();
            freeCam.boundsMax = new Vector2(128 * 2f, 128 * 2f);

            var world = new GameObject("World");
            var boot = world.AddComponent<GameRoot>();
            boot.width = 128;
            boot.height = 128;
            boot.seed = 1337;
            boot.terrainMaterial = terrainMat;
            boot.villagerPrefab = villager;
            boot.zombiePrefab = zombie;
            boot.survivorPrefabs = survivorModels;
            boot.zombiePrefabs = zombieModels;
            boot.freeCamera = freeCam;
            boot.decorKit = DecorKitBuilder.Build();
            boot.peuplement = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Art/Decor/peuplement.json");
            // Logements et objets d'etagere : instancies en jeu (toit amovible, etagere garnie)
            boot.artPrefabs = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art/Batiments/Logements" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(g => g != null).ToArray();
            Debug.Log($"[AfterCity] Logements : {boot.artPrefabs.Length} modeles ({string.Join(", ", boot.artPrefabs.Where(g => g.name.StartsWith("SM_Logement")).Select(g => g.name))})");
            Debug.Log("[AfterCity] Peuplement : " + (boot.peuplement != null ? "peuplement.json" : "ABSENT, placement code en dur"));
            boot.animalPrefabs = EnsureAnimals("SK_Cerf", "SK_Sanglier");
            boot.chickenPrefabs = EnsureAnimals("SK_Poule_Blanche", "SK_Poule_Rousse", "SK_Poule_Noire");
            boot.smokeMaterial = EnsureParticleMaterial("Assets/Materials/MAT_Fumee.mat");
            boot.mistMaterial = EnsureParticleMaterial("Assets/Materials/MAT_Brume.mat");
            boot.sun = light;

            var bench = new GameObject("Benchmark").AddComponent<Phase0Benchmark>();
            bench.freeCamera = freeCam;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[AfterCity] Scene sauvee : " + ScenePath);
        }

        static Material EnsureTerrainMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(TerrainMatPath);
            if (mat != null) return mat;
            var shader = Shader.Find("AfterCity/VertexColorLit");
            if (shader == null) throw new Exception("Shader AfterCity/VertexColorLit introuvable");
            Directory.CreateDirectory(Path.GetDirectoryName(TerrainMatPath));
            mat = new Material(shader) { name = "MAT_Terrain" };
            AssetDatabase.CreateAsset(mat, TerrainMatPath);
            return mat;
        }

        /// <summary>Importe SK_{name}_Test.fbx en Generic avec clips en boucle, cree son Animator et un prefab PF_{name}_Test.</summary>
        static GameObject EnsureCharacter(string name)
        {
            string VillagerFbx = $"Assets/Art/Characters/SK_{name}_Test.fbx";
            string VillagerController = $"Assets/Art/Characters/AC_{name}_Test.controller";
            var importer = AssetImporter.GetAtPath(VillagerFbx) as ModelImporter;
            if (importer == null) throw new Exception("FBX introuvable : " + VillagerFbx);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            var clips = importer.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.loopTime = true;
                c.name = c.takeName.Contains("|") ? c.takeName.Substring(c.takeName.LastIndexOf('|') + 1) : c.takeName;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            var all = AssetDatabase.LoadAllAssetsAtPath(VillagerFbx).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToArray();
            Debug.Log($"[AfterCity] Clips de {name} : " + string.Join(", ", all.Select(c => c.name)));
            var walk = all.FirstOrDefault(c => c.name.Contains("Walk")) ?? all.FirstOrDefault();

            AssetDatabase.DeleteAsset(VillagerController);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(VillagerController);
            var sm = controller.layers[0].stateMachine;
            foreach (var clip in all.OrderBy(c => c == walk ? 0 : 1))
            {
                var state = sm.AddState(clip.name);
                state.motion = clip;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(VillagerFbx);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            string prefabPath = $"Assets/Art/Characters/PF_{name}_Test.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        /// <summary>Modeles attendus dans Assets/Art/Personnages (equipe 3D). Ordre = role des survivants.</summary>
        static readonly string[] SurvivorModels =
            { "SK_Survivant_Ouvrier", "SK_Survivant_Carrier", "SK_Survivant_Milicien", "SK_Survivant_Pisteur", "SK_Survivant_Medecin", "SK_Survivant_Nettoyeur", "SK_Survivant_Militaire" };
        static readonly string[] ZombieModels = { "SK_Rodeur", "SK_Coureur", "SK_Cogneur", "SK_Grimpeur", "SK_Brute" };

        /// <summary>
        /// Construit un controleur commun (un etat par clip « prefixe + Etat » trouve dans le dossier, ex. ANIM_Humain_Marche -> Marche)
        /// et un prefab par modele present. Liste vide si aucun modele n'est encore arrive : le jeu garde les modeles de test.
        /// </summary>
        /// <summary>« ANIM_Humain_Marche » ou « Humain_Marche » -> « Marche » ; null si le clip n'a pas ce prefixe.</summary>
        static string StateName(string clip, string prefix)
        {
            if (clip.StartsWith("ANIM_")) clip = clip.Substring(5);
            return clip.StartsWith(prefix) && clip.Length > prefix.Length ? clip.Substring(prefix.Length) : null;
        }

        static GameObject[] EnsurePersonnages(string[] models, string clipPrefix, string controllerName)
        {
            string folder = CharacterImport.Folder;
            if (!AssetDatabase.IsValidFolder(folder.TrimEnd('/'))) return new GameObject[0];
            var fbxs = AssetDatabase.FindAssets("t:Model", new[] { folder.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var clips = fbxs.SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>())
                .Where(c => !c.name.StartsWith("__preview__") && StateName(c.name, clipPrefix) != null)
                .GroupBy(c => StateName(c.name, clipPrefix)).Select(g => g.First()).ToArray();
            var present = models.Where(m => AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}{m}.fbx") != null).ToArray();
            Debug.Log($"[AfterCity] {controllerName} : {present.Length} modele(s), clips {string.Join(", ", clips.Select(c => c.name))}");
            foreach (var m in present)
            {
                var av = AssetDatabase.LoadAllAssetsAtPath($"{folder}{m}.fbx").OfType<Avatar>().FirstOrDefault();
                var rend = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}{m}.fbx").GetComponentInChildren<Renderer>();
                Debug.Log($"[AfterCity]   {m} : avatar {(av == null ? "absent" : $"valide={av.isValid} humanoide={av.isHuman}")}, materiau {(rend != null && rend.sharedMaterial != null ? rend.sharedMaterial.name : "aucun")}");
            }
            if (present.Length == 0) return new GameObject[0];

            string ctrlPath = $"{folder}{controllerName}.controller";
            AssetDatabase.DeleteAsset(ctrlPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm = controller.layers[0].stateMachine;
            foreach (var clip in clips.OrderBy(c => c.name.EndsWith("Repos") ? 0 : 1))
            {
                var state = sm.AddState(StateName(clip.name, clipPrefix));
                state.motion = clip;
            }

            Directory.CreateDirectory(folder + "Prefabs");
            var prefabs = new System.Collections.Generic.List<GameObject>();
            foreach (var m in present)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}{m}.fbx");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                prefabs.Add(PrefabUtility.SaveAsPrefabAsset(instance, $"{folder}Prefabs/PF_{m}.prefab"));
                UnityEngine.Object.DestroyImmediate(instance);
            }
            return prefabs.ToArray();
        }

        /// <summary>Un controleur par animal (squelettes differents) avec ses propres clips Animal_*, et un prefab PF_SK_x.</summary>
        static GameObject[] EnsureAnimals(params string[] models)
        {
            const string folder = "Assets/Art/Animaux/";
            var prefabs = new System.Collections.Generic.List<GameObject>();
            Directory.CreateDirectory(folder + "Prefabs");
            foreach (var m in models)
            {
                var fbx = $"{folder}{m}.fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                if (model == null) continue;
                var clips = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview__") && AnimalState(c.name) != null).ToArray();
                Debug.Log($"[AfterCity] {m} : clips {string.Join(", ", clips.Select(c => c.name))}");
                string ctrlPath = $"{folder}AC_{m}.controller";
                AssetDatabase.DeleteAsset(ctrlPath);
                var controller = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
                var sm = controller.layers[0].stateMachine;
                foreach (var clip in clips.OrderBy(c => c.name.EndsWith("Repos") ? 0 : 1))
                    sm.AddState(AnimalState(clip.name)).motion = clip;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullCompletely;
                prefabs.Add(PrefabUtility.SaveAsPrefabAsset(instance, $"{folder}Prefabs/PF_{m}.prefab"));
                UnityEngine.Object.DestroyImmediate(instance);
            }
            return prefabs.ToArray();
        }

        /// <summary>Materiau de particules URP transparent (fumee, brume), texture douce par defaut d'Unity.</summary>
        static Material EnsureParticleMaterial(string path)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) { Debug.LogWarning("[AfterCity] Shader de particules URP introuvable"); return null; }
            AssetDatabase.DeleteAsset(path);
            var mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(mat, path);
            Debug.Log($"[AfterCity] {mat.name} : texture {(tex != null ? tex.name : "aucune")}");
            return mat;
        }

        /// <summary>« ANIM_Animal_Marche » ou « Poule_Marche » -> « Marche ».</summary>
        static string AnimalState(string clip) => StateName(clip, "Animal_") ?? StateName(clip, "Poule_");

        [MenuItem("After City/Phase 0/3. Build Windows (Builds/Phase0)")]
        public static void BuildPlayer()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Builds", "Phase0"));
            Directory.CreateDirectory(outDir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outDir, "AfterCity.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[AfterCity] Build {s.result} : {s.outputPath}, {s.totalErrors} erreurs, {s.totalSize / (1024 * 1024)} Mo, {s.totalTime}");
            if (Application.isBatchMode)
                EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
