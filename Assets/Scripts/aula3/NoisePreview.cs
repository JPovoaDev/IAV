using UnityEngine;

public class NoisePreview : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Texture2D tex = new Texture2D(256, 256);
        for (int x = 0; x < 256; x++)
            for (int z = 0; z < 256; z++) {
                float n = FBm(x, z, 4, 0.02f);
                tex.SetPixel(x, z, new Color(n, n, n));
            }
        tex.Apply();
        GetComponent<Renderer>().material.mainTexture = tex;
    }

    public static float FBm(float x, float z, int octaves, float scale, float persistence = 0.5f, float lacunarity = 2.0f) {
        float value = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float totalAmplitude = 0f;

        for (int i = 0; i < octaves; i++) {
            value += Mathf.PerlinNoise(x * scale * frequency, z * scale * frequency) * amplitude;
            totalAmplitude += amplitude;
            amplitude *= persistence; // decresce a cada oitava
            frequency *= lacunarity; // cresce a cada oitava
        }

        return value / totalAmplitude; // normalizar para [0, 1]
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
