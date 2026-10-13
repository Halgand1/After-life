# Décor urbain abandonné (ruines) · After City

29 objets fixes prêts pour Unity, générés par `../scripts/generer_decor_ruines.py` (Blender 4.2+ ou module `bpy`).
Régénérer : `blender --background --python ../scripts/generer_decor_ruines.py -- --sortie .` (ajouter `--sans-rendu` pour sauter l'aperçu).

## Contenu
- `SM_*.fbx` : un FBX par objet, avec `_LOD0`, `_LOD1`, `_LOD2` et les accroches `SOCKET_*` (empties parentés au LOD0).
- Texture : **la texture canonique** `modeles3d/sorties/T_Batiments_1024.png` (fil 3D), sur le matériau `MAT_Batiments`. Aucune copie n'est livrée ici. Filtrage Point, compression désactivée.
- `bilan.txt` : triangles par LOD, emprise en cases, hauteur, accroches de chaque objet.
- `apercu_decor_ruines.png` : planche de contrôle (rendu Cycles).
- `AfterCity_Decor_Ruines.blend` : scène source.

## Conventions suivies (modeles3d/CONVENTIONS.md)
1 unité = 1 m · 1 case = 2 m · 1 niveau = 0,5 m · survivant 1,00 m, étage 1,60 m, porte 1,30 × 0,80 m · l'objet regarde vers −Y dans Blender · origine au centre de l'emprise au sol · export Forward −Z / Up Y, Apply Unit, FBX_SCALE_UNITS · faces plates.
UV de chaque face au centre d'une case : u = (col + 0,5)/16 × 0,25, v = 1 − (ligne + 0,5)/16 × 0,25 (palette dans le quart haut‑gauche de la 1024).
Couleur de sommet `Col` : R = occlusion approchée par la hauteur, G = saleté/rouille, B = 0 (pas de fenêtres éclairées : maisons abandonnées).
LOD : pas de décimation ; chaque pièce porte un niveau (grands volumes dans les 3 LOD, détails retirés en LOD1 puis LOD2), donc les silhouettes restent nettes.

## Familles
| Famille | Objets | Emprise | Accroches |
|---|---|---|---|
| Véhicules | Berline A (capot ouvert), Berline B (portière arrachée), Berline brûlée, Pick‑up, Camionnette | 1 × 2 | `SOCKET_Travail_01/02` (fouille, démontage), `SOCKET_Fumee` sur les berlines |
| Débris | Gravats brique, Gravats béton, Pneus, Bidons, Poubelles, Palettes, Tôles | 1 × 1 | `SOCKET_Travail_01` sur les gravats (ordre Nettoyer) |
| Maisons | Pavillon (2 × 2, toit percé), Maison à étage effondrée (3 × 2), Cabane de jardin (1 × 1) | voir colonne | `SOCKET_Porte`, `SOCKET_Travail_01/02` |
| Clôtures | Bois, Bois cassée, Grillage, Grillage cassé (segment d'une case le long de X) | 1 × 1 | — |
| Barricades | Planches, Sacs de sable, Cheval de frise, Palettes | 1 × 1 | — |
| Panneaux | Stop, Limite, Direction (2 × 1), « AIDE » peint, Lampadaire cassé, Poteau électrique | 1 × 1 | — |

Budgets : véhicules 290–390 triangles, débris 90–430, petit bâtiment 180, pavillon 1 036, maison à étage 1 588, clôtures 108–180, barricades 204–288 (sous les 250 d'un segment de palissade sauf palettes et planches, à 264–288).

## Palette
Cases prises dans `palette.md` (fusion du fil 3D) : ligne 7 colonnes 0–15 pour le décor urbain (peintures de voiture, pneu, chrome, panneaux, contreplaqué, grillage, conteneur, bidon), ligne 9 colonnes 12–15 (intérieur sombre, mousse, herbe sèche, bois peint vert), lignes 8 et 9 pour les bâtiments (assombries pour l'ambiance demandée par Sam).

## Import Unity
Importer le dossier, assigner la texture canonique `T_Batiments_1024` (Point, sRGB) au matériau `MAT_Batiments` (URP Lit, couleur de base = texture × vertex color si le shader la lit). Créer un LOD Group avec les trois maillages (60 % / 25 % / 8 %). Les `SOCKET_*` servent de points de travail pour les ordres Fouiller, Démonter et Nettoyer.
