# Peuplement de la carte — v0.1

`peuplement.json` dit au fil de la démo quoi poser où. Pour chaque zone (forêt dense, lisière, prairie, roche, marais, abords de la ferme, ruines au bord de la route), il donne des **groupes** de modèles avec une **densité** : la probabilité qu'une case de la zone reçoive un modèle du groupe, tirée avec la graine de la carte. Les nombres à côté des modèles sont des poids de tirage.

- **Grosses pièces** (arbres, rochers, buissons, épaves, maisons) : une par case, elles bloquent la construction. **Détails au sol** (herbes, feuilles, branches, flaques, boue, ossements) : emprise 0, se cumulent, affichés à moins de 60 m de la caméra.
- **Ferme** : des garanties de départ (6 buissons à baies, 8 tas de cailloux, 12 arbres, 2 rochers à proximité) pour que le joueur puisse construire le Puits et nourrir ses 8 survivants les premiers jours, d'après `equilibrage/temps_et_paliers.json`.
- **Ressources des modèles** : un arbre donne 6 bois, un rocher 30 pierres, un tas de cailloux 3 ou 5, un buisson à baies 6 légumes et regarnit en une saison (pas en hiver). Ces chiffres prolongent `equilibrage/` et restent à valider.
- **Animaux** : troupeaux de cerfs (2 à 4, un pour 400 cases de forêt dense, fuient à 8 cases) et groupes de sangliers (1 à 3, un pour 600 cases, chargent un survivant seul à moins de 4 cases). La cabane de chasseur ne produit que si un troupeau vit à moins de 14 cases.
- **Saisons** : feuillus et tapis de feuilles passent en version Automne puis Hiver ; pas de fleurs ni de baies en hiver.

Modèles : `decor/nature/` (51), `decor/ruines/` (29), `modeles3d/sorties/` (animaux, ferme, Rôdeur). Tous les noms du fichier existent (vérifié).
