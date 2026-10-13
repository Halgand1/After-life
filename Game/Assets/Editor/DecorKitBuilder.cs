using System.Collections.Generic;
using System.IO;
using System.Linq;
using AfterCity.Game.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AfterCity.EditorTools
{
    /// <summary>
    /// Cuit les modeles fixes de l'equipe 3D (Decor/Nature, Decor/Ruines, Batiments) en un DecorKit :
    /// un maillage LOD0 par modele, pose a l'origine de son emprise, avec uv palette et couleurs de sommet.
    /// </summary>
    public static class DecorKitBuilder
    {
        const string KitPath = "Assets/Art/Baked/DecorKit.asset";
        const string MatPath = "Assets/Art/Baked/MAT_Decor_Palette.mat";
        static readonly string[] Folders = { "Assets/Art/Decor/Nature", "Assets/Art/Decor/Ruines", "Assets/Art/Batiments" };

        public static string KeyOf(string fbxPath)
        {
            var n = Path.GetFileNameWithoutExtension(fbxPath);
            if (n.StartsWith("SM_Nature_")) return n.Substring(10);
            return n.StartsWith("SM_") ? n.Substring(3) : n;
        }

        public static DecorKit Build()
        {
            var folders = Folders.Where(AssetDatabase.IsValidFolder).ToArray();
            if (folders.Length == 0) return null;
            Directory.CreateDirectory("Assets/Art/Baked");
            AssetDatabase.DeleteAsset(KitPath);
            var kit = ScriptableObject.CreateInstance<DecorKit>();
            AssetDatabase.CreateAsset(kit, KitPath);

            var names = new List<string>();
            var meshes = new List<Mesh>();
            foreach (var path in AssetDatabase.FindAssets("t:Model", folders).Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                var cis = new List<CombineInstance>();
                var rootPos = go.transform.position;
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null || mf.name.EndsWith("_LOD1") || mf.name.EndsWith("_LOD2")) continue;
                    var m = Matrix4x4.Translate(-rootPos) * mf.transform.localToWorldMatrix;
                    for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                        cis.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = m });
                }
                if (cis.Count == 0) continue;
                var mesh = new Mesh { name = "KIT_" + KeyOf(path), indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(cis.ToArray(), true, true);
                if (mesh.vertexCount == 0) continue;
                AssetDatabase.AddObjectToAsset(mesh, kit);
                names.Add(KeyOf(path));
                meshes.Add(mesh);
            }
            kit.names = names.ToArray();
            kit.meshes = meshes.ToArray();
            kit.material = EnsureMaterial();
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            var sizes = string.Join(", ", names.Zip(meshes, (n, m) => $"{n} {m.bounds.size.x:0.0}x{m.bounds.size.y:0.0}x{m.bounds.size.z:0.0}"));
            Debug.Log($"[AfterCity] DecorKit : {names.Count} modeles : {sizes}");
            return kit;
        }

        static Material EnsureMaterial()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Batiments/T_Batiments_1024.png");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                var shader = Shader.Find("AfterCity/CharacterPalette") ?? Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader) { name = "MAT_Decor_Palette" };
                AssetDatabase.CreateAsset(mat, MatPath);
            }
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", new Color(0.86f, 0.86f, 0.84f));
            mat.SetFloat("_DirtStrength", 0.5f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            Debug.Log($"[AfterCity] MAT_Decor_Palette : texture {(tex != null ? tex.name : "ABSENTE")}");
            return mat;
        }
    }
}
