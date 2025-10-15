#version 330
#define MAX_SPHERES 8
#define MAX_BLOCKS 6
#define MAX_BOUNCES 5  // Augmenté pour plus de réalisme
#define MAX_SAMPLES 8  // Anti-aliasing
#define PI 3.14159265

// Structures de matériaux
#define MAT_DIFFUSE 0
#define MAT_METALLIC 1
#define MAT_GLASS 2
#define MAT_EMISSIVE 3
#define MAT_MIRROR 4
#define MAT_ZONE_EMISSION 5
#define MAT_EAU 6


// Structure pour les matériaux, alignée sur vec4 pour la compatibilité avec des uniformes
struct Material {
    int type;       // Type de matériau
    float roughness; // Rugosité (métal, verre)
    float ior;      // Indice de réfraction (verre)
    float padding;  // Padding pour l'alignement
    vec3 albedo;    // Couleur de base
    float padding2; // Padding supplémentaire
};

struct Hit {
    bool hit;
    float t;
    vec3 normal;
    Material matId;
    int blockId;
};

uniform vec4 spheres[MAX_SPHERES];     // xyz = position, w = rayon
uniform Material materials[MAX_SPHERES]; // Matériaux des sphères
uniform int sphereCount;

uniform vec3 blocks[MAX_BLOCKS]; // Positions des blocs (pour les murs)
uniform vec3 blockSizes[MAX_BLOCKS]; // Tailles des blocs
uniform Material materials_block[MAX_BLOCKS]; // Matériaux des murs
uniform int blockCount;
//pour les lumières sur les murs
uniform vec3 emission_block[MAX_BLOCKS]; // intensité RGB de lumière émise par le bloc

uniform vec3 lightPos;
uniform vec3 lightColor;
uniform float lightIntensity;
uniform vec2 resolution;
uniform vec3 viewEye;
uniform vec3 viewCenter;
uniform float time;     // Pour le bruit

// Uniformes pour les vagues circulaires
uniform vec3 waveCenter;    // Centre des ondulations
uniform int enableWaves;    // Activer/désactiver les vagues
uniform float waveDuration; // Durée des vagues (en secondes)
uniform float waveAmplitude; // Amplitude des vagues
uniform float waveStartTime; // Moment où les vagues ont commencé
uniform float waveDecayRate; // Taux de dissipation par seconde

uniform sampler2D previousFrame;
uniform float frameBlend; // 0.1 to 0.2 works well


out vec4 finalColor;

// Hash function pour générer des nombres pseudo-aléatoires
uint hash(uint x) {
    x = x * 1664525u + 1013904223u;
    x ^= x >> 16u;
    x *= 0x3dba2d8du;
    x ^= x >> 16u;
    return x;
}

float random(vec3 pos, float seed) {
    uint h = hash(uint(pos.x * 8192.0) ^ hash(uint(pos.y * 8192.0) ^ hash(uint(pos.z * 8192.0) ^ hash(uint(seed * 91.237)))));
    return float(h) / 4294967296.0;
}

vec2 randomVec2(vec3 pos, float seed) {
    return vec2(
        random(pos, seed),
        random(pos, seed + 1.618)
    );
}

// Échantillonnage cosinus pondéré pour une meilleure distribution
vec3 sampleHemisphere(vec3 normal, vec3 pos, float seed) {
    vec2 rand = randomVec2(pos, seed);
    
    float phi = 2.0 * PI * rand.x;
    float cosTheta = sqrt(rand.y);  // Distribution en cosinus
    float sinTheta = sqrt(1.0 - cosTheta * cosTheta);
    
    vec3 dir = vec3(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);
    
    // Créer une base orthonormée alignée avec la normale
    vec3 up = abs(normal.z) < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
    vec3 tangent = normalize(cross(up, normal));
    vec3 bitangent = cross(normal, tangent);
    mat3 tbn = mat3(tangent, bitangent, normal);
    
    return normalize(tbn * dir);
}

// Réflexion spéculaire avec perturbation pour rugosité
vec3 reflect_custom(vec3 incident, vec3 normal, float roughness, vec3 pos, float seed) {
    vec3 reflected = reflect(incident, normal);
    
    if (roughness > 0.0) {
        vec2 rand = randomVec2(pos, seed);
        float phi = 2.0 * PI * rand.x;
        float cosTheta = pow(1.0 - rand.y * roughness * roughness, 1.0 / 3.0);
        float sinTheta = sqrt(1.0 - cosTheta * cosTheta);
        
        vec3 scatter = vec3(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);
        
        vec3 up = abs(reflected.z) < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
        vec3 tangent = normalize(cross(up, reflected));
        vec3 bitangent = cross(reflected, tangent);
        mat3 tbn = mat3(tangent, bitangent, reflected);
        
        return normalize(tbn * scatter);
    }
    
    return reflected;
}

