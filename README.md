# After City (titre provisoire)

Jeu de gestion et de construction de colonie, survie post‑apocalyptique zombie, inspiré de *Kingdoms and Castles*.
Conception : voir `/mnt/project-files/conception/` (document de conception, cahier des charges, planches).

## Organisation du dépôt

| Dossier | Contenu |
|---|---|
| `Core/` | **Le moteur After City** : toute la logique du jeu en C# pur (monde, terraformage, simulation, agents). Paquet Unity local, sans dépendance à Unity. |
| `Tests/` | Tests automatisés du moteur (NUnit), lancés hors d'Unity avec `dotnet test`. |
| `Game/` | Le projet Unity 6 (URP) : affichage, interface, sons. Créé en phase 0. |
| `Art/` | Fichiers Blender sources (à créer en phase 0). |
| `Tools/` | Outils de build et scripts. |

## Ce qui existe

- `Core/` (moteur, C# pur, testé) : carte générée par graine (relief, biomes, rivière, ferme), décor récoltable (arbres, rochers, épaves), terraformage, survivants avec recherche de chemin A*, ordres (couper, tailler, démonter, creuser, remblayer, barricader, tracer des chemins de terre plus rapides), construction avec arbre de progression (abri > poste d'expédition > atelier > tour de guet ; champ libre), technologie Asphalte (recherche à l'atelier, routes x2), nourriture (récolte à 18 h, repas à l'aube, famine), démontage des épaves et atelier (pièces tirées selon leur rareté : métal, caoutchouc, plastique, céramique, acier, électronique), expéditions en ville (réussite 50 à 95 % selon l'équipe et le danger, butin et blessures selon le résultat), cycle jour/nuit, hordes nocturnes de rôdeurs (champ de flux, barricades cassées, fosses de 2 niveaux qui piègent, défense au corps à corps, tirs des tours), arrivée de nouveaux survivants à l'aube.
- `Game/` (Unity 6000.6, URP) : terrain en blocs par morceaux, survivants et zombies animés (modèles Blender), outil d'ordres façon K&C (clic ou glisser), HUD, minimap, nuit éclairée (feu de camp, lanternes), sons générés, SSAO et post-traitement (F2 pour comparer).
- 58 tests automatisés, tous verts.

## Jouer

Lancer `Builds/Phase0/AfterCity.exe` (raccourci « After City (test) » sur le bureau). Pour reconstruire : `.\Tools\Unity\phase0.ps1` (crée/configure le projet, construit la scène, fait le build Windows et mesure les FPS).

| Touche | Action |
|---|---|
| 1 à 7 | Ordres : couper, tailler, démonter, creuser, remblayer, barricade, chemin de terre |
| 8, 9, 0 | Bâtiments : abri, tour de guet, poste d'expédition (l'atelier de démontage se pose depuis la barre) |
| Clic / glisser | Donner l'ordre sur une case ou une zone |
| Clic droit / glisser droit | Annuler des ordres |
| WASD, flèches, bords d'écran | Déplacer la caméra |
| Molette, Q / E | Zoom, rotation |
| Espace, + / - | Pause, vitesse |
| N | Passer à la nuit (la horde arrive à 20h30) |
| F2 | Effets visuels on/off |
| H | Aide (objectifs et commandes) |
| Minimap | Clic pour déplacer la caméra |

## Modèles 3D

`Tools/Blender/make_villager_test.py` génère les personnages (`villager` ou `zombie`) : `.blend` dans `Art/Characters`, `.fbx` dans `Game/Assets/Art/Characters`.
## Lancer les tests

```bash
cd Tests
dotnet test
```

Nécessite le SDK .NET 8. Le moteur cible `netstandard2.1` et C# 9, comme Unity 6.
