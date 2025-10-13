#version 330 core

in vec2 fragTexCoord;
out vec4 fragColor;

// Textures d'entrée (liées depuis Raylib avec SetShaderValueTexture)
uniform sampler2D renderNoisy;     // image bruitée
uniform sampler2D renderNormals;   // normales + profondeur dans alpha
uniform sampler2D renderHistory;   // frame précédente

// Uniformes
uniform vec2 resolution;
uniform float time;
uniform int frame;
uniform float u_denoiseStrength; // force du débruitage

// Constantes pour le filtre À-Trous
const float c_phi = 1.0;
const float n_phi = 128.0;
const float p_phi = 1.0;

void main() {
    vec2 uv = fragTexCoord;
    vec2 pixel = 1.0 / resolution;

    vec3 cval = texture(renderNoisy, uv).rgb;
    vec3 nval = texture(renderNormals, uv).rgb;
    float zval = texture(renderNormals, uv).a;

    float stepwidth = u_denoiseStrength;

    vec3 sum = vec3(0.0);
    float cum_w = 0.0;

    for (int i = -2; i <= 2; ++i) {
        for (int j = -2; j <= 2; ++j) {
            vec2 offset = vec2(i, j) * stepwidth * pixel;
            vec2 tc = uv + offset;

            vec3 ctmp = texture(renderNoisy, tc).rgb;
            vec3 ntmp = texture(renderNormals, tc).rgb;
            float ztmp = texture(renderNormals, tc).a;

            float dist2 = dot(ctmp - cval, ctmp - cval);
            float c_w = min(exp(-dist2 / (c_phi * c_phi)), 1.0);

            float n_w = min(exp(-dot(ntmp - nval, ntmp - nval) / (n_phi * n_phi)), 1.0);
            float r_w = min(exp(-pow(ztmp - zval, 2.0) / (p_phi * p_phi)), 1.0);

            float weight = c_w * n_w * r_w;
            sum += ctmp * weight;
            cum_w += weight;
        }
    }

    vec3 colorFiltered = sum / cum_w;

    // Feedback simple avec blending temporel (tu peux ajuster le facteur)
    vec3 prev = texture(renderHistory, uv).rgb;
    vec3 blended = mix(colorFiltered, prev, 0.1); // 0.1 = blending léger

    fragColor = vec4(blended, 1.0);
}
