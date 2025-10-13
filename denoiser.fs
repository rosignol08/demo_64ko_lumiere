// denoiser.fs
#version 330
// Post-process denoising shader qui utilise Edge-Avoiding À-Trous Wavelet Transform et Temporal AA 
//de https://www.shadertoy.com/view/ldKBzG

uniform sampler2D u_texture;
uniform vec2 u_resolution;
uniform float u_time;
uniform float u_denoiseStrength;

vec3 encodePalYuv(vec3 rgb) {
    //rgb = pow(rgb, vec3(2.0)); // gamma correction
    return vec3(
        dot(rgb, vec3(0.299, 0.587, 0.114)),
        dot(rgb, vec3(-0.14713, -0.28886, 0.436)),
        dot(rgb, vec3(0.615, -0.51499, -0.10001))
    );
}

vec3 decodePalYuv(vec3 yuv) {
    vec3 rgb = vec3(
        dot(yuv, vec3(1., 0., 1.13983)),
        dot(yuv, vec3(1., -0.39465, -0.58060)),
        dot(yuv, vec3(1., 2.03211, 0.))
    );
    // Appliquer une correction gamma plus douce
    //rgb = pow(rgb, vec3(1.0 / 1.2));
//
    //// Ajuster le contraste
    //float contrast = 2.2; // Ajustez cette valeur pour augmenter ou diminuer le contraste
    //rgb = (rgb - 0.5) * contrast + 0.5;
//
    return rgb;
    //return pow(rgb, vec3(1.0 / 2.0)); // gamma correction
}

