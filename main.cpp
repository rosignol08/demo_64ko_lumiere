#include "raylib.h"
#include "raymath.h"
#include "rlgl.h"
//#include "raygui.h"
#include <stdlib.h>
#include <stdio.h>
#include <vector>
#include <iostream>
#define RLIGHTS_IMPLEMENTATION
#if defined(_WIN32) || defined(_WIN64)
//#include "include/shaders/rlights.h"
#elif defined(__linux__)
//#include "include/shaders/rlights.h"
#endif

#if defined(PLATFORM_DESKTOP)
    #define GLSL_VERSION            330
#else   // PLATFORM_ANDROID, PLATFORM_WEB
    #define GLSL_VERSION            330
#endif

// Variables globales pour stocker les angles de rotation
float angleX = 0.0f;
float angleY = 0.0f;
float distance_cam = 5.0f;

// Variable pour activer/désactiver la rotation
bool isRotating = false;

#define MAX_SPHERES 2
#define MAX_BLOCKS 6

// Structure pour les sphères
typedef struct {
    Vector3 position;
    float radius;
} Sphere;

//structure pour les blocs (murs)
typedef struct {
    Vector3 position;
    Vector3 size; // Taille du bloc (largeur, hauteur, profondeur)
} Block;

// Structure pour les matériaux
typedef struct {
    int type;         // 0 = diffus, 1 = métallique, 2 = verre, 3 = emissif 4 = mirroir 5 = zone_emition 6 = eau
    float roughness;  // 0.0 - 1.0
    float ior;        // indice de réfraction (verre)
    float padding;    // pour alignement
    Vector3 albedo;   // couleur
    float padding2;   // pour alignement
} Material2;

// Données des sphères
Sphere spheres[MAX_SPHERES] = {
    {{0.0f, 10.0f, 0.0f}, 1.0f}     // Sphère centrale (commence à y=10)
    //{{1.5f, 0.0f, 1.5f}, 0.5f},      // Petite sphère
    //{{-2.5f, 0.0f, 0.0f}, 1.0f},    // Sphère à gauche
    //{{2.5f, 0.0f, 0.0f}, 1.0f},     // Sphère à droite
    //{{0.0f, -1001.0f, 0.0f}, 1000.0f}, // Sol (grosse sphère en dessous)
    //{{0.0f, 0.0f, -2.5f}, 1.0f},    // Sphère derrière
    //{{0.0f, 0.0f, 2.5f}, 1.0f},     // Sphère devant
    //{{-1.5f, 0.0f, -1.5f}, 0.5f},   // Petite sphère
};

// Matériaux correspondants
//int type;       // Type de matériau
//float roughness; // Rugosité (métal, verre)
//float ior;      // Indice de réfraction (verre)
//float padding;  // Padding pour l'alignement
//vec3 albedo;    // Couleur de base
//float padding2; // Padding supplémentaire

Material2 materials[MAX_SPHERES] = {
    {3, 0.0f, 1.0f, 0.0f, {1.0f, 0.50f, 0.0f}, 0.0f}    // Balle miroir
    //{3, 0.0f, 1.0f, 0.0f, {0.9f, 0.9f, 0.0f}, 0.0f}     // Jaune diffus
    //{1, 0.1f, 1.0f, 0.0f, {0.8f, 0.8f, 0.9f}, 0.0f},    // Métal bleuté
    //{2, 0.0f, 1.5f, 0.0f, {0.9f, 0.9f, 0.9f}, 0.0f},    // Verre
    //{0, 0.5f, 1.0f, 0.0f, {0.8f, 0.8f, 0.8f}, 0.0f},    // Sol gris diffus
    //{0, 0.2f, 1.0f, 0.0f, {0.9f, 0.3f, 0.3f}, 0.0f},    // Rouge diffus
    //{1, 0.2f, 1.0f, 0.0f, {0.9f, 0.6f, 0.2f}, 0.0f},    // Métal doré
    //{2, 0.1f, 1.3f, 0.0f, {0.3f, 0.7f, 0.9f}, 0.0f},    // Verre bleuté
};

//les murs :
//un grand mur d'eau donc mirroir
Block blocks[MAX_BLOCKS] = {
    {{0.0f, -1.0f, 0.0f}, {200.0f, 0.1f, 200.0f}}  // Sol
    //{{0.0f, 10.0f, 0.0f}, {20.0f, 0.1f, 20.0f}},  // Plafond
    //{{-10.0f, 0.0f, 0.0f}, {0.1f, 20.0f, 20.0f}}, // Mur gauche
    //{{10.0f, 0.0f, 0.0f}, {0.1f, 20.0f, 20.0f}},  // Mur droit
    //{{0.0f, 0.0f, -10.0f}, {20.0f, 20.0f, 0.1f}}, // Mur arrière
    //{{0.0f, 0.0f, 10.0f}, {20.0f, 20.0f, 0.1f}}   // Mur avant
};

