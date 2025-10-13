#version 330 core

in vec2 fragTexCoord;
out vec4 fragColor;

uniform sampler2D currentFrame;  // denoiseTarget
uniform sampler2D historyFrame;  // renderHistory

uniform vec2 resolution;
uniform float time;
uniform int frame;

// YUV-RGB conversion routine
vec3 encodePalYuv(vec3 rgb) {
    rgb = pow(rgb, vec3(2.0)); // gamma correction
    return vec3(
        dot(rgb, vec3(0.299, 0.587, 0.114)),
        dot(rgb, vec3(-0.14713, -0.28886, 0.436)),
        dot(rgb, vec3(0.615, -0.51499, -0.10001))
    );
}

vec3 decodePalYuv(vec3 yuv) {
    vec3 rgb = vec3(
        dot(yuv, vec3(1.0, 0.0, 1.13983)),
        dot(yuv, vec3(1.0, -0.39465, -0.58060)),
        dot(yuv, vec3(1.0, 2.03211, 0.0))
    );
    return pow(rgb, vec3(1.0 / 2.0)); // inverse gamma correction
}

void main() {
    vec2 uv = fragTexCoord;
    vec2 off = 1.0 / resolution;

    vec3 curr = texture(currentFrame, uv).rgb;
    vec4 histData = texture(historyFrame, uv);

    vec3 hist = histData.rgb;
    float histMixRate = min(histData.a, 0.5); // lire alpha de l’historique

    //nettoyage de l’historique
    // Réinitialisation locale si trop de changement (anti-taches persistantes)
    //if (distance(curr, hist) > 0.3) {
    //    hist = curr;
    //    histMixRate = 1.0; // forcera une mise à jour brutale
    //}
    //hist *= 0.8;

    // Gamma-space accumulation
    vec3 blended = mix(hist * hist, curr * curr, histMixRate);
    blended = sqrt(blended);

    // Neighborhood samples
    vec3 samples[9];
    samples[0] = texture(currentFrame, uv).rgb;
    samples[1] = texture(currentFrame, uv + vec2(+off.x, 0.0)).rgb;
    samples[2] = texture(currentFrame, uv + vec2(-off.x, 0.0)).rgb;
    samples[3] = texture(currentFrame, uv + vec2(0.0, +off.y)).rgb;
    samples[4] = texture(currentFrame, uv + vec2(0.0, -off.y)).rgb;
    samples[5] = texture(currentFrame, uv + vec2(+off.x, +off.y)).rgb;
    samples[6] = texture(currentFrame, uv + vec2(-off.x, +off.y)).rgb;
    samples[7] = texture(currentFrame, uv + vec2(+off.x, -off.y)).rgb;
    samples[8] = texture(currentFrame, uv + vec2(-off.x, -off.y)).rgb;

    // Convert to YUV for clamping
    vec3 blendedYUV = encodePalYuv(blended);

    vec3 minYUV = encodePalYuv(samples[0]);
    vec3 maxYUV = minYUV;

    for (int i = 1; i < 9; ++i) {
        vec3 yuv = encodePalYuv(samples[i]);
        minYUV = min(minYUV, yuv);
        maxYUV = max(maxYUV, yuv);
    }

    // Slight blending of extremes (stabilisation)
    minYUV = mix(minYUV, encodePalYuv(curr), 0.5);
    maxYUV = mix(maxYUV, encodePalYuv(curr), 0.5);

    vec3 preClampYUV = blendedYUV;
    blendedYUV = clamp(blendedYUV, minYUV, maxYUV);

    //blendedYUV.x = pow(blendedYUV.x, 0.75); // boost progressif de la luminance (Y)
//blendedYUV.x *= 1.05; // +5% de luminosité
//blendedYUV.x = clamp(blendedYUV.x, 0.0, 1.0);

    // Recalculate mix rate based on clamping strength
    vec3 diff = blendedYUV - preClampYUV;
    float clampAmount = dot(diff, diff);

    float mixRate = histMixRate;
    mixRate = 1.0 / (1.0 / mixRate + 1.0);  // smooth feedback
    mixRate += clampAmount * 4.0;
    mixRate = clamp(mixRate, 0.05, 0.5);

    vec3 finalColor = decodePalYuv(blendedYUV);
    fragColor = vec4(finalColor, mixRate); // output mixRate in alpha for next frame
}
