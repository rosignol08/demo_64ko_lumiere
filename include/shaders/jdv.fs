#version 330
out vec4 fragColor;
in vec2 tc;

uniform sampler2D tex;
uniform vec2 pas;

vec2 offset[8] = vec2[8]( vec2(pas.x, 0.0),
			  vec2(pas.x, pas.y),
			  vec2(0.0, pas.y),
			  vec2(-pas.x, pas.y),
			  vec2(-pas.x, 0.0),
			  vec2(-pas.x, -pas.y),
			  vec2(0.0, -pas.y),
			  vec2(pas.x,-pas.y)
			  );

bool est_vivant(vec2 p) {
  return texture(tex, p).r > 0.99;
}

int score(vec2 p) {
  int s = 0;
  for(int i = 0; i < 8; ++i)
    if(est_vivant(p + offset[i]))
      ++s;
  return s;
}

void main() {
  if(est_vivant(tc)) {
    int s = score(tc);
    if(s != 2 && s != 3)
      fragColor = vec4(0.0);
    else
      fragColor = texture(tex, tc);
  } else {
    if(score(tc) == 3)
      fragColor = vec4(1.0);
    else
      fragColor = texture(tex, tc);
  }    
}