Material2 materials_block[MAX_BLOCKS] = {
    {6, 0.80f, 1.0f, 0.0f, {0.2f, 0.2f, 0.225f}, 0.0f} // Mur gauche gris
    //{1, 0.80f, 1.0f, 0.0f, {0.2f, 0.2f, 0.225f}, 0.0f}, // Mur droit gris
    //{1, 0.80f, 1.0f, 0.0f, {0.2f, 0.2f, 0.225f}, 0.0f}, // Mur arrière gris
    //{1, 0.80f, 1.0f, 0.0f, {0.2f, 0.2f, 0.225f}, 0.0f}, // Mur avant gris
    //{1, 0.80f, 1.0f, 0.0f, {0.2f, 0.2f, 0.225f}, 0.0f}, // Mur gauche avant gris
    //{1, 0.80f, 1.0f, 0.0f, {0.2f, 0.2f, 0.225f}, 0.0f}  // Mur droit avant gris
};


// Position de la lumière
Vector3 lightPos = {0.f,1.f,0.f};
// Couleur de la lumière
Vector3 lightColor = {1.0f, 0.50f, 0.0f};// Lumière légèrement chaude {1.0f, 0.9f, 0.8f};
// Intensité de la lumière
float lightIntensity = -0.20f;

// Variables pour le faisceau lumineux
Vector3 beamDirection = {-0.5f, -1.0f, 0.5f}; // Direction du faisceau
Vector3 beamPosition = {-1.0f, 10.0f, 0.0f};
Vector3 beamColor = {1.0f, 0.0f, 0.0f};
float beamAngle = 0.0f;      // Angle du cône (en radians)
float beamIntensity = 100.0f; // Intensité du faisceau
bool enableBeam = true;      // Activer/désactiver le faisceau

// Variables pour les vagues circulaires
Vector3 waveCenter = {0.0f, -1.0f, 0.0f}; // Centre des ondulations (sur la surface de l'eau)
bool enableWaves = false;     // Activer/désactiver les vagues
float waveDuration = 5.0f;   // Durée des vagues (en secondes)
float waveAmplitude = 0.5f; // Amplitude des vagues
float waveStartTime = 0.0f;  // Moment où les vagues ont commencé
float waveDecayRate = 0.9f;  // Taux de dissipation (90% = 10% de réduction par seconde)

