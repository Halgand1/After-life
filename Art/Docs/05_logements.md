# Fiche 05 — Logements façon Kingdoms and Castles

Référence : GDD v0.9 § 8.1 (demande de Sam : chaque logement montre son intérieur et une étagère de ressources). Conventions : [00_conventions.md](00_conventions.md). Script : `scripts/generer_logements.py`. Sorties : `sorties/SM_Logement_*`, `SM_Interieur_*`, `SM_Etagere_*`, `bilan_logements.txt`, aperçus `apercu_logements_coupe.png`, `apercu_logements.png`, `apercu_interieur.png`.

## 1. Principe
- Chaque FBX `SM_Logement_<Type>` contient la **coque meublée** (`SM_Logement_<Type>_LOD0/1/2` : sol, murs, couchages, étagères vides, poêle, table) et le **toit à part** (`SM_Logement_<Type>_Toit_LOD0/1/2`). Pour la vue en coupe, le jeu masque les objets dont le nom contient `_Toit_`. Les murs font un étage (1,6 m) : depuis la caméra du jeu, on voit tout l'intérieur sans toit, comme dans K&C.
- Les étagères sont vides. Chaque case porte une accroche `SOCKET_Etagere_NN_Case_MM` (orientée comme l'étagère) où le jeu pose un objet `SM_Etagere_<Ressource>_<1|2|3>` selon le stock réel de la maison (1 = peu, 2 = moitié, 3 = plein ; rien = vide). Étagère : 3 niveaux × 2 cases = 6 cases. La caisse de la tente a 2 cases sur son couvercle.
- Autres accroches : `SOCKET_Porte`, `SOCKET_Couchage_NN` (un par place, pour l'animation de sommeil), `SOCKET_Lumiere_NN`, `SOCKET_Fumee` (poêle), `SOCKET_Travail_Poele` / `_Cuisine`.

## 2. Les cinq logements
| Logement | Emprise | Places | Intérieur | Cases d'étagère | Murs | Toit |
|---|---|---|---|---|---|---|
| Tente | 1 × 1 | 2 | matelas double sur palettes, caisse, lanterne | 2 | — | toile beige + bâche bleue |
| Abri | 2 × 2 | 4 | 4 sacs de couchage au sol, 1 étagère, caisse-tabouret | 6 | planches grises, tôles clouées | tôles dépareillées à une pente, bâche |
| Foyer (dortoir) | 3 × 3 | 10 | 10 lits de camp, 3 étagères, poêle, grande table à bancs | 18 | planches grises | bardeaux, bâche, cheminée de poêle |
| Maison en bois | 2 × 3 | 5 | séjour (poêle, table, 2 étagères) + chambre (lit double, 3 lits) | 12 | planches blanches | bardeaux |
| Maison en briques | 3 × 3 | 6 | séjour‑cuisine (cuisinière, table 4 chaises, 3 étagères) + 2 chambres | 18 | briques et joints | bardeaux, cheminée en brique |

Triangles (coque + toit) : voir `bilan_logements.txt` ; tous sous les budgets des bâtiments.

## 3. Kit d'intérieur (`SM_Interieur_*`, pour d'autres bâtiments ou des variantes)
Couchage (sac au sol), LitCamp, LitDouble, Etagere (avec ses 6 accroches), Caisse (2 accroches), Poele, Cuisiniere, Table (4 chaises), TableBancs.

## 4. Objets d'étagère (`SM_Etagere_<Ressource>_<1|2|3>`)
Origine au centre de la case, au niveau de la planche ; encombrement maximal 0,38 × 0,28 × 0,30 m.
| Ressource | 1 (peu) | 2 (moitié) | 3 (plein) |
|---|---|---|---|
| Buches (bois de chauffage) | 2 bûches | 4 | 6 en pyramide |
| Bocaux (fruits, légumes) | 2 bocaux | 4 | 6 |
| BidonsEau | 1 jerrican bleu | 2 | 3 |
| Conserves | 3 boîtes | 6 | 10 sur deux rangs |
| Pharmacie | boîte à croix rouge | + bandages | + flacons |
| Savon (savon, alcool, bouteilles) | 2 pains | + 2 bouteilles brunes | + 2 bouteilles vertes |