// Réfraction avec loi de Fresnel et perturbation pour rugosité
vec3 refract(vec3 incident, vec3 normal, float ior, float roughness, vec3 pos, float seed, out float reflectionChance) {
    float eta = dot(incident, normal) < 0.0 ? 1.0 / ior : ior;
    vec3 n = dot(incident, normal) < 0.0 ? normal : -normal;
    
    float cosI = abs(dot(incident, n));
    float sinT2 = eta * eta * (1.0 - cosI * cosI);
    
    // Réflexion totale interne
    if (sinT2 > 1.0) {
        reflectionChance = 1.0;
        return reflect_custom(incident, n, roughness, pos, seed);
    }
    
    float cosT = sqrt(1.0 - sinT2);
    
    // Approximation de Schlick pour Fresnel
    float r0 = ((1.0 - eta) / (1.0 + eta)) * ((1.0 - eta) / (1.0 + eta));
    float fresnel = r0 + (1.0 - r0) * pow(1.0 - cosI, 5.0);
    
    reflectionChance = fresnel;
    
    if (random(pos, seed + 4.269) < fresnel) {
        return reflect_custom(incident, n, roughness, pos, seed);
    }
    
    vec3 refracted = normalize(eta * incident + (eta * cosI - cosT) * n);
    
    if (roughness > 0.0) {
        vec2 rand = randomVec2(pos, seed + 2.718);
        float phi = 2.0 * PI * rand.x;
        float cosTheta = pow(1.0 - rand.y * roughness * roughness, 1.0 / 2.0);
        float sinTheta = sqrt(1.0 - cosTheta * cosTheta);
        
        vec3 scatter = vec3(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);
        
        vec3 up = abs(refracted.z) < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
        vec3 tangent = normalize(cross(up, refracted));
        vec3 bitangent = cross(refracted, tangent);
        mat3 tbn = mat3(tangent, bitangent, refracted);
        
        return normalize(tbn * scatter);
    }
    
    return refracted;
}

bool intersectSphere(vec3 ro, vec3 rd, vec4 sphere, out float t, out vec3 n) {
    vec3 oc = ro - sphere.xyz;
    float b = dot(oc, rd);
    float c = dot(oc, oc) - sphere.w * sphere.w;
    float h = b*b - c;
    
    if (h < 0.0) return false;
    
    h = sqrt(h);
    t = -b - h;
    
    if (t < 0.001) t = -b + h;
    if (t < 0.001) return false;
    
    vec3 hit = ro + t * rd;
    n = normalize(hit - sphere.xyz);
    
    return true;
}

// Fonction pour calculer la hauteur des vagues circulaires
float calculateWaveHeight(vec3 pos, vec3 waveCenter, float time) {
    float distance = length(pos.xz - waveCenter.xz);
    float waveSpeed = 2.0;  // Vitesse de propagation des ondes
    float waveFreq = 3.0;   // Fréquence des ondulations
    
    // Calcul du temps écoulé depuis le début des vagues
    float elapsedTime = time - waveStartTime;
    
    // Atténuation temporelle progressive utilisant le paramètre waveDecayRate
    float timeAttenuation = 1.0;
    if (elapsedTime > waveDuration) {
        // Après la durée active, on laisse les vagues existantes se dissiper naturellement
        // mais on n'en génère plus de nouvelles
        
        // Calculer à quelle distance cette onde a été générée
        float waveAge = (distance / waveSpeed); // Temps qu'il a fallu à cette onde pour arriver ici
        float waveGenerationTime = elapsedTime - waveAge; // Moment où cette onde a été générée
        
        // Si cette onde a été générée après waveDuration, elle n'existe pas
        if (waveGenerationTime > waveDuration) {
            return 0.0; // Pas de nouvelles vagues après le temps imparti
        }
        
        // Sinon, cette onde existe mais se dissipe progressivement
        float overtimeSeconds = elapsedTime - waveDuration;
        timeAttenuation = pow(waveDecayRate, overtimeSeconds);
    } else if (elapsedTime < 0.0) {
        // Si les vagues n'ont pas encore commencé
        timeAttenuation = 0.0;
    } else {
        // Pendant la durée active, réduction légère pour effet naturel
        float naturalDecay = mix(0.98, 1.0, waveDecayRate); // Plus le decay est fort, plus la réduction naturelle est faible
        timeAttenuation = pow(naturalDecay, elapsedTime);
    }
    
    // Atténuation avec la distance (plus progressive)
    float distanceAttenuation = exp(-distance * 0.15); // Réduit le coefficient pour une portée plus longue
    
    // Atténuation supplémentaire pour les vagues très éloignées (après 10 unités)
    float farDistanceAttenuation = 1.0;
    if (distance > 10.0) {
        float excessDistance = distance - 10.0;
        farDistanceAttenuation = exp(-excessDistance * 0.5); // Atténuation rapide au-delà de 10 unités
    }
    
    // Vérifier si l'onde a eu le temps d'atteindre cette distance
    float timeToReachDistance = distance / waveSpeed;
    if (elapsedTime < timeToReachDistance) {
        return 0.0; // L'onde n'est pas encore arrivée à cette distance
    }
    
    // Atténuation temporelle des ondulations individuelles (vagues vieillissent)
    float waveAge = elapsedTime - timeToReachDistance; // Temps depuis que l'onde est arrivée ici
    float ageAttenuation = exp(-waveAge * 0.1); // Les vagues s'affaiblissent en vieillissant
    
    // Onde circulaire qui se propage avec toutes les attenuations
    // Utiliser elapsedTime au lieu de time pour que les vagues partent du centre
    float wave = sin(waveFreq * distance - waveSpeed * (waveStartTime + elapsedTime)) 
                 * distanceAttenuation 
                 * farDistanceAttenuation
                 * timeAttenuation 
                 * ageAttenuation;
    
    // Amplitude finale avec réduction progressive globale
    float finalAmplitude = waveAmplitude * timeAttenuation;
    
    return wave * finalAmplitude;
}

