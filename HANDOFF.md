# HANDOFF — After City (état au 13/10/2026, 20h45)

## Ce qui existe et marche

- **Moteur `Core/`** (C# pur, 64 tests verts avec `cd Tests; dotnet test`) :
  - carte générée par graine, décor (arbres, rochers, épaves, cailloux, baies) ;
  - survivants (chemins A*), ordres : couper, tailler, ramasser des cailloux (4 = 1 pierre), cueillir, fouiller, démonter, creuser, remblayer, barricader, chemin de terre, asphalte ;
  - construction avec arbre de progression (abri > poste d'expédition > atelier > tour de guet ; champ libre) et recherche Asphalte ;
  - nourriture (récolte à 18 h, repas à l'aube, famine), jour/nuit, arrivée de survivants à l'aube ;
  - hordes : première la nuit du jour 3, puis 3 + 2n ; barricades cassées, fosses de 2 niveaux qui piègent, tours qui tirent ;
  - expéditions (réussite de 50 à 95 %), démontage par rareté.
- **Jeu `Game/`** (Unity 6000.6.5f1, URP) :
  - terrain en chunks, HUD IMGUI (barre d'outils par catégories, Tab et 1-6), minimap, aide (H) ;
  - tutoriel optionnel (PlayerPrefs `aftercity.tutorial.done`) ;
  - nuit éclairée (feu de camp, lanternes), sons générés (M pour couper), SSAO et post-traitement (F2), chute des arbres, outils en main.
- **Build** : `.\Tools\Unity\phase0.ps1` configure le projet, construit la scène et le build Windows dans `Builds/Phase0/`, puis lance une mesure de 20 s (FPS et captures). Raccourci bureau : « After City - démo ».
  - `-aftercity-soak <s>` : test d'endurance.
  - `-aftercity-shot <png>` : capture du premier lancement.
- **Dernières mesures** (RTX 5060, 1600x900, vsync coupée) : environ 700 FPS avec effets, 780 sans. L'endurance sur 4 jours de jeu passe sans erreur.

## Pipeline personnages (travail en cours, à tester avec les vrais FBX)

- `Game/Assets/Editor/CharacterImport.cs` (AssetPostprocessor) couvre `Assets/Art/Personnages/`, `Assets/Art/Equipement/` et `Assets/Art/Animaux/` :
  - SK_ humains et zombies en Humanoid, animaux (SK_Cerf, SK_Sanglier, SK_Cheval, SK_Vache) en Generic, SM_ en statique ;
  - clips en boucle, préfixe `Armature|` retiré ;
  - LODGroup construit depuis _LOD0/_LOD1/_LOD2 ;
  - matériau unique `MAT_Personnages_Palette`, créé à la configuration (pas pendant l'import), avec le shader `AfterCity/CharacterPalette` : palette × couleur de sommet R (occlusion) × G (saleté) ;
  - texture `T_Palette_256` : filtre Point, sans mipmaps.
- `Phase0Builder.EnsurePersonnages` génère `AC_Humain` et `AC_Zombie`, avec un état par clip `[ANIM_]Humain_<Etat>` ou `[ANIM_]Zombie_<Etat>`, et un prefab `Prefabs/PF_<modèle>` par FBX présent. Ensuite, `GameRoot.survivorPrefabs[id % n]` remplace le villageois de test ; sans fichier, le jeu revient aux modèles de test.
- Les états sont choisis par `AnimStates` selon l'ordre en cours :
  - Frapper : bois et chantier ;
  - Piocher : pierre et creuser ;
  - RamasserCailloux, Cueillir, Fouiller : actions du même nom ;
  - Marche et Repos sinon.
  - Zombies : Marche, Attaque ou Repos.
- Testé avec un humanoïde d'essai : avatar valide (humanoïde) et matériau assigné. L'essai a été retiré du dossier.

## Art de l'equipe 3D branche (13/10, soir)

- **Animations** : les FBX v0.3 n'avaient aucune animation (export NLA vide). Les v0.3b (pose de repos en T) les contiennent ; `Builds/Logs/03_scene.log` liste les clips par controleur. Script de secours si un export revient vide : on recharge le .blend par armature, on ne garde que ses actions, export `bake_anim_use_all_actions`.
- **DecorKit** (`Scripts/World/DecorKit.cs`, `Editor/DecorKitBuilder.cs`) : chaque SM_ de `Decor/Nature`, `Decor/Ruines`, `Batiments` est cuit en un maillage LOD0 (`Assets/Art/Baked/DecorKit.asset`) ; le terrain les fusionne par morceau de 32 x 32 cases dans un second maillage (`Decor_i_j`), materiau `MAT_Decor_Palette` (shader palette, texture `Batiments/T_Batiments_1024.png`). Recolter un arbre reconstruit le morceau comme avant.
- **DecorPlacer** (`Scripts/World/DecorPlacer.cs`) : choix des modeles par biome (pins serres en foret dense, feuillus/bouleaux en plaine, arbres morts, souches et debris dans un rayon de 4 cases autour des epaves = zones de ruines), rochers, cailloux, baies, epaves (brulees une fois fouillees), maisons en ruine, ferme abandonnee (corps de ferme en ruine, silo, puits), batiments du moteur (abri = cabane, poste d'expedition = tas de stockage, atelier = hangar, champ = potager, tour de guet, grange en ruine), barricades = palissades. Un decor fixe disparait si on construit dessus.
- **Animaux** : `AnimalView` (cerfs et sangliers) errent en foret dense, Repos/Marche/Brouter/Fouir, pas de chasse.
- **Zombies** : 6 sur 10 sont des rodeurs, puis coureur, cogneur, grimpeur, brute (`GameRoot.ZombieType`).
- **Survivants** : modele = role par ordre d'arrivee (Ouvrier, Carrier, Milicien, Pisteur, Medecin, Nettoyeur, Militaire) ; pas encore de `Villager.Role` dans Core. Les nouveaux modeles gardent leur outil d'origine (pas d'outil procedural en plus).
- Le shader palette utilise maintenant l'ambiance du jeu (`_AC_AmbientSky/Ground`), comme le terrain : personnages et decor s'assombrissent la nuit.
- Mesure : le benchmark ajoute `screenshot_ensemble.png`, `screenshot_survivants.png`, `screenshot_foret.png`.

## À faire ensuite

1. Déposer les vrais FBX, relancer `phase0.ps1`, lire dans `Builds/Logs/03_scene.log` les lignes `[AfterCity] AC_Humain …`, `avatar valide=…` et `materiau …`.
2. « Enforce T-Pose » n'existe pas dans l'API ModelImporter. Si les avatars sont mal formés (bras le long du corps), régler la pose dans la configuration de l'avatar ou exporter les modèles en T-pose.
3. Pas encore utilisés en jeu :
   - Course et MarcheChargee (pas de portage dans le moteur) ;
   - l'échange d'équipement par `SOCKET_*` et `catalogue_equipement.json` ;
   - les types de zombies (le modèle est choisi par id, sans règle de jeu) ;
   - les animaux.
4. Rôles des survivants : le modèle est actuellement choisi par `id % 7` ; il faudra les relier à un vrai rôle dans `Core` (Villager.Role).
5. Plus tard (retours de Sam) : cerfs et sangliers en forêt, enclos, emplacements d'équipement par survivant, personnages plus détaillés.

## Repères

- Envoi GitHub : la session du projet pousse le code ; le paquet de transfert est dans `Builds/transfer/` (bundle git + objets LFS).
- Le modèle de test du villageois et du zombie vient de `Tools/Blender/make_villager_test.py [villager|zombie]`.
- `Builds/Phase0_stable/` est une copie de secours d'un build qui marche.
