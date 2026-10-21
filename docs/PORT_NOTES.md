# Notes de portage

## 1. Source et audit (vérifiés)
- Page itch.io « Handtracking Physics Playground » → dépôt `Spatial-Cinematics/Handtracking-Physics`, cloné (`13e9111`, 2020-04-24). Licence **Apache-2.0**. Moteur **Unity 2019.3.2f1**.
- Code de jeu : ~740 lignes (`Assets/Scripts`). `Assets/Oculus` (649 Mo) = Oculus Integration SDK (licence Oculus propriétaire).
- Couture avec Oculus : `OVRSkeleton.Bones[id].Transform` (lu par `HandCapsules`), `OVRInput` (manettes, `PhysicsHand`, `InputDeviceSwitcher`), et dans `Main.unity` : `OVRCameraRig`, `OVRManager`, `OVRGrabbable`×5, `OVRScreenFade`, `OVRHeadsetEmulator`, prefab imbriqué `OVRHandPrefab`. Aucun `OVRGrabber` dans `Main` : les objets sont saisis par contact physique des capsules.
- Physique : capsules cinématiques (`pa`) suivant les os + corps dynamiques (`pb`) reliés par `FixedJoint`, IK Animation Rigging pour l'avatar. Conservée telle quelle. Pas de saisie « magique » : la prise vient des collisions/frottements.
- Réglages d'origine lourds pour mobile : pas fixe 0,005 s (200 Hz), solveur 10/30 itérations ; textures de l'avatar 4K (~40 Mo de PNG).

## 2. Stratégie : compatibilité à GUID identiques
Le SDK Oculus ne peut être redistribué qu'« en entier » et « en binaire » (licence 3.3). Il est donc retiré à l'assemblage. `overlay/Assets/OVRCompat` réécrit **de zéro** les ~7 classes utilisées avec les **mêmes GUID et noms de champs sérialisés**, plus un `OVRHandPrefab.prefab` minimal reprenant les `fileID` référencés. Résultat : scènes, prefabs et scripts de jeu d'origine **non modifiés**.
`OVRSkeleton` est alimenté par `HandTrackingService` : 24 os (ids identiques à l'original), +X le long de l'os vers l'enfant, +Y vers le dos de la main. Hypothèse déduite des capsules du prefab (collider direction X, centre +X) ; à confirmer sur appareil.
Le runtime se greffe via `RuntimeInitializeOnLoadMethod` : aucun fichier de scène n'est édité.

## 3. Versions
| Élément | Choix | Raison |
|---|---|---|
| Unity | 2019.4.40f1 | Cardboard XR Plugin v1.20.0 supporte 2019.4+ ; les versions récentes exigent Unity 6 |
| Cardboard | `com.google.xr.cardboard` v1.20.0 (git), xr.management 3.2.9, legacyinputhelpers 2.1.4 | dépendances déclarées par le package |
| MediaPipe | Tasks Android `tasks-vision:0.10.14` + CameraX 1.3.4 | officiel, pas de Bazel ; caméra gérée en natif (pas de WebCamTexture) |
| Modèle | `hand_landmarker.task` (float16, ~7,5 Mo, valeur à confirmer à l'assemblage — le script affiche la taille réelle) | seul bundle officiel Hand Landmarker |
| Graphismes | OpenGL ES 3 uniquement | requis par le plugin Cardboard 1.20 |

Versions non vérifiables depuis le sandbox (Maven bloqué) : `tasks-vision:0.10.14`, `camera:1.3.4`, `2019.4.40f1` côté GameCI. Si Gradle ne les trouve pas, ajustez `PortBuild.Deps`.

## 4. Caméra du téléphone
Dans un Cardboard, l'écran est face aux yeux : l'objectif **arrière** regarde vers l'avant (bon sens), l'objectif frontal vers le visage (inutilisable). Contraintes réelles : (a) le viewer doit laisser l'objectif arrière dégagé ; (b) l'objectif est excentré (un côté du téléphone) → une main peut être hors champ ; (c) champ de vision ~65° < champ VR ; (d) monoculaire : profondeur estimée ; (e) étiquettes gauche/droite de MediaPipe supposent une image miroir → inversées (réglable). Configuration la plus simple : viewer à trou/lid ouvert, objectif côté dominant, mains tendues à 25–60 cm.

## 5. Performances
Préréglages runtime (`PortSettings.ApplyQuality`) : **Original** (valeurs d'origine), **Balanced** (défaut : 120 Hz physique, 8/16 itérations, ombres dures), **Light** (90 Hz, 6/8, sans ombres, 45 FPS). `maximumDeltaTime` borné à 50 ms. Textures `3DModels`/`Textures` → ASTC 6×6 max 2048 sur Android à l'import (fichiers sources intacts). Suivi : 640×480, 30 Hz par défaut (10–60), CPU (GPU delegate optionnel, non garanti), 1 bitmap RGBA par résultat, 1 float[] (~1 Ko) par résultat vers Unity, lecture de contrôle sans allocation. Fréquence de suivi découplée du rendu : les mains sont recalculées à chaque frame dans le repère de la tête courante.
**Aucune mesure réelle** (APK, RAM, FPS, latence) n'existe : à relever sur appareil.

## 6. Licences
- Jeu d'origine : Apache-2.0 (conserver LICENSE et mentions). Assets tiers (avatar Mixamo, etc.) : voir leurs conditions.
- Cardboard XR Plugin : Apache-2.0 ; la marque « Google Cardboard » : usage descriptif seulement.
- MediaPipe / modèle : Apache-2.0 (Google). CameraX : Apache-2.0.
- Oculus Utilities SDK : **non redistribué** ici.
- Unity : licence Personal à votre nom, activée en CI via vos secrets.

## 7. Test sur appareil (checklist)
1. Démarrage sans crash, orientation paysage, 2 vues stéréo, distorsion cohérente après scan QR.
2. Rotation de tête fluide ; recentrage (menu / maintien bouton).
3. Voyant caméra OK ; refuser la permission → message lisible.
4. Main droite puis gauche détectées (● dans le menu) ; vérifier l'inversion G/D, sinon basculer `SwapHandedness`.
5. Capsules suivent la main ; pousser le ballon ; saisir/relâcher la hache ; occultation puis réapparition sans « explosion ».
6. Mise en arrière-plan / retour. FPS et température sur 10 min en Balanced puis Light.