// Fonction pour calculer la normale déformée par les vagues
vec3 calculateWaveNormal(vec3 pos, vec3 waveCenter, float time) {
    float eps = 0.01;
    
    // Échantillonner la hauteur en 4 points autour de la position
    float h0 = calculateWaveHeight(pos, waveCenter, time);
    float hx = calculateWaveHeight(pos + vec3(eps, 0, 0), waveCenter, time);
    float hz = calculateWaveHeight(pos + vec3(0, 0, eps), waveCenter, time);
    
    // Calculer le gradient (dérivées partielles)
    vec3 tangentX = vec3(eps, hx - h0, 0);
    vec3 tangentZ = vec3(0, hz - h0, eps);
    
    // Normale = produit vectoriel des tangentes
    vec3 normal = normalize(cross(tangentX, tangentZ));
    return normal;
}

// Fonction d'intersection pour les boîtes alignées sur les axes (AABB) avec vagues
bool intersectBox(vec3 ro, vec3 rd, vec3 boxMin, vec3 boxMax, out float t, out vec3 n) {
    vec3 invDir = 1.0 / rd;
    vec3 t0s = (boxMin - ro) * invDir;
    vec3 t1s = (boxMax - ro) * invDir;
    
    vec3 tsmaller = min(t0s, t1s);
    vec3 tbigger = max(t0s, t1s);
    
    float tmin = max(max(tsmaller.x, tsmaller.y), tsmaller.z);
    float tmax = min(min(tbigger.x, tbigger.y), tbigger.z);
    
    if (tmin > tmax || tmax < 0.001) return false;
    
    t = tmin > 0.001 ? tmin : tmax;
    if (t < 0.001) return false;
    
    // Point d'intersection initial
    vec3 hit = ro + rd * t;
    vec3 center = (boxMin + boxMax) * 0.5;
    vec3 d = abs(hit - center) - (boxMax - boxMin) * 0.5;
    
    // Vérifier si on touche la surface supérieure (eau) ET si les vagues sont activées
    bool isTopSurface = (d.y > d.x && d.y > d.z && hit.y > center.y);
    
    if (isTopSurface && enableWaves == 1) {
        // Utiliser le centre des ondulations défini par l'uniform
        vec3 waveCenterPos = waveCenter;
        
        // Itération pour trouver l'intersection précise avec la surface déformée
        float rayT = t;
        for (int iter = 0; iter < 8; iter++) {
            vec3 currentPos = ro + rd * rayT;
            
            // Hauteur de la vague à cette position
            float waveHeight = calculateWaveHeight(currentPos, waveCenterPos, time);
            
            // Surface déformée
            float surfaceY = boxMax.y + waveHeight;
            
            // Erreur entre la position du rayon et la surface
            float error = currentPos.y - surfaceY;
            
            // Ajuster t pour converger vers la surface
            rayT -= error / rd.y;
            
            // Convergence suffisante
            if (abs(error) < 0.001) break;
        }
        
        t = rayT;
        hit = ro + rd * t;
        
        // Calculer la normale déformée par les vagues
        n = calculateWaveNormal(hit, waveCenterPos, time);
    } else {
        // Faces normales (non déformées)
        if (d.x > d.y && d.x > d.z) {
            n = vec3(sign(hit.x - center.x), 0.0, 0.0);
        } else if (d.y > d.z) {
            n = vec3(0.0, sign(hit.y - center.y), 0.0);
        } else {
            n = vec3(0.0, 0.0, sign(hit.z - center.z));
        }
    }
    
    return true;
}

