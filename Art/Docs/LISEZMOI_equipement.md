# Équipement des survivants — v0.1

Généré par `modeles3d/scripts/generer_equipement.py` (Blender 5.2, module bpy). 38 objets sur 6 emplacements, chacun en LOD0/1/2, nommés selon [CONVENTIONS.md](../../CONVENTIONS.md) : `SM_Equip_<Emplacement>_<Nom>`. Palette partagée `modeles3d/sorties/T_Palette_256.png`, matériau `MAT_Palette_Personnages`, aucune couleur ajoutée. Origine de chaque objet sur son accroche, positions d'accroche identiques à CONVENTIONS.md (le script les lit dans `generer_personnages.py`).

| Emplacement | Accroche | Objets |
|---|---|---|
| Tete | `SOCKET_Tete` | CasqueMilitaire, CasquePolice, CasqueChantier, CasqueMoto, Casquette, Bonnet, ChapeauPisteur, Bandana, MasqueGaz |
| Torse | `SOCKET_Torse` | GiletTactique, GiletPolice, PlastronBricole, GiletHauteVisibilite, BrassardMedecin |
| Dos | `SOCKET_Dos` | SacRandonnee, SacPetit, FusilDos, ArbaleteDos, HotteBois, JerricanDos |
| Ceinture | `SOCKET_Ceinture` | CeintureOutils, CeintureTactique, CeintureSurvie, CeintureMedicale |
| MainDroite | `SOCKET_MainDroite` | Hache, Pioche, Pelle, Marteau, Houe, BatteCloutee, Machette, PiedDeBiche, Fusil, Arbalete, Seau |
| MainGauche | `SOCKET_MainGauche` | SacMedical, LampeTorche, BouclierPanneau |

`catalogue_equipement.json` donne pour chaque objet son fichier, son accroche, ses triangles et les rôles qui le portent, plus la **dotation de départ** de chaque rôle (Sam : chaque survivant est pré‑équipé). `apercu_survivants_equipes.png` montre les 7 rôles équipés ; `apercu_equipement.png` montre tous les objets.

## Dans Unity
Chaque FBX contient le squelette `RIG_Humain` (sans corps ni animation) et l'objet `_LOD0/1/2`, **enfant de son accroche** avec la bonne position et la bonne rotation locales.
1. Importer le FBX (*Rig > Animation Type = Generic* ou *None* : seul le maillage sert).
2. Pour équiper un survivant : instancier l'objet sous l'os de même nom (`SOCKET_Tete`…) du personnage, en recopiant `localPosition`, `localRotation` et `localScale` qu'il a sous l'accroche dans le FBX d'équipement. Les squelettes étant identiques, l'objet suit ensuite les animations.
3. Matériau : le même que les personnages (URP Lit, `T_Palette_256`, filtre *Point*).

## Budgets (CONVENTIONS.md : une pièce 50–250, équipement complet 600)
Une pièce : 24 à 228 triangles au LOD0 (les petits outils sont sous 50, c'est voulu). Dotations de départ au LOD0 : Carrier 290, Nettoyeur 432, Médecin 452, Ouvrier 502, Pisteur 548, Milicien 568, Militaire 600. Les objets sont faits sur le corps A ; sur le corps B (épaules 4 cm plus étroites) les gilets débordent de 2 cm, invisible à la caméra du jeu.