int main(void) {
    // Initialisation
    const int screenWidth = 1280;
    const int screenHeight = 720;
    
    SetConfigFlags(FLAG_MSAA_4X_HINT); // Enable Multi Sampling Anti Aliasing 4x (if available)
    InitWindow(screenWidth, screenHeight, "Raytracer avancé - GLSL");
    
    Camera camera = { 0 };
    camera.position = (Vector3){ 0.0f, 2.0f, 6.0f };  // Position initiale de la caméra
    camera.target = (Vector3){ 0.0f, 0.0f, 0.0f };    // Point visé par la caméra
    camera.up = (Vector3){ 0.0f, 1.0f, 0.0f };        // Vecteur "up" de la caméra
    camera.fovy = 60.0f;                              // Field of view Y
    camera.projection = CAMERA_PERSPECTIVE;           // Type de projection

    // Chargement du shader de raytracing
    Shader shader = LoadShader(0, "raytest.fs");
    //Shader denoiser_shader = LoadShader(0, "denoiser.fs");

    //test denoiser plusieurs passes
    Shader denoise_shader = LoadShader(0, "denoise.fs");
    Shader taa_shader = LoadShader(0, "taa.fs");
    
    // Récupération des emplacements des uniformes dans le shader
    int viewEyeLoc = GetShaderLocation(shader, "viewEye");
    int viewCenterLoc = GetShaderLocation(shader, "viewCenter");
    int resolutionLoc = GetShaderLocation(shader, "resolution");
    int timeLoc = GetShaderLocation(shader, "time");
    
    // Paramètres de résolution pour le shader
    float resolution[2] = { (float)screenWidth, (float)screenHeight };
    SetShaderValue(shader, resolutionLoc, resolution, SHADER_UNIFORM_VEC2);
    
    // Emplacement des uniformes pour les sphères et les matériaux
    int spheresLoc = GetShaderLocation(shader, "spheres");
    int materialsLoc = GetShaderLocation(shader, "materials");
    int sphereCountLoc = GetShaderLocation(shader, "sphereCount");
    int lightPosLoc = GetShaderLocation(shader, "lightPos");
    int lightColorLoc = GetShaderLocation(shader, "lightColor");
    int lightIntensityLoc = GetShaderLocation(shader, "lightIntensity");
    
    // Emplacements pour le faisceau lumineux
    int beamDirectionLoc = GetShaderLocation(shader, "beamDirection");
    int beamPositionLoc = GetShaderLocation(shader, "beamPosition");
    int beamColorLoc = GetShaderLocation(shader, "beamColor");
    int beamAngleLoc = GetShaderLocation(shader, "beamAngle");
    int beamIntensityLoc = GetShaderLocation(shader, "beamIntensity");
    int enableBeamLoc = GetShaderLocation(shader, "enableBeam");
    
    // Emplacements pour les vagues circulaires
    int waveCenterLoc = GetShaderLocation(shader, "waveCenter");
    int enableWavesLoc = GetShaderLocation(shader, "enableWaves");
    int waveDurationLoc = GetShaderLocation(shader, "waveDuration");
    int waveAmplitudeLoc = GetShaderLocation(shader, "waveAmplitude");
    int waveStartTimeLoc = GetShaderLocation(shader, "waveStartTime");
    int waveDecayRateLoc = GetShaderLocation(shader, "waveDecayRate");
    
    //pareil pour les blocs de murs
    int blocksLoc = GetShaderLocation(shader, "blocks");
    int materialsBlockLoc = GetShaderLocation(shader, "materials_block");
    int blockCountLoc = GetShaderLocation(shader, "blockCount");
    
    // Passage du nombre de sphères au shader
    int sphereCount = MAX_SPHERES;
    SetShaderValue(shader, sphereCountLoc, &sphereCount, SHADER_UNIFORM_INT);
    
    // Passage du nombre de blocs au shader
    int blockCount = MAX_BLOCKS;
    SetShaderValue(shader, blockCountLoc, &blockCount, SHADER_UNIFORM_INT);

    float runTime = 0.0f;
    
    DisableCursor();  // Limite le curseur à l'intérieur de la fenêtre

    //la render texture pour appliquer le post process shader
    RenderTexture2D target = LoadRenderTexture(screenWidth, screenHeight);
    //RenderTexture2D history = LoadRenderTexture(screenWidth, screenHeight);

    //pour le shader de denoising
    RenderTexture2D renderNoisy = LoadRenderTexture(screenWidth, screenHeight);
    RenderTexture2D renderNormals = LoadRenderTexture(screenWidth, screenHeight);
    RenderTexture2D renderHistory = LoadRenderTexture(screenWidth, screenHeight);
    RenderTexture2D denoiseTarget = LoadRenderTexture(screenWidth, screenHeight);
    RenderTexture2D taaOutput = LoadRenderTexture(screenWidth, screenHeight);
    
    int frameCounter = 0;

    SetTargetFPS(600); // Limite les FPS à 60
    
    // Boucle principale du jeu
    while (!WindowShouldClose()) {
        // Mise à jour de la logique
        float deltaTime = GetFrameTime();
        runTime += deltaTime;
        
        // Gestion des contrôles de la caméra
        if (IsMouseButtonPressed(MOUSE_RIGHT_BUTTON)) isRotating = true;
        if (IsMouseButtonReleased(MOUSE_RIGHT_BUTTON)) isRotating = false;
        
        // Capture des mouvements de la souris
        if (isRotating) {
            Vector2 mouseDelta = GetMouseDelta();
            angleX -= mouseDelta.y * 0.2f; // Sensibilité verticale
            angleY -= mouseDelta.x * 0.2f; // Sensibilité horizontale
        }
        
        // Gestion du zoom avec la molette de la souris
        distance_cam -= GetMouseWheelMove() * 0.5f;
        if (distance_cam < 2.0f) distance_cam = 2.0f;   // Distance minimale
        if (distance_cam > 20.0f) distance_cam = 20.0f; // Distance maximale
        
        // Limiter les angles X pour éviter une rotation complète
        if (angleX > 89.0f) angleX = 89.0f;
        if (angleX < -89.0f) angleX = -89.0f;
        
        // Calcul de la position de la caméra en coordonnées sphériques
        float radAngleX = DEG2RAD * angleX;
        float radAngleY = DEG2RAD * angleY;
        
        camera.position.x = distance_cam * cos(radAngleX) * sin(radAngleY);
        camera.position.y = distance_cam * sin(radAngleX);
        camera.position.z = distance_cam * cos(radAngleX) * cos(radAngleY);
        
        // Mouvement de la lumière sur un chemin circulaire
        //lightPos.x = 5.0f * cosf(runTime * 0.5f);
        //lightPos.y = 5.0f + 2.0f * sinf(runTime * 0.3f);
        //lightPos.z = 3.0f * sinf(runTime * 0.7f);
        
        // Contrôles optionnels pour ajuster manuellement la lumière
        if (IsKeyDown(KEY_U)) lightPos.y += 0.2f;
        if (IsKeyDown(KEY_J)) lightPos.y -= 0.2f;
        if (IsKeyDown(KEY_H)) lightPos.x -= 0.2f;
        if (IsKeyDown(KEY_K)) lightPos.x += 0.2f;
        if (IsKeyDown(KEY_Y)) lightIntensity -= 1.0f * deltaTime;
        if (IsKeyDown(KEY_I)) lightIntensity += 1.0f * deltaTime;
        
        // Contrôles pour le faisceau lumineux
        if (IsKeyPressed(KEY_B)) enableBeam = !enableBeam;  // Activer/désactiver le faisceau
        if (IsKeyDown(KEY_Q)) beamAngle -= 0.01f;          // Réduire l'angle du faisceau
        if (IsKeyDown(KEY_E)) beamAngle += 0.01f;          // Augmenter l'angle du faisceau
        if (IsKeyDown(KEY_T)) beamIntensity += 0.5f;       // Augmenter l'intensité du faisceau
        if (IsKeyDown(KEY_G)) beamIntensity -= 0.5f;       // Diminuer l'intensité du faisceau
        
        // Contrôles pour la direction du faisceau
        if (IsKeyDown(KEY_LEFT_SHIFT)) {
            if (IsKeyDown(KEY_W)) beamDirection.y += 0.02f;
            if (IsKeyDown(KEY_S)) beamDirection.y -= 0.02f;
            if (IsKeyDown(KEY_A)) beamDirection.x -= 0.02f;
            if (IsKeyDown(KEY_D)) beamDirection.x += 0.02f;
            if (IsKeyDown(KEY_Z)) beamDirection.z -= 0.02f;
            if (IsKeyDown(KEY_X)) beamDirection.z += 0.02f;
            beamDirection = Vector3Normalize(beamDirection); // Normaliser la direction
        }
        // Limiter les valeurs
        if (beamAngle < 0.1f) beamAngle = 0.1f;
        if (beamAngle > 1.5f) beamAngle = 1.5f;
        if (beamIntensity < 0.0f) beamIntensity = 0.0f;
        
        // Contrôles pour les vagues circulaires
        if (IsKeyPressed(KEY_V)) {
            enableWaves = !enableWaves;
            if (enableWaves) {
                waveStartTime = runTime; // Redémarrer les vagues au moment actuel
            }
        }
        
        // Contrôles pour déplacer le centre des ondulations
        if (IsKeyDown(KEY_LEFT_CONTROL)) {
            if (IsKeyDown(KEY_W)) waveCenter.z -= 0.1f;
            if (IsKeyDown(KEY_S)) waveCenter.z += 0.1f;
            if (IsKeyDown(KEY_A)) waveCenter.x -= 0.1f;
            if (IsKeyDown(KEY_D)) waveCenter.x += 0.1f;
        }
        
        // Contrôles pour ajuster l'amplitude des vagues
        if (IsKeyDown(KEY_LEFT_ALT)) {
            if (IsKeyDown(KEY_UP)) waveAmplitude += 0.01f;
            if (IsKeyDown(KEY_DOWN)) waveAmplitude -= 0.01f;
            if (waveAmplitude < 0.0f) waveAmplitude = 0.0f;
            if (waveAmplitude > 1.0f) waveAmplitude = 1.0f;
        }
        
        // Contrôles pour ajuster la durée des vagues
        if (IsKeyDown(KEY_LEFT_ALT)) {
            if (IsKeyDown(KEY_RIGHT)) waveDuration += 0.2f;
            if (IsKeyDown(KEY_LEFT)) waveDuration -= 0.2f;
            if (waveDuration < 1.0f) waveDuration = 1.0f;
            if (waveDuration > 20.0f) waveDuration = 20.0f;
        }
        
        // Contrôles pour ajuster le taux de dissipation des vagues
        if (IsKeyDown(KEY_RIGHT_ALT)) {
            if (IsKeyDown(KEY_UP)) waveDecayRate += 0.01f;    // Moins de dissipation
            if (IsKeyDown(KEY_DOWN)) waveDecayRate -= 0.01f;  // Plus de dissipation
            if (waveDecayRate < 0.5f) waveDecayRate = 0.5f;   // Minimum 50% (dissipation rapide)
            if (waveDecayRate > 0.99f) waveDecayRate = 0.99f; // Maximum 99% (quasi-persistence)
        }
        
        // Redémarrer les vagues avec la touche R
        if (IsKeyPressed(KEY_R)) {
            waveStartTime = runTime;
        }
        // La sphère émissive garde sa couleur orange fixe : {1.0f, 0.5f, 0.0f}
        // L'intensité de la lumière varie pour créer un effet vivant
        //lightIntensity = 0.5f;
        //std::cout << runTime << std::endl;
        // La couleur de lumière reste orange en permanence
        lightColor.x = 1.0f;  // Rouge fort
        lightColor.y = 0.5f;  // Vert moyen  
        lightColor.z = 0.0f;  // Pas de bleu = orange
        // Make light intensity oscillate between 0 and 2
        //lightIntensity = 1.0f + sinf(runTime * 1.5f);
        // Ajustement de l'intensité de la lumière
        //if (IsKeyDown(KEY_EQUAL)) lightIntensity += 0.2f;
        //if (IsKeyDown(KEY_MINUS) && lightIntensity > 0.2f) lightIntensity -= 0.2f;
        
        // Passage des valeurs des uniformes au shader
        float cameraPos[3] = { camera.position.x, camera.position.y, camera.position.z };
        float cameraTarget[3] = { 0.0f, 0.0f, 0.0f }; // On regarde toujours l'origine
        
        SetShaderValue(shader, viewEyeLoc, cameraPos, SHADER_UNIFORM_VEC3);
        SetShaderValue(shader, viewCenterLoc, cameraTarget, SHADER_UNIFORM_VEC3);
        SetShaderValue(shader, timeLoc, &runTime, SHADER_UNIFORM_FLOAT);
        // Animation de la sphère[0] pour simuler la chute puis la flottabilité sur l'eau
        static float sphereVelocity = 0.0f;
        static bool goingDown = true;
        static bool hasHitWater = false;        // Pour savoir si la sphère a touché l'eau
        static bool wavesTriggered = false;     // Pour activer les vagues une seule fois
        static float bounceStartTime = -1.0f;   // Début des rebonds
        static bool isFloating = false;         // Phase de flottaison

        // Paramètres physiques
        const float gravity = 10.0f;            // Gravité pour la chute libre
        const float waterSurface = -0.8f;      // Surface de l'eau où la sphère flotte
        const float waterBottom = -1.0f;       // Fond de l'eau
        const float triggerY = -1.0f;          // Seuil pour déclencher les vagues
        const float floatStrength = 3.0f;      // Force de flottaison
        const float damping = 0.7f;            // Amortissement des rebonds
        const float bounceDuration = 5.0f;     // Durée des rebonds de flottaison

        // Vérifier si la sphère atteint le seuil de déclenchement des vagues
        if (!wavesTriggered && spheres[0].position.y <= triggerY) {
            wavesTriggered = true;
            enableWaves = true;
            waveStartTime = runTime;
            waveCenter = (Vector3){spheres[0].position.x, -1.0f, spheres[0].position.z}; // Centre des vagues à la position de la sphère
        }

        // Phase 1: Chute libre jusqu'à la surface de l'eau
        if (!hasHitWater) {
            sphereVelocity += gravity * deltaTime;
            spheres[0].position.y -= sphereVelocity * deltaTime *2.f;
            
            // Quand la sphère atteint la surface de l'eau
            if (spheres[0].position.y <= waterSurface) {
                hasHitWater = true;
                isFloating = true;
                bounceStartTime = runTime;
                sphereVelocity = sphereVelocity * 0.5f; // Réduire la vitesse à l'impact
                spheres[0].position.y = waterSurface;
                goingDown = false; // Première remontée
            }
        }
        // Phase 2: Rebonds de flottaison pendant 2 secondes
        else if (isFloating && (runTime - bounceStartTime) < bounceDuration) {
            // Calculer le temps écoulé et le facteur d'amortissement progressif
            float elapsedBounceTime = runTime - bounceStartTime;
            float dampingProgress = elapsedBounceTime / bounceDuration; // 0 à 1
            float progressiveDamping = 1.0f - (dampingProgress * 0.8f); // Réduction progressive de 80%
            
            if (goingDown) {
                // Descente avec résistance de l'eau
                sphereVelocity += (gravity * 0.3f * progressiveDamping) * deltaTime;
                spheres[0].position.y -= sphereVelocity * deltaTime * 2.f;

                if (spheres[0].position.y <= waterBottom) {
                    spheres[0].position.y = waterBottom;
                    sphereVelocity = -sphereVelocity * damping * progressiveDamping;
                    goingDown = false;
                }
            } else {
                // Remontée par flottaison avec amortissement progressif
                sphereVelocity += -floatStrength * 2.0f * progressiveDamping * deltaTime;
                spheres[0].position.y -= sphereVelocity * deltaTime;

                // Quand elle atteint la surface, appliquer un freinage fort
                if (spheres[0].position.y >= waterSurface) {
                    spheres[0].position.y = waterSurface;
                    
                    // Amortissement très fort près de la fin pour stabiliser à la surface
                    float endDamping = damping * (0.3f + progressiveDamping * 0.2f); // Devient très faible
                    sphereVelocity = -sphereVelocity * endDamping;
                    
                    // Si la vitesse est très faible, arrêter complètement
                    if (abs(sphereVelocity) < 0.5f || dampingProgress > 0.8f) {
                        sphereVelocity = 0.0f;
                        // Forcer l'arrêt en passant à la phase 3
                        isFloating = false; // Cela déclenche la phase 3 immédiatement
                    } else {
                        goingDown = true;
                    }
                }
            }
        }
        // Phase 3: Arrêt des rebonds, sphère stabilisée à la surface
        else if (isFloating) {
            spheres[0].position.y = waterSurface;
            sphereVelocity = 0.0f;
        }
        // Envoi des données des sphères et des matériaux au shader
        // Note: Ces structures doivent être correctement alignées pour le GPU
        for (int i = 0; i < MAX_SPHERES; i++) {
            // Format vec4 pour chaque sphère (position + rayon)
            float sphereData[4] = { 
                spheres[i].position.x, 
                spheres[i].position.y, 
                spheres[i].position.z, 
                spheres[i].radius 
            };
            SetShaderValue(shader, GetShaderLocation(shader, TextFormat("spheres[%d]", i)), 
                           sphereData, SHADER_UNIFORM_VEC4);
                           
            // Transmission du matériau
            // Attention: ceci est une approche simplifiée, l'alignement peut poser problème
            // Pour un code plus robuste, considérer l'utilisation d'UBO/SSBO si disponible
            SetShaderValue(shader, GetShaderLocation(shader, TextFormat("materials[%d].type", i)), 
                          &materials[i].type, SHADER_UNIFORM_INT);
            SetShaderValue(shader, GetShaderLocation(shader, TextFormat("materials[%d].roughness", i)), 
                          &materials[i].roughness, SHADER_UNIFORM_FLOAT);
            SetShaderValue(shader, GetShaderLocation(shader, TextFormat("materials[%d].ior", i)), 
                          &materials[i].ior, SHADER_UNIFORM_FLOAT);
            SetShaderValue(shader, GetShaderLocation(shader, TextFormat("materials[%d].albedo", i)), 
                          &materials[i].albedo, SHADER_UNIFORM_VEC3);
        }
        // Envoi des données des blocs et de leurs matériaux au shader
        for (int i = 0; i < MAX_BLOCKS; i++) {
            // Format vec4 pour chaque bloc (position + taille)
            float blockPos[3] = {
        blocks[i].position.x, 
        blocks[i].position.y, 
        blocks[i].position.z
    };
    SetShaderValue(shader, GetShaderLocation(shader, TextFormat("blocks[%d]", i)),
                   blockPos, SHADER_UNIFORM_VEC3);
    
    // Transmettez la taille séparément
    float blockSize[3] = {
        blocks[i].size.x,
        blocks[i].size.y,
        blocks[i].size.z
    };
    SetShaderValue(shader, GetShaderLocation(shader, TextFormat("blockSizes[%d]", i)),
                   blockSize, SHADER_UNIFORM_VEC3);
            // Transmission du matériau du bloc
            SetShaderValue(shader, GetShaderLocation(shader, TextFormat("materials_block[%d].type", i)),
                          &materials_block[i].type, SHADER_UNIFORM_INT);
            SetShaderValue(shader, GetShaderLocation(shader, TextFormat("materials_block[%d].roughness", i)),
                            &materials_block[i].roughness, SHADER_UNIFORM_FLOAT);
            SetShaderValue(shader, GetShaderLocation(shader, TextFormat("materials_block[%d].ior", i)),
                            &materials_block[i].ior, SHADER_UNIFORM_FLOAT);
            SetShaderValue(shader, GetShaderLocation(shader, TextFormat("materials_block[%d].albedo", i)),
                            &materials_block[i].albedo, SHADER_UNIFORM_VEC3);
        }
        
        // Mise à jour de la position de la lumière
        SetShaderValue(shader, lightPosLoc, &lightPos, SHADER_UNIFORM_VEC3);
        SetShaderValue(shader, lightColorLoc, &lightColor, SHADER_UNIFORM_VEC3);
        SetShaderValue(shader, lightIntensityLoc, &lightIntensity, SHADER_UNIFORM_FLOAT);
        
        // Mise à jour des paramètres du faisceau
        SetShaderValue(shader, beamDirectionLoc, &beamDirection, SHADER_UNIFORM_VEC3);
        SetShaderValue(shader, beamPositionLoc, &beamPosition, SHADER_ATTRIB_VEC3);
        SetShaderValue(shader, beamColorLoc, &beamColor, SHADER_ATTRIB_VEC3);
        SetShaderValue(shader, beamAngleLoc, &beamAngle, SHADER_UNIFORM_FLOAT);
        SetShaderValue(shader, beamIntensityLoc, &beamIntensity, SHADER_UNIFORM_FLOAT);
        SetShaderValue(shader, enableBeamLoc, &enableBeam, SHADER_UNIFORM_INT);
        
        // Mise à jour des paramètres des vagues
        SetShaderValue(shader, waveCenterLoc, &waveCenter, SHADER_UNIFORM_VEC3);
        SetShaderValue(shader, enableWavesLoc, &enableWaves, SHADER_UNIFORM_INT);
        SetShaderValue(shader, waveDurationLoc, &waveDuration, SHADER_UNIFORM_FLOAT);
        SetShaderValue(shader, waveAmplitudeLoc, &waveAmplitude, SHADER_UNIFORM_FLOAT);
        SetShaderValue(shader, waveStartTimeLoc, &waveStartTime, SHADER_UNIFORM_FLOAT);
        SetShaderValue(shader, waveDecayRateLoc, &waveDecayRate, SHADER_UNIFORM_FLOAT);
        
        //liaison entre les textures et les shaders
        SetShaderValueTexture(denoise_shader, GetShaderLocation(denoise_shader, "renderNoisy"), renderNoisy.texture);
        SetShaderValueTexture(denoise_shader, GetShaderLocation(denoise_shader, "renderNormals"), renderNormals.texture);
        SetShaderValueTexture(denoise_shader, GetShaderLocation(denoise_shader, "renderHistory"), renderHistory.texture);

        //pour le taa shader
        SetShaderValue(taa_shader, GetShaderLocation(taa_shader, "resolution"), resolution, SHADER_UNIFORM_VEC2);
        SetShaderValue(taa_shader, GetShaderLocation(taa_shader, "time"), &runTime, SHADER_UNIFORM_FLOAT);
        SetShaderValue(taa_shader, GetShaderLocation(taa_shader, "frame"), &frameCounter, SHADER_UNIFORM_INT);

        SetShaderValueTexture(taa_shader, GetShaderLocation(taa_shader, "currentFrame"), denoiseTarget.texture);
        SetShaderValueTexture(taa_shader, GetShaderLocation(taa_shader, "historyFrame"), renderHistory.texture);


        // Vérification si la fenêtre est redimensionnée
        if (IsWindowResized()) {
            resolution[0] = (float)GetScreenWidth();
            resolution[1] = (float)GetScreenHeight();
            SetShaderValue(shader, resolutionLoc, resolution, SHADER_UNIFORM_VEC2);
        }
        
        // Dessin
        BeginTextureMode(renderNoisy);       // Enable drawing to texture
                          // End drawing to texture (now we have a texture available for next passes)
        
        //BeginDrawing();
        //BeginDrawing(); // Start 3d mode drawing
            //ClearBackground(BLACK);
            
            // On dessine simplement un rectangle plein écran blanc,
            // l'image est générée dans le shader de raytracing
            BeginShaderMode(shader);
                DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), WHITE);
            EndShaderMode();
            //EndDrawing();
            
        //EndDrawing();
        
        EndTextureMode();


            BeginTextureMode(denoiseTarget); // ← on dessine dans denoiseTarget (frame courante débruitée)
                BeginShaderMode(denoise_shader);
                    // Uniformes
                    float resolution[2] = { (float)GetScreenWidth(), (float)GetScreenHeight() };
                    SetShaderValue(denoise_shader, GetShaderLocation(denoise_shader, "resolution"), resolution, SHADER_UNIFORM_VEC2);

                    SetShaderValue(denoise_shader, GetShaderLocation(denoise_shader, "time"), &runTime, SHADER_UNIFORM_FLOAT);
                    SetShaderValue(denoise_shader, GetShaderLocation(denoise_shader, "frame"), &frameCounter, SHADER_UNIFORM_INT);

                    float denoiseStrength = 1.0f;
                    SetShaderValue(denoise_shader, GetShaderLocation(denoise_shader, "u_denoiseStrength"), &denoiseStrength, SHADER_UNIFORM_FLOAT);

                    // Textures (attention aux noms !)
                    SetShaderValueTexture(denoise_shader, GetShaderLocation(denoise_shader, "renderNoisy"), renderNoisy.texture);
                    SetShaderValueTexture(denoise_shader, GetShaderLocation(denoise_shader, "renderNormals"), renderNormals.texture);
                    SetShaderValueTexture(denoise_shader, GetShaderLocation(denoise_shader, "renderHistory"), renderHistory.texture);

                    // Dessiner un quad plein écran pour appliquer le shader
                    DrawTexturePro(
                        renderNoisy.texture,                       // source texture (image bruitée)
                        (Rectangle){ 0, 0, (float)screenWidth, -(float)screenHeight },
                        (Rectangle){ 0, 0, (float)screenWidth, (float)screenHeight },
                        (Vector2){ 0, 0 },
                        0.0f,
                        WHITE
                    );
                EndShaderMode();
            EndTextureMode();

