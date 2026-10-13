# Scripts Blender : personnages, animaux et bâtiments

`generer_personnages.py` construit les 7 survivants (un par rôle, ère 1, pré‑équipés) et les 5 zombies des fiches 01 et 02, avec le squelette commun, les LOD, l'équipement et des animations de base.

`generer_animaux.py` construit le cerf, le sanglier, le cheval et la vache de la fiche 04. Il réutilise la palette et le constructeur de `generer_personnages.py` : garder les deux fichiers dans le même dossier. Testé avec Blender 5.2 LTS (module `bpy`) ; doit marcher à partir de Blender 4.2.

`generer_batiments.py` construit les bâtiments du palier 1 de la fiche 03 (ferme en état Ruine et Fini, grange, silo, hangar, poulailler, tour de guet, tas de stockage, tente, cabane de bûcheron, puits, potager, fosse à incinérer, enclos à bétail et à chevaux) et les kits modulaires (palissade, clôtures). Il produit aussi **la texture canonique `T_Batiments_1024.png`**, partagée avec le décor nature et ruines (voir palette.md). Même dossier que `generer_personnages.py`.

## Lancer
**Sans ouvrir Blender** (dans une invite de commandes) :
```
blender --background --python generer_personnages.py -- --sortie C:\Users\Utilisateur\Documents\AfterCity\Art\Personnages
blender --background --python generer_animaux.py -- --sortie C:\Users\Utilisateur\Documents\AfterCity\Art\Animaux
blender --background --python generer_batiments.py -- --sortie C:\Users\Utilisateur\Documents\AfterCity\Art\Batiments
```
Ajouter `--sans-rendu` pour sauter les images d'aperçu (le script prend alors 2 secondes au lieu de 2 à 3 minutes).

**Dans Blender** : onglet *Scripting*, ouvrir le fichier, *Run Script*. Les personnages apparaissent dans la scène, sans export.

## Ce qui sort
| Fichier | Contenu |
|---|---|
| `SK_Survivant_<Rôle>.fbx` | squelette `RIG_Humain` (25 os), maillages `_LOD0/_LOD1/_LOD2`, équipement `SM_Equip_*` accroché aux 6 emplacements, 9 animations |
| `SK_Cerf.fbx`, `SK_Sanglier.fbx`, `SK_Cheval.fbx`, `SK_Vache.fbx` | squelette `RIG_Quadrupede` (type *Generic* dans Unity), LOD, 4 animations |
| `SK_<Zombie>.fbx` | idem, 3 ou 4 animations de zombie |
| `T_Palette_256.png` | la palette (palette.md) |
| `AfterCity_Personnages.blend` | fichier de travail, pour retoucher à la main |
| `apercu_*.png` | rendus de contrôle |
| `SM_<Bâtiment>_<État>.fbx`, `SM_Palissade_*`, `SM_Cloture_*` | objet vide racine, maillages `_LOD0/_LOD1/_LOD2`, accroches `SOCKET_Porte`, `_Travail_NN`, `_Stock_NN`, `_Fumee`, `_Lumiere`, `_Garde`, `_Animal_NN` (objets vides) |
| `T_Batiments_1024.png` | texture canonique des bâtiments et du décor (palette dans le quart haut‑gauche) |
| `bilan_batiments.txt` | emprise, hauteur, triangles par LOD et budget, accroches |
| `bilan.txt`, `bilan_animaux.txt` | hauteur, triangles par LOD, triangles d'équipement et animations |

## Dans Unity
1. Glisser les FBX et la palette dans `Game/Assets/Art/Personnages/`.
2. Sur chaque FBX : *Rig > Animation Type = Humanoid*, puis *Configure > Pose > Enforce T‑Pose* (le modèle est construit bras le long du corps), *Apply*.
3. Matériau : URP Lit avec `T_Palette_256` en *Base Map*, filtre *Point* sur la texture (cases nettes). Pour retrouver l'aspect des aperçus, le shader doit multiplier la couleur par l'occlusion (canal R des couleurs de sommet) et la crasse (canal G) ; voir `creer_materiau` dans le script.
4. Équipement : les objets `SM_Equip_*` sont enfants des os `SOCKET_*`. Pour changer un objet, le jeu remplace l'enfant de l'accroche.
5. Les groupes de LOD sont créés automatiquement grâce aux suffixes `_LOD0/1/2`.
6. Bâtiments : matériau `MAT_Batiments` = URP Lit avec `T_Batiments_1024` (filtre *Point*, sans compression). Canal B des couleurs de sommet = fenêtres et braises à rendre émissives la nuit. Les clôtures des enclos ne sont pas dans le modèle de l'enclos : le jeu les pose case par case avec `SM_Cloture_*`, comme la palissade.
7. Animations : chaque prise porte son nom (`ANIM_Humain_Marche`, `ANIM_Zombie_Repos`…), à cocher *Loop Time* pour les boucles.

## Limites connues (à reprendre à la main ou dans une version suivante)
- Les animations sont **procédurales** et simples : elles servent à tester le pipeline et la lisibilité, pas à la finition.
- Chaque pièce est pondérée à 100 % sur un seul os : rendu net façon K&C, mais les coudes et genoux se plient sans transition.
- Les variantes d'ère 2 et 3, les coiffures supplémentaires et les 100 combinaisons du Rôdeur ne sont pas encore générées.
- L'occlusion des couleurs de sommet (canal R) est une approximation par la hauteur, pas un vrai calcul.

Pour ajouter un rôle ou un zombie : copier une entrée de `SURVIVANTS` ou `ZOMBIES` et changer les proportions, les couleurs (noms de palette.md) et les accessoires.
