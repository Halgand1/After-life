using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AfterCity.EditorTools
{
    /// <summary>
    /// Import automatique des personnages et animaux deposes dans Assets/Art/Personnages/ :
    /// - squelette Humanoid pour les humains et les zombies (Generic pour les animaux) ;
    /// - un seul materiau URP Lit partage, base map = T_Palette_256 (filtre Point), un peu assombri pour l'ambiance ;
    /// - LODGroup construit a partir des suffixes _LOD0 / _LOD1 / _LOD2 ;
    /// - tous les clips en boucle (Loop Time), renommes sans le prefixe "Armature|".
    /// </summary>
    public sealed class CharacterImport : AssetPostprocessor
    {
        public const string Folder = "Assets/Art/Personnages/";
        public const string PaletteMaterialPath = Folder + "MAT_Personnages_Palette.mat";
        static readonly string[] Animals = { "SK_Cerf", "SK_Sanglier", "SK_Cheval", "SK_Vache" };

        /// <summary>Dossiers couverts : personnages, equipement, animaux (et leurs sous-dossiers).</summary>
        static readonly string[] Roots = { Folder, "Assets/Art/Equipement/", "Assets/Art/Animaux/", "Assets/Art/Decor/", "Assets/Art/Batiments/" };

        /// <summary>Change a chaque modification des reglages : force Unity a reimporter les modeles.</summary>
        public override uint GetVersion() => 3;

        static bool InFolder(string path)
        {
            path = path.Replace('\\', '/');
            return Roots.Any(r => path.StartsWith(r));
        }

        static bool IsStatic(string path) => Path.GetFileNameWithoutExtension(path).StartsWith("SM_");
        static bool IsAnimal(string path) => Animals.Any(a => Path.GetFileNameWithoutExtension(path).StartsWith(a));

        void OnPreprocessTexture()
        {
            if (!InFolder(assetPath)) return;
            var ti = (TextureImporter)assetImporter;
            if (Path.GetFileNameWithoutExtension(assetPath).StartsWith("T_Batiments"))
            {
                // Palette en haut a gauche + trim sheet : bilineaire, sans mipmaps (sinon les couleurs se melangent au loin)
                ti.filterMode = FilterMode.Bilinear;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.wrapMode = TextureWrapMode.Clamp;
                return;
            }
            if (!Path.GetFileNameWithoutExtension(assetPath).StartsWith("T_Palette")) return;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.wrapMode = TextureWrapMode.Clamp;
        }

        void OnPreprocessModel()
        {
            if (!InFolder(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            if (IsStatic(assetPath))
            {
                // Equipement (SM_Equip_*) : objet fixe accroche a un SOCKET_* du squelette
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
                mi.isReadable = true; // cuits dans le DecorKit (CombineMeshes)
            }
            else
            {
                mi.animationType = IsAnimal(assetPath) ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = true;
            }
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.globalScale = 1f;
            // Note : « Enforce T-Pose » n'a pas d'equivalent dans l'API ; les modeles doivent etre exportes en T-pose.
        }

        void OnPreprocessAnimation()
        {
            if (!InFolder(assetPath) || IsStatic(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.loopTime = true;
                c.loopPose = true;
                if (c.name.Contains("|")) c.name = c.name.Substring(c.name.LastIndexOf('|') + 1);
            }
            mi.clipAnimations = clips;
        }

        /// <summary>Tous les renderers du dossier utilisent le meme materiau a palette.</summary>
        /// <remarks>Le materiau est cree a l'etape de configuration (CreateAsset est interdit pendant un import).</remarks>
        Material OnAssignMaterialModel(Material material, Renderer renderer)
        {
            if (!InFolder(assetPath)) return null;
            return AssetDatabase.LoadAssetAtPath<Material>(PaletteMaterialPath);
        }

        /// <summary>
        /// Cree (ou complete) le materiau a palette, puis reimporte les modeles du dossier pour qu'ils l'utilisent.
        /// Appele par Phase0Builder.SetupProject, hors import.
        /// </summary>
        public static void PrepareFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/'))) return;
            var mat = EnsurePaletteMaterial();
            var palette = AssetDatabase.FindAssets("T_Palette_256 t:Texture2D", new[] { Folder.TrimEnd('/') })
                .Select(g => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
            if (mat != null && palette != null && mat.GetTexture("_BaseMap") != palette)
            {
                mat.SetTexture("_BaseMap", palette);
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssets();
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Folder.TrimEnd('/') }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        }

        public static Material EnsurePaletteMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PaletteMaterialPath);
            if (mat != null) return mat;
            var shader = Shader.Find("AfterCity/CharacterPalette") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;
            Directory.CreateDirectory(Folder);
            mat = new Material(shader) { name = "MAT_Personnages_Palette" };
            var palette = AssetDatabase.FindAssets("T_Palette_256 t:Texture2D", new[] { Folder.TrimEnd('/') })
                .Select(g => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
            if (palette != null) mat.SetTexture("_BaseMap", palette);
            // Un peu plus sombre et mat : ambiance post-apocalyptique
            mat.SetColor("_BaseColor", new Color(0.82f, 0.82f, 0.80f));
            mat.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(mat, PaletteMaterialPath);
            return mat;
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!InFolder(assetPath)) return;
            // Meme les maillages sans materiau recoivent la palette
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PaletteMaterialPath);
            if (mat != null)
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
            BuildLods(root);
        }

        /// <summary>Regroupe les maillages _LOD0 / _LOD1 / _LOD2 dans un LODGroup.</summary>
        static void BuildLods(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var lods = new[] { "_LOD0", "_LOD1", "_LOD2" }
                .Select(s => renderers.Where(r => r.name.EndsWith(s)).ToArray())
                .Where(a => a.Length > 0).ToArray();
            if (lods.Length < 2) return;
            var group = root.GetComponent<LODGroup>();
            if (group == null) group = root.AddComponent<LODGroup>();
            float[] heights = { 0.45f, 0.18f, 0.04f };
            group.SetLODs(lods.Select((r, i) => new LOD(heights[Mathf.Min(i, heights.Length - 1)], r)).ToArray());
            group.RecalculateBounds();
        }
    }
}