void main() {
    vec2 uv = gl_FragCoord.xy / u_resolution.xy;
    vec2 offset[25];
    offset[0] = vec2(-2,-2);
    offset[1] = vec2(-1,-2);
    offset[2] = vec2(0,-2);
    offset[3] = vec2(1,-2);
    offset[4] = vec2(2,-2);
    offset[5] = vec2(-2,-1);
    offset[6] = vec2(-1,-1);
    offset[7] = vec2(0,-1);
    offset[8] = vec2(1,-1);
    offset[9] = vec2(2,-1);
    offset[10] = vec2(-2,0);
    offset[11] = vec2(-1,0);
    offset[12] = vec2(0,0);
    offset[13] = vec2(1,0);
    offset[14] = vec2(2,0);
    offset[15] = vec2(-2,1);
    offset[16] = vec2(-1,1);
    offset[17] = vec2(0,1);
    offset[18] = vec2(1,1);
    offset[19] = vec2(2,1);
    offset[20] = vec2(-2,2);
    offset[21] = vec2(-1,2);
    offset[22] = vec2(0,2);
    offset[23] = vec2(1,2);
    offset[24] = vec2(2,2);

    float kernel[25];
    kernel[0] = 1.0/256.0;
    kernel[1] = 1.0/64.0;
    kernel[2] = 3.0/128.0;
    kernel[3] = 1.0/64.0;
    kernel[4] = 1.0/256.0;
    kernel[5] = 1.0/64.0;
    kernel[6] = 1.0/16.0;
    kernel[7] = 3.0/32.0;
    kernel[8] = 1.0/16.0;
    kernel[9] = 1.0/64.0;
    kernel[10] = 3.0/128.0;
    kernel[11] = 3.0/32.0;
    kernel[12] = 9.0/64.0;
    kernel[13] = 3.0/32.0;
    kernel[14] = 3.0/128.0;
    kernel[15] = 1.0/64.0;
    kernel[16] = 1.0/16.0;
    kernel[17] = 3.0/32.0;
    kernel[18] = 1.0/16.0;
    kernel[19] = 1.0/64.0;
    kernel[20] = 1.0/256.0;
    kernel[21] = 1.0/64.0;
    kernel[22] = 3.0/128.0;
    kernel[23] = 1.0/64.0;
    kernel[24] = 1.0/256.0;
    
    vec4 sum = vec4(0.0);
    float c_phi = 1.0;
    float n_phi = 0.5;
    vec4 cval = texture2D(u_texture, uv);
    vec4 nval = texture2D(u_texture, uv);

    float cum_w = 0.0;
    for(int i=0; i<25; i++) {
        vec2 uv_offset = uv + offset[i] * u_denoiseStrength / u_resolution.xy;
        vec4 ctmp = texture2D(u_texture, uv_offset);
        vec4 t = cval - ctmp;
        float dist2 = dot(t, t);
        float c_w = min(exp(-(dist2)/c_phi), 1.0);

        vec4 ntmp = texture2D(u_texture, uv_offset);
        t = nval - ntmp;
        dist2 = max(dot(t, t), 0.0);
        float n_w = min(exp(-(dist2)/n_phi), 1.0);

        float weight = c_w * n_w;
        sum += ctmp * weight * kernel[i];
        cum_w += weight * kernel[i];
    }

    vec4 denoisedColor = sum / cum_w;

    // Temporal AA
    vec4 lastColor = texture2D(u_texture, uv);
    vec3 antialiased = lastColor.xyz;
    float mixRate = min(lastColor.w, 0.5);

    vec2 off = 1.0 / u_resolution.xy;
    vec3 in0 = denoisedColor.xyz;

    antialiased = mix(antialiased * antialiased, in0 * in0, mixRate);
    antialiased = sqrt(antialiased);

    vec3 in1 = texture2D(u_texture, uv + vec2(+off.x, 0.0)).xyz;
    vec3 in2 = texture2D(u_texture, uv + vec2(-off.x, 0.0)).xyz;
    vec3 in3 = texture2D(u_texture, uv + vec2(0.0, +off.y)).xyz;
    vec3 in4 = texture2D(u_texture, uv + vec2(0.0, -off.y)).xyz;
    vec3 in5 = texture2D(u_texture, uv + vec2(+off.x, +off.y)).xyz;
    vec3 in6 = texture2D(u_texture, uv + vec2(-off.x, +off.y)).xyz;
    vec3 in7 = texture2D(u_texture, uv + vec2(+off.x, -off.y)).xyz;
    vec3 in8 = texture2D(u_texture, uv + vec2(-off.x, -off.y)).xyz;

    antialiased = encodePalYuv(antialiased);
    in0 = encodePalYuv(in0);
    in1 = encodePalYuv(in1);
    in2 = encodePalYuv(in2);
    in3 = encodePalYuv(in3);
    in4 = encodePalYuv(in4);
    in5 = encodePalYuv(in5);
    in6 = encodePalYuv(in6);
    in7 = encodePalYuv(in7);
    in8 = encodePalYuv(in8);

    vec3 minColor = min(min(min(in0, in1), min(in2, in3)), in4);
    vec3 maxColor = max(max(max(in0, in1), max(in2, in3)), in4);
    minColor = mix(minColor, min(min(min(in5, in6), min(in7, in8)), minColor), 0.5);
    maxColor = mix(maxColor, max(max(max(in5, in6), max(in7, in8)), maxColor), 0.5);

    vec3 preclamping = antialiased;
    antialiased = clamp(antialiased, minColor, maxColor);

    mixRate = 1.0 / (1.0 / mixRate + 1.0);

    vec3 diff = antialiased - preclamping;
    float clampAmount = dot(diff, diff);

    mixRate += clampAmount * 4.0;
    mixRate = clamp(mixRate, 0.01, 0.5);

    antialiased = decodePalYuv(antialiased);
//colorOutput = vec4(antialiased, 1.0);  // Pour l'affichage
//temporalOutput = vec4(0.0, 0.0, 0.0, mixRate);  // Pour le feedback temporel




    //gl_FragColor = vec4(antialiased, mixRate);
    gl_FragColor = vec4(antialiased, 1.0);
    //gl_FragColor = vec4(vec3(mixRate), 1.0);


}