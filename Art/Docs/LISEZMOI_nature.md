# Décor nature (SM_Nature_*) — v0.2

Généré par `decor/scripts/generer_nature.py` (Blender 5.2, module bpy). 51 modèles fixes, chacun en LOD0/1/2, style bas‑poly à facettes. Conventions de [CONVENTIONS.md](../../modeles3d/CONVENTIONS.md) : 1 unité = 1 m, 1 case = 2 m, origine au centre de l'emprise au sol, export Forward −Z / Up Y.

| Catégorie | Modèles | Triangles LOD0 |
|---|---|---|
| Arbres | Pin_A/B/C, Feuillu_A/B/C_Ete, Bouleau_A/B, Arbre_Mort_A/B | 124 à 192 |
| Saisons | Feuillu_A/B/C_Automne, Feuillu_A/B_Hiver (nus), Pin_Neige_A/B | 152 à 192 |
| Forêt dense | Foret_Dense_A (mixte), _B (feuillus), _C (pins) : 8 à 10 arbres serrés avec sous‑bois et tronc couché, sur 2 × 2 cases | 1 750 à 2 060 |
| Cueillette | Buisson_Baies_Rouges, Buisson_Baies_Bleues, Buisson_Baies_Vide (après récolte), Champignons | 80 à 248 |
| Pierre | Rocher_A/B/C, Rocher_Mousse, Rocher_Ocre (à tailler) ; Cailloux_A/B (à ramasser) | 80 à 220 |
| Sol | Buisson_A/B, Herbe_A/B, Herbe_Haute, Fleurs, Fougere | 14 à 154 |
| Bois coupé | Souche_A/B (après abattage), Tronc_Couche | 80 à 128 |
| Détails au sol (v0.2) | Tapis_Feuilles_A/B/Automne, Branches_Mortes_A/B, Flaque_A/B, Boue_A/B, Ossements : plats (1 à 12 cm), emprise 0, ne bloquent rien | 26 à 198 |

`catalogue_nature.json` donne pour chaque modèle sa catégorie, son emprise en cases, la ressource qu'il donne (Wood, Stone, Vegetables, cf. `equilibrage/`), sa hauteur et ses triangles. Aperçus : `apercu_nature.png` (tous les modèles), `apercu_lisiere.png` (lisière de forêt vue par la caméra du jeu).

## Matériau
`MAT_Batiments` sur la texture de référence `modeles3d/sorties/T_Batiments_1024.png` (CONVENTIONS.md, aucune copie ici) : la palette commune est en haut à gauche (256 px) ; les couleurs de la nature y occupent les **lignes 10 à 15** (palette.md). Flaques, boue et ossements réutilisent des cases déjà présentes de la palette commune (8,1 · 8,14 · 9,4 · 9,7). Couleur de sommet `Col` : R occlusion, G saleté, B = 0.

## Dans Unity
Importer les FBX ; groupes de LOD automatiques grâce aux suffixes. Activer *GPU Instancing* sur le matériau : arbres, herbes et rochers sont faits pour être posés par centaines. Varier rotation (0–360°) et échelle (0,85–1,15) à la pose. Pour la forêt dense, poser un `Foret_Dense_*` tous les 2 × 2 cases et compléter la lisière avec des arbres isolés.

## Où poser quoi
Voir `decor/peuplement.json` (zones, densités par case, ressources des modèles, cerfs et sangliers) et `decor/PEUPLEMENT.md`.