Hit intersectScene(vec3 ro, vec3 rd) {
    Hit closestHit;
    closestHit.hit = false;
    closestHit.t = 1e9;

    for (int i = 0; i < blockCount; ++i) {
        float t;
        vec3 n;
        vec3 boxMin = blocks[i];
        vec3 boxMax = blocks[i] + blockSizes[i];

        if (intersectBox(ro, rd, boxMin, boxMax, t, n)) {
            if (t < closestHit.t) {
                closestHit.hit = true;
                closestHit.t = t;
                closestHit.normal = n;
                closestHit.blockId = i;
                closestHit.matId = materials_block[i];
            }
        }
    }

    return closestHit;
}

// Fonction auxiliaire pour calculer l'éclairage direct
vec3 directLight(vec3 p, vec3 n, vec3 viewDir, int matType, vec3 albedo, float roughness, float dist) {
    vec3 toLight = normalize(lightPos - p);
    float distToLight = length(lightPos - p);
    
    // Calculer l'intensité du faisceau pour ce point
    //float beamIntensity = calculateBeamIntensity(p);
    
    // Vérifier si le point est dans l'ombre
    bool occluded = false;
    for (int i = 0; i < sphereCount; ++i) {
        float t;
        vec3 tmp;
        if (intersectSphere(p + n * 0.001, toLight, spheres[i], t, tmp)) {
            if (t < distToLight) {
                occluded = true;
                break;
            }
        }
    }

    // Vérifier les ombres avec les murs
    if (!occluded) {
        for (int i = 0; i < blockCount; ++i) {
            float t;
            vec3 tmp;
            vec3 halfSize = blockSizes[i] * 0.5;
            vec3 blockMin = blocks[i] - halfSize;
            vec3 blockMax = blocks[i] + halfSize;

            if (intersectBox(p + n * 0.001, toLight, blockMin, blockMax, t, tmp)) {
                if (t < distToLight) {
                    occluded = true;
                    break;
                }
            }
        }
    }
    
    
    if (occluded) return vec3(0.0);
    
    // Atténuation de la lumière
    float attenuation = lightIntensity / (1.0 + 0.1 * distToLight + 0.01 * distToLight * distToLight); //bloque la lumière à 2
    
    // Ajouter l'intensité du faisceau
    //attenuation += beamIntensity;
    
    // Différents modèles d'éclairage en fonction du matériau
    vec3 lightContrib = vec3(0.0);
    
    if (matType == MAT_DIFFUSE) {
        // Modèle d'éclairage Lambert pour surfaces diffuses
        float diff = max(dot(n, toLight), 0.0);
        lightContrib = albedo * lightColor * diff * attenuation;
        
        // Ajout d'une légère composante ambiante
        lightContrib += albedo * lightColor * 0.05;
    }
    else if (matType == MAT_METALLIC) {
        // Modèle d'éclairage Phong/Blinn-Phong pour surfaces métalliques
        float diff = max(dot(n, toLight), 0.0);
        vec3 halfwayDir = normalize(toLight + viewDir);
        float spec = pow(max(dot(n, halfwayDir), 0.0), (1.0 - roughness) * 128.0 + 1.0);
        
        lightContrib = albedo * lightColor * diff * attenuation;
        lightContrib += albedo * lightColor * spec * (1.0 - roughness) * attenuation;
    }
    else if (matType == MAT_GLASS) {
        // Pour le verre on inclut principalement la composante spéculaire
        vec3 reflectDir = reflect(-toLight, n);
        float spec = pow(max(dot(viewDir, reflectDir), 0.0), (1.0 - roughness) * 128.0 + 1.0);
        
        lightContrib = lightColor * spec * (1.0 - roughness) * attenuation;
    }
    else if (matType == MAT_MIRROR) {
        // Pour les miroirs, on utilise la réflexion
        //on renvoie les photon avec l'angle inverse qu'ils ont arrivés
        vec3 reflectDir = reflect(-viewDir, n);
        float spec = pow(max(dot(reflectDir, toLight), 0.0), (1.0 - roughness) * 128.0 + 1.0);
        lightContrib = lightColor * spec * (1.0 - roughness) * attenuation;

    }
    
    return lightContrib;
}