// Application du TAA à la texture de sortie finale
BeginTextureMode(taaOutput);  // Capture le résultat du TAA dans taaOutput
    BeginShaderMode(taa_shader);
        // Passer la texture courante (débruitée) et la frame précédente
        SetShaderValueTexture(taa_shader, GetShaderLocation(taa_shader, "currentFrame"), denoiseTarget.texture);
        SetShaderValueTexture(taa_shader, GetShaderLocation(taa_shader, "historyFrame"), renderHistory.texture);

        // Uniformes nécessaires
        SetShaderValue(taa_shader, GetShaderLocation(taa_shader, "time"), &runTime, SHADER_UNIFORM_FLOAT);
        SetShaderValue(taa_shader, GetShaderLocation(taa_shader, "frame"), &frameCounter, SHADER_UNIFORM_INT);

        DrawTexturePro(
            denoiseTarget.texture,
            (Rectangle){ 0, 0, (float)screenWidth, -(float)screenHeight },
            (Rectangle){ 0, 0, (float)screenWidth, (float)screenHeight },
            (Vector2){ 0, 0 },
            0.0f,
            WHITE
        );
    EndShaderMode();
EndTextureMode();
//pour enlever les artefacts de la frame précédente
if (frameCounter % 3 == 0) {
    BeginTextureMode(renderHistory);
        // On écrase totalement l'historique avec l'image courante (nettoyée)
        DrawTextureRec(
            denoiseTarget.texture,
            (Rectangle){ 0, 0, (float)screenWidth, -(float)screenHeight },
            (Vector2){ 0, 0 },
            WHITE
        );
    EndTextureMode();
}

            //pour la derniere image
            BeginTextureMode(renderHistory);
                DrawTextureRec(
                        taaOutput.texture,
                        (Rectangle){ 0, 0, (float)screenWidth, -(float)screenHeight },
                        (Vector2){ 0, 0 },
                        WHITE
                    );
                EndTextureMode();
                
