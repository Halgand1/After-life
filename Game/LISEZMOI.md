# Game/ : le projet Unity

Ce dossier accueillera le projet Unity 6, créé pendant la phase 0 depuis Unity Hub
(modèle **Universal 3D**, emplacement : ce dossier `Game`).

Une fois le projet créé, on relie le moteur After City en ajoutant cette ligne dans
`Game/Packages/manifest.json`, section `dependencies` :

```json
"com.aftercity.core": "file:../../Core"
```

Unity charge alors tout le code de `Core/Runtime` comme un paquet local, sans copie.