//fonction d'échantillonnage direct de la lumière
vec3 sampleDirectLight(vec3 p, vec3 n, vec3 viewDir, Material mat, float seed) {
    // Éviter l'auto-intersection avec un petit décalage
    vec3 origin = p + n * 0.001;
    vec3 contrib = vec3(0.0);
    
    // Trouver les sources de lumière émissives (sphères)
    for (int i = 0; i < sphereCount; ++i) {
        if (materials[i].type == MAT_EMISSIVE) {
            // Échantillonnage de la sphère lumineuse
            vec3 lightCenter = spheres[i].xyz;
            float lightRadius = spheres[i].w;
            float distToLight = length(lightCenter - p);
            
            // Génération d'un point aléatoire sur la sphère lumineuse
            vec2 rand = randomVec2(p, seed + float(i) * 0.773);
            float phi = 2.0 * PI * rand.x;
            float cosTheta = 2.0 * rand.y - 1.0;
            float sinTheta = sqrt(1.0 - cosTheta * cosTheta);
            
            vec3 sampleOffset = lightRadius * vec3(
                cos(phi) * sinTheta,
                sin(phi) * sinTheta,
                cosTheta
            );
            
            vec3 lightPos = lightCenter + sampleOffset;
            vec3 toLight = normalize(lightPos - p);
            
            // Vérifier la visibilité (ombres)
            bool occluded = false;
            for (int j = 0; j < sphereCount; ++j) {
                if (j == i) continue; // Ignorer la source
                float t;
                vec3 tmp;
                if (intersectSphere(origin, toLight, spheres[j], t, tmp)) {
                    if (t < distToLight) {
                        occluded = true;
                        break;
                    }
                }
            }
            
            if (!occluded) {
                // Calculer la contribution de cette lumière
                float solidAngle = 2.0 * PI * (1.0 - sqrt(1.0 - (lightRadius*lightRadius)/(distToLight*distToLight)));
                float cosLight = max(0.0, dot(n, toLight));
                
                // BRDF selon le matériau
                vec3 brdf = vec3(0.0);
                if (mat.type == MAT_DIFFUSE) {
                    brdf = mat.albedo / PI; // Lambert
                }
                else if (mat.type == MAT_METALLIC) {
                    vec3 halfwayDir = normalize(toLight + viewDir);
                    float spec = pow(max(dot(n, halfwayDir), 0.0), (1.0 - mat.roughness) * 128.0 + 1.0);
                    brdf = (mat.albedo + spec * (1.0 - mat.roughness)) / PI;
                }
                else if (mat.type == MAT_GLASS) {
                    // Approximation simple pour le verre
                    vec3 reflectDir = reflect(-toLight, n);
                    float spec = pow(max(dot(viewDir, reflectDir), 0.0), (1.0 - mat.roughness) * 128.0 + 1.0);
                    brdf = vec3(spec * (1.0 - mat.roughness)) / PI;
                }
                else if (mat.type == MAT_MIRROR) {
                    // Pour un miroir parfait, on vérifie si la direction réfléchie pointe vers la source
                    vec3 reflectDir = reflect(-viewDir, n);
                    vec3 toReflectedLight = normalize(lightPos - p);
                    
                    // Vérifier si la direction réfléchie est alignée avec la direction vers la lumière
                    float alignment = dot(normalize(reflectDir), toReflectedLight);
                    
                    // Seuil pour considérer l'alignement (plus strict pour un miroir parfait)
                    float threshold = 0.999; // Très strict pour un miroir parfait
                    
                    if (alignment > threshold) {
                        // Réflexion parfaite : la BRDF est 1/cos(theta) pour compenser la géométrie
                        float cosTheta = max(0.0, dot(n, toReflectedLight));
                        brdf = mat.albedo / max(cosTheta, 0.001); // Éviter division par 0
                    } else {
                        brdf = vec3(0.0); // Pas de contribution si pas d'alignement parfait
                    }
                }
                else if (mat.type == MAT_EAU) {
                    // Pour l'eau calme, comportement similaire au miroir avec teinte d'eau
                    vec3 reflectDir = reflect(-viewDir, n);
                    vec3 toReflectedLight = normalize(lightPos - p);
                    
                    float alignment = dot(normalize(reflectDir), toReflectedLight);
                    float threshold = 0.995; // Légèrement moins strict que le miroir
                    
                    if (alignment > threshold) {
                        float cosTheta = max(0.0, dot(n, toReflectedLight));
                        // Teinte bleu-vert de l'eau sur la réflexion
                        brdf = vec3(0.8, 0.9, 1.0) / max(cosTheta, 0.001);
                    } else {
                        brdf = vec3(0.0);
                    }
                }
                // Calcul du PDF
                float distance2 = dot(lightPos - p, lightPos - p);
                float cosTheta = max(dot(toLight, -normalize(sampleOffset)), 0.0);
                float pdf = distance2 / (cosTheta * 4.0 * PI * lightRadius * lightRadius + 0.001); // éviter /0

                // Contribution lumineuse si pdf valide
                if (pdf > 0.0) {
                    vec3 Li = materials[i].albedo * lightIntensity;
                    float cosLight = max(0.0, dot(n, toLight));
                    contrib += brdf * Li * cosLight / pdf;
                }
            }
        }
    }
    
    return contrib;
}