BeginDrawing();
    //ClearBackground(BLACK); //faut pas mettre ça sinon ça assombrit l'image

    // Dessiner le résultat du TAA
    DrawTextureRec(
        taaOutput.texture,
        (Rectangle){ 0, 0, (float)screenWidth, -(float)screenHeight },
        (Vector2){ 0, 0 },
        WHITE
    );
    
    // Affichage d'informations
    DrawFPS(10, 10);
    DrawText(TextFormat("Light Intensity: %.1f", lightIntensity), 10, 30, 20, WHITE);
    DrawText(TextFormat("Beam: %s | Angle: %.2f | Intensity: %.1f", 
             enableBeam ? "ON" : "OFF", beamAngle, beamIntensity), 10, 50, 20, WHITE);
    DrawText(TextFormat("Waves: %s | Amp: %.2f | Dur: %.1fs | Decay: %.0f%%", 
             enableWaves ? "ON" : "OFF", waveAmplitude, waveDuration, waveDecayRate * 100), 10, 70, 20, WHITE);
    
    // Calculer le temps restant pour les vagues
    float elapsedTime = runTime - waveStartTime;
    float timeLeft = waveDuration - elapsedTime;
    if (enableWaves && timeLeft > 0) {
        DrawText(TextFormat("Wave time left: %.1fs", timeLeft), 10, 90, 20, WHITE);
    } else if (enableWaves && elapsedTime > waveDuration) {
        float fadeTime = 2.0f;
        float fadeLeft = fadeTime - (elapsedTime - waveDuration);
        if (fadeLeft > 0) {
            DrawText(TextFormat("Fading out: %.1fs", fadeLeft), 10, 90, 20, YELLOW);
        } else {
            DrawText("Waves stopped", 10, 90, 20, GRAY);
        }
    }
    
    DrawText("Controls:", 10, GetScreenHeight() - 170, 20, WHITE);
    DrawText("  Mouse Right - Rotate camera", 10, GetScreenHeight() - 150, 20, WHITE);
    DrawText("  Mouse Wheel - Zoom in/out", 10, GetScreenHeight() - 130, 20, WHITE);
    DrawText("  H/K/U/J/Y/I - Move light", 10, GetScreenHeight() - 110, 20, WHITE);
    DrawText("  B - Toggle beam | Q/E - Beam angle | T/G - Beam intensity", 10, GetScreenHeight() - 90, 20, WHITE);
    DrawText("  V - Toggle waves | R - Restart waves | Ctrl + WASD - Move center", 10, GetScreenHeight() - 70, 20, WHITE);
    DrawText("  Alt + Up/Down - Wave amplitude | Alt + Left/Right - Duration", 10, GetScreenHeight() - 50, 20, WHITE);
    DrawText("  Right Alt + Up/Down - Decay rate (persistence)", 10, GetScreenHeight() - 30, 20, WHITE);
    DrawText("  Shift + WASD/ZX - Beam direction", 10, GetScreenHeight() - 10, 20, WHITE);
EndDrawing();

        frameCounter++;

    }
    
    // Nettoyage
    UnloadShader(shader);
    UnloadShader(denoise_shader);
    UnloadShader(taa_shader);
    UnloadRenderTexture(target); // Unload render texture
    UnloadRenderTexture(renderNoisy);
    UnloadRenderTexture(renderNormals);
    UnloadRenderTexture(renderHistory);
    UnloadRenderTexture(denoiseTarget);
    CloseWindow();
    
    return 0;
}