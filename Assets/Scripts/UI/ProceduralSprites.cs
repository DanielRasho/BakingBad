using System;
using UnityEngine;

// Small signed-distance helpers for drawing UI sprites in code (icons, holes, badges) without extra art assets.
public static class ProceduralSprites
{
    // Renders a sampler into a sprite with 4x4 supersampling so edges stay smooth when the UI scales.
    public static Sprite Create(string spriteName, int width, int height, Vector2 pivot, Func<float, float, Color> sampler)
    {
        const int subSamples = 4;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = spriteName;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color accumulated = Color.clear;
                for (int sy = 0; sy < subSamples; sy++)
                {
                    for (int sx = 0; sx < subSamples; sx++)
                    {
                        Color sample = sampler(x + (sx + 0.5f) / subSamples, y + (sy + 0.5f) / subSamples);
                        accumulated += new Color(sample.r * sample.a, sample.g * sample.a, sample.b * sample.a, sample.a);
                    }
                }

                accumulated /= subSamples * subSamples;
                pixels[y * width + x] = accumulated.a > 0f
                    ? new Color(accumulated.r / accumulated.a, accumulated.g / accumulated.a, accumulated.b / accumulated.a, accumulated.a)
                    : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), pivot, 100f);
        sprite.name = spriteName;
        return sprite;
    }

    // Signed distance to an arbitrary closed polygon (negative inside).
    public static float PolygonDistance(Vector2 point, Vector2[] vertices)
    {
        float squaredDistance = (point - vertices[0]).sqrMagnitude;
        float sign = 1f;
        for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i, i++)
        {
            Vector2 edge = vertices[j] - vertices[i];
            Vector2 toPoint = point - vertices[i];
            Vector2 closest = toPoint - edge * Mathf.Clamp01(Vector2.Dot(toPoint, edge) / Vector2.Dot(edge, edge));
            squaredDistance = Mathf.Min(squaredDistance, closest.sqrMagnitude);

            bool aboveStart = point.y >= vertices[i].y;
            bool belowEnd = point.y < vertices[j].y;
            bool leftOfEdge = edge.x * toPoint.y > edge.y * toPoint.x;
            if ((aboveStart && belowEnd && leftOfEdge) || (!aboveStart && !belowEnd && !leftOfEdge))
            {
                sign = -sign;
            }
        }

        return sign * Mathf.Sqrt(squaredDistance);
    }

    public static float CapsuleDistance(Vector2 point, Vector2 start, Vector2 end, float radius)
    {
        Vector2 segment = end - start;
        float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / Vector2.Dot(segment, segment));
        return (point - (start + segment * t)).magnitude - radius;
    }
}