// Fonction hash 2D rapide pour du bruit pseudo-aléatoire
float hash21(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float emissionPattern(vec3 hitPos, vec3 blockMin, vec3 blockMax, float time) {
    // Coordonnées locales (0..1) sur le mur en X et Y (ou X et Z selon orientation)
    float localX = (hitPos.x - blockMin.x) / (blockMax.x - blockMin.x);
    float localY = (hitPos.y - blockMin.y) / (blockMax.y - blockMin.y);

    // Vitesse de défilement dans chaque direction
    vec2 speed = vec2(0.03, 0.05);

    // Coordonnées animées (défilement dans x et y)
    vec2 uv = vec2(localX, localY) + speed * time;

    // Appliquer fract pour avoir un motif qui boucle sur [0,1]
    uv = fract(uv);

    // Échantillonnage du bruit
    float noiseVal = hash21(floor(uv * 20.0)); // 20 = résolution du motif

    // Seuil pour "allumer" la lumière dans certaines zones
    float threshold = 0.8;

    // Retourne 1 si bruit au-dessus du seuil, sinon 0
    float emission = step(threshold, noiseVal);

    // On peut lisser un peu la transition
    emission = smoothstep(threshold, threshold + 0.1, noiseVal);

    return emission;
}



vec3 trace(vec3 ro, vec3 rd, float seed) {
    vec3 col = vec3(0.0);
    vec3 throughput = vec3(1.0);

    for (int bounce = 0; bounce < MAX_BOUNCES; ++bounce) {
        float minT = 1e9;
        int hitIdx = -1;
        int hitType = 0; // 0 = sphère, 1 = mur
        vec3 n, hit;
        
        // Trouver l'intersection la plus proche
        for (int i = 0; i < sphereCount; ++i) {
            float t;
            vec3 ni;
            if (intersectSphere(ro, rd, spheres[i], t, ni)) {
                if (t < minT) {
                    minT = t;
                    hit = ro + rd * t;
                    n = ni;
                    hitIdx = i;
                    hitType = 0;
                }
            }
        }

        // Trouver l'intersection la plus proche avec les murs
        for (int i = 0; i < blockCount; ++i) {
            float t;
            vec3 ni;
            vec3 halfSize = blockSizes[i] * 0.5;
            vec3 blockMin = blocks[i] - halfSize;
            vec3 blockMax = blocks[i] + halfSize;

            if (intersectBox(ro, rd, blockMin, blockMax, t, ni)) {
                if (t < minT) {
                    minT = t;
                    hit = ro + rd * t;
                    n = ni;
                    hitIdx = i;
                    hitType = 1;
                }
            }
        }

        // Si pas d'intersection, ajouter un fond dégradé et sortir
        if (hitIdx == -1) {
            // Effet volumétrique du faisceau dans l'air
            //if (enableBeam) {
            //    // Échantillonner plusieurs points le long du rayon pour l'effet volumétrique
            //    float stepSize = 0.5;
            //    int numSteps = int(20.0 / stepSize);
            //    vec3 volumetricContrib = vec3(0.0);
            //    
            //    for (int i = 0; i < numSteps; ++i) {
            //        float t = float(i) * stepSize;
            //        vec3 samplePoint = ro + rd * t;
            //        float beamContrib = calculateBeamIntensity(samplePoint);
            //        
            //        if (beamContrib > 0.0) {
            //            // Simuler la diffusion atmosphérique
            //            float scattering = 0.01; // Coefficient de diffusion
            //            volumetricContrib += beamColor * beamContrib * scattering * stepSize;
            //        }
            //    }                
            //    col += throughput * volumetricContrib;
            //}
            
            // Ciel dégradé personnalisé : bleu foncé en haut, beige vers l'horizon
            float t = clamp(0.7 * (rd.y + 1.0), 0.0, 1.0);
            vec3 skyTop = vec3(0.133, 0.255, 0.502);      // Bleu foncé
            vec3 skyHorizon = vec3(1, 0.788, 0.592);  // Beige clair
            vec3 skyColor = mix(skyHorizon, skyTop, t);
            col += throughput * skyColor * 0.35;
            break;
        }

        // Après avoir trouvé l'intersection:
        Material mat;
        if (hitType == 1) {
            vec3 halfSize = blockSizes[hitIdx] * 0.5;
            vec3 blockMin = blocks[hitIdx] - halfSize;
            vec3 blockMax = blocks[hitIdx] + halfSize;

            Material matBase = materials_block[hitIdx];
            //verif que le mur est de type 5 MAT_ZONE_EMISSION
            if (matBase.type == MAT_ZONE_EMISSION) {
                float emissionFactor = emissionPattern(hit, blockMin, blockMax, time);
                if (emissionFactor > 0.0) {
                    matBase.type = MAT_EMISSIVE;
                    matBase.albedo = vec3(1.0);  // ou couleur désirée
                }
                // Sinon garder le matériau de base (MAT_ZONE_EMISSION se comporte comme le matériau sous-jacent)
            }
            mat = matBase;
        } else {
            mat = materials[hitIdx];
        }        
        // Si on touche une source émissive, ajouter sa contribution et terminer
        if (mat.type == MAT_EMISSIVE) {
            col += throughput * mat.albedo * lightIntensity;
            break;
        }
        
        // Ajout de l'échantillonnage direct de la lumière (NEE)
        vec3 directLight = sampleDirectLight(hit, n, -rd, mat, seed + float(bounce) * 1.618);
        col += throughput * directLight;
        
        //// Récupérer les propriétés du matériau
        //Material mat = materials[hitIdx];
        
        // Calculer l'éclairage direct
        //vec3 direct = directLight(hit, n, -rd, mat.type, mat.albedo, mat.roughness, minT);
        //col += throughput * direct;
        
        // Calculer le prochain rayon en fonction du matériau
        if (mat.type == MAT_DIFFUSE) {
            // Surface diffuse: échantillonnage de l'hémisphère
            rd = sampleHemisphere(n, hit, seed + float(bounce) * 3.14159);
            ro = hit + n * 0.001;
            throughput *= mat.albedo;
        }
        else if (mat.type == MAT_METALLIC) {
            // Surface métallique: réflexion
            rd = reflect_custom(rd, n, mat.roughness, hit, seed + float(bounce) * 2.71828);
            ro = hit + n * 0.001;
            throughput *= mat.albedo;
        }
        // Si on touche une source émissive, ajouter sa contribution et terminer
        else if (mat.type == MAT_EMISSIVE) {
            vec3 emitCol = mat.albedo;

            if (hitType == 1) { // mur
                vec3 blockMin = blocks[hitIdx] - 0.5 * blockSizes[hitIdx];
                vec3 blockMax = blocks[hitIdx] + 0.5 * blockSizes[hitIdx];
                float strength = emissionPattern(hit, blockMin, blockMax, time);
                emitCol *= strength;
            }

            col += throughput * emitCol * lightIntensity;
            //col += vec3(1.0, 0.0, 0.0); // lumière rouge vive fixe
            break;
        }

        else if (mat.type == MAT_GLASS) {
            // Verre: réfraction ou réflexion
            float reflChance;
            rd = refract(rd, n, mat.ior, mat.roughness, hit, seed + float(bounce) * 1.41421, reflChance);
            ro = hit + normalize(rd) * 0.001;
            
            // Le verre absorbe un peu de lumière, principalement sur les longues distances
            float absorbance = 0.1;
            vec3 absorption = exp(-mat.albedo * absorbance * minT);
            //throughput *= absorption / (1.0 - reflChance);
            throughput *= mix(absorption, vec3(1.0), reflChance);

        }
        else if (mat.type == MAT_MIRROR) {
            // Miroir parfait: réflexion sans rugosité
            if (mat.roughness < 0.01) {
                // Réflexion parfaite
                rd = reflect(rd, n);
            } else {
                // Réflexion avec rugosité
                rd = reflect_custom(rd, n, mat.roughness, hit, seed + float(bounce) * 1.73205);
            }
            ro = hit + n * 0.001;
            throughput *= mat.albedo;
        }
        else if (mat.type == MAT_EAU) {
            // Eau calme: réflexion parfaite comme un miroir avec légère teinte d'eau
            float cosI = abs(dot(-rd, n));
            
            // Effet Fresnel pour eau calme (plus de réflexion à angle rasant)
            float r0 = 0.02; // Réflectivité de l'eau à incidence normale (2%)
            float fresnel = r0 + (1.0 - r0) * pow(1.0 - cosI, 5.0);
            
            // Pour eau calme, on privilégie la réflexion (90% du temps)
            if (random(hit, seed + float(bounce) * 1.41421) < 0.9) {
                // Réflexion parfaite (eau calme = miroir parfait)
                rd = reflect(rd, n);
                ro = hit + n * 0.001;
                // Légère teinte bleu-vert de l'eau sur la réflexion
                throughput *= mix(vec3(0.8, 0.9, 1.0), vec3(1.0), fresnel);
            } else {
                // Rare réfraction pour effet réaliste
                float eta = 1.0 / 1.33; // Air vers eau
                vec3 refracted = refract(rd, n, eta);
                if (length(refracted) > 0.0) {
                    rd = normalize(refracted);
                    ro = hit - n * 0.001; // Entrer dans l'eau
                    throughput *= mat.albedo * 0.8; // Absorption légère
                } else {
                    // Réflexion totale si réfraction impossible
                    rd = reflect(rd, n);
                    ro = hit + n * 0.001;
                    throughput *= vec3(0.9, 0.95, 1.0);
                }
            }
        }
        

        
        // Roulette russe pour terminer prématurément les chemins à faible contribution
        if (bounce > 2) {
            float p = max(throughput.r, max(throughput.g, throughput.b));
            p = clamp(p, 0.0, 1.0);  // Ensure p stays in valid probability range
            if (random(hit, seed + bounce * 0.77) > p) break;
            throughput /= p;
        }
    }
    
    return col;
}

mat3 setCamera(vec3 ro, vec3 ta) {
    vec3 cw = normalize(ta - ro);
    vec3 cp = vec3(0.0, 1.0, 0.0);
    vec3 cu = normalize(cross(cw, cp));
    vec3 cv = normalize(cross(cu, cw));
    return mat3(cu, cv, cw);
}

void main() {
    vec3 color = vec3(0.0);
    
    // Anti-aliasing: multiplier les échantillons par pixel
    float sqrtSamples = sqrt(float(MAX_SAMPLES));
    float strataSize = 1.0 / sqrtSamples;

    for (int s = 0; s < MAX_SAMPLES; ++s) {
        // Calculer le décalage du sous-pixel pour l'anti-aliasing
        int strataX = s % int(sqrt(float(MAX_SAMPLES)));
        int strataY = s / int(sqrt(float(MAX_SAMPLES)));

        vec2 strata = vec2(float(strataX), float(strataY)) * strataSize;
        vec2 inStrata = vec2(random(vec3(gl_FragCoord.xy, time), float(s) * 0.1), random(vec3(gl_FragCoord.xy, time), float(s) * 0.2));

        vec2 jitter = strata + inStrata * strataSize - 0.5;
        
        vec2 uv = ((gl_FragCoord.xy + jitter) * 2.0 - resolution.xy) / resolution.y;
        
        // Mise en place de la caméra
        mat3 cam = setCamera(viewEye, viewCenter);
        vec3 rd = cam * normalize(vec3(uv, 1.5));
        vec3 ro = viewEye;
        
        // Seed pour le générateur de nombres aléatoires
        float seed = float(s) + random(vec3(gl_FragCoord.xy, 0.0), time);
        
        // Tracer le rayon
        color += trace(ro, rd, seed);
    }
    
    // Moyenne des échantillons
    color /= float(MAX_SAMPLES);
    
    // Tone mapping (ACES)
    const float a = 2.51;
    const float b = 0.03;
    const float c = 2.43;
    const float d = 0.59;
    const float e = 0.14;
    color = clamp((color * (a * color + b)) / (color * (c * color + d) + e), 0.0, 1.0);
    
    // Correction gamma
    color = pow(color, vec3(1.0 / 2.2));
    
    // Légère vignette
    vec2 q = gl_FragCoord.xy / resolution.xy;
    color *= 0.7 + 0.3 * pow(16.0 * q.x * q.y * (1.0 - q.x) * (1.0 - q.y), 0.1);
    vec3 prevColor = texture(previousFrame, gl_FragCoord.xy / resolution.xy).rgb;
    color = mix(color, prevColor, frameBlend);
    finalColor = vec4(color, 1.0);
}