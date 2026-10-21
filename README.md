# Handtracking Physics Playground → Android + Google Cardboard

Port du vrai projet **Spatial Cinematics / Handtracking-Physics** (Unity) vers un smartphone Android dans un casque Cardboard :
rendu stéréo + gyroscope (SDK Cardboard officiel), suivi des mains **MediaPipe Hand Landmarker** (caméra arrière), mains physiques d'origine inchangées.

> **État honnête** : ce kit a été écrit sans éditeur Unity ni compilateur (environnement sans accès à Unity/Maven). **Rien n'a été compilé ni testé sur téléphone.** Aucun APK n'existe à ce jour. Le build cloud ci-dessous est la première compilation réelle : prévoyez une ou deux itérations de corrections sur les erreurs qu'elle révèlera. Voir `docs/PORT_NOTES.md`.

## Contenu
- `overlay/` : uniquement les fichiers ajoutés/remplacés (couche de compatibilité `OVRCompat`, runtime `Port`, pont Java MediaPipe, manifest de packages).
- `UPSTREAM.txt` : dépôt d'origine épinglé (`13e9111`). Le code d'origine n'est jamais modifié ni copié ici.
- `tools/apply_port.py` : assemble `upstream + overlay` → projet Unity (retire le SDK Oculus, valide les GUID).
- `.github/workflows/` : build cloud (GameCI). `tools/build-local.*` : build sur votre PC.

## Minimum d'actions pour obtenir l'APK (GitHub Actions)
Il faut un compte GitHub **à vous** et une licence Unity **à vous** (Personal gratuite) : je ne peux pas les contourner.
1. Créez un dépôt **privé**, importez ce dossier (il est petit : ~35 fichiers).
2. Onglet *Actions* → **1 - Request Unity activation file** → *Run workflow* → téléchargez l'artefact `.alf`.
3. Sur https://license.unity3d.com/manual envoyez le `.alf` (connexion Unity ID, choisir *Unity Personal*) → vous recevez un `.ulf`.
4. Dépôt → *Settings → Secrets and variables → Actions* : créez `UNITY_LICENSE` (contenu complet du `.ulf`), `UNITY_EMAIL`, `UNITY_PASSWORD`.
5. *Actions* → **2 - Build Android APK** → *Run workflow*. L'APK debug apparaît en artefact `HandPlaygroundCardboard-debug-apk` (le workflow vérifie qu'il existe, son intégrité zip et sa taille).
Alternative sans GitHub : Unity 2019.4.40f1 + module Android installés → `UNITY=... ./tools/build-local.sh` (ou `build-local.ps1`).

## Installation et usage
1. Copiez l'APK sur le téléphone, autorisez les sources inconnues, installez (Android 7.0+/API 24, ARM64 ; ARMv7 : `PORT_ARMV7=1`).
2. Au 1er lancement : accepter la caméra ; scanner le QR de votre viewer Cardboard (ou menu → « Scanner le QR »).
3. Téléphone en paysage dans le viewer, **objectif arrière dégagé** (trou dans le viewer). Tendez les mains devant vous, paumes/dos visibles de l'objectif.
4. Menu : toucher l'écran / bouton Cardboard, puis regarder un bouton ~1,2 s. Réglages : recentrage, suivi on/off, qualité, fréquence, lissage, hauteur des yeux.

## Limites (à lire)
- **Rotation de tête seulement (3DoF)** : pas de suivi de position.
- **Mains = 2,5D monoculaire** : profondeur *estimée* par la taille de la paume (pas mesurée) ; un seul objectif, décalé des yeux (`CameraOffset` réglable) ; mains hors champ de l'objectif = non suivies. MediaPipe ne voit rien que la caméra ne voit pas.
- Orientation de la « roulis » des doigts de l'avatar à calibrer sur appareil (`RollOffsetLeft/Right`).
- La scène `Slow` (manettes Quest, hors build d'origine) n'est pas portée.
Détails : `docs/PORT_NOTES.md`.
