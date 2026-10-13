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

## Ce qui existe déjà (avant la phase 0)

- `Core/Runtime/World` : grille à hauteurs (0 à 8) et biomes, génération de carte par graine avec ferme de départ, terraformage (creuser, remblayer, aplanir) avec gestion de la terre.
- `Core/Runtime/Agents/FlowField.cs` : champ de flux des hordes, avec la règle « une marche de 2 niveaux arrête un rôdeur », les murs à casser et l'eau infranchissable.
- `Core/Runtime/Sim` : stocks de ressources.
- 20 tests automatisés, tous verts.

## Lancer les tests

```bash
cd Tests
dotnet test
```

Nécessite le SDK .NET 8. Le moteur cible `netstandard2.1` et C# 9, comme Unity 6.
