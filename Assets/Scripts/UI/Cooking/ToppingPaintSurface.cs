using System;
using UnityEngine;
using UnityEngine.UI;

// Top view of the cake on its plate that the player covers with sauce by dragging a bottle over it.
// Everything lives in one small texture: the plate, the cake top and the sauce painted on the cake.
// Sauce only sticks to the cake, so the plate stays clean and the cake keeps its shape.
[DisallowMultipleComponent]
[RequireComponent(typeof(RawImage))]
public class ToppingPaintSurface : MonoBehaviour
{
    // Texture resolution; low on purpose so counting covered pixels every stroke is cheap.
    private const int Resolution = 128;
    private const float PlateRadiusFraction = 0.5f;
    private const float CakeRadiusFraction = 0.46f;
    private const float CakeEdgeWidthPixels = 2f;

    [SerializeField] private float brushRadiusPixels = 6.5f;
    [SerializeField] private Color plateColor = new Color(0.82f, 0.84f, 0.87f, 1f);
    [SerializeField] private Color cakeColor = new Color(0.93f, 0.85f, 0.80f, 1f);
    [SerializeField] private Color cakeEdgeColor = new Color(0.66f, 0.50f, 0.40f, 1f);

    private RawImage rawImage;
    private RectTransform rectTransform;
    private Canvas rootCanvas;
    private Texture2D texture;
    private Color32[] basePixels;
    private Color32[] pixels;
    private bool[] onPlate;
    private bool[] onCake;
    private bool[] painted;
    private int cakePixelCount;
    private int paintedPixels;
    private Color32 brushColor = new Color32(255, 255, 255, 255);
    private bool isInteractable;
    private bool strokeActive;
    private Vector2 lastStrokePoint;
    private bool textureDirty;

    // Raised whenever new sauce lands, so the UI can refresh the coverage text.
    public event Action Painted;

    // Fraction (0-1) of the cake top covered with sauce.
    public float Coverage
    {
        get { return cakePixelCount > 0 ? (float)paintedPixels / cakePixelCount : 0f; }
    }

    public bool HasPaint
    {
        get { return paintedPixels > 0; }
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnDestroy()
    {
        if (texture != null)
        {
            Destroy(texture);
        }
    }

    public void SetBrushColor(Color color)
    {
        EnsureInitialized();
        brushColor = color;
    }

    public void SetInteractable(bool interactable)
    {
        isInteractable = interactable;
        if (!interactable)
        {
            strokeActive = false;
        }
    }

    public void Clear()
    {
        EnsureInitialized();
        Array.Copy(basePixels, pixels, pixels.Length);
        Array.Clear(painted, 0, painted.Length);
        paintedPixels = 0;
        strokeActive = false;
        textureDirty = true;
        ApplyIfDirty();
    }

    // Reshapes the cake and wipes the sauce. The function gets a point relative to the center and the cake radius,
    // both in texture pixels, and returns its distance to the outline: negative inside the cake.
    public void SetCakeShape(Func<Vector2, float, float> shapeDistance)
    {
        EnsureInitialized();
        BuildBasePixels(shapeDistance);
        Clear();
    }

    public bool IsOverPlate(Vector2 screenPosition)
    {
        EnsureInitialized();
        Vector2 texturePoint;
        return TryGetTexturePoint(screenPosition, out texturePoint) && IsInside(onPlate, texturePoint);
    }

    public bool IsOverCake(Vector2 screenPosition)
    {
        EnsureInitialized();
        Vector2 texturePoint;
        return TryGetTexturePoint(screenPosition, out texturePoint) && IsInside(onCake, texturePoint);
    }

    // Pours sauce at this point; consecutive calls join into one solid stroke until EndStroke.
    public void PaintAt(Vector2 screenPosition)
    {
        if (!isInteractable || !IsOverPlate(screenPosition))
        {
            strokeActive = false;
            return;
        }

        Vector2 texturePoint;
        TryGetTexturePoint(screenPosition, out texturePoint);
        StampLine(strokeActive ? lastStrokePoint : texturePoint, texturePoint);
        lastStrokePoint = texturePoint;
        strokeActive = true;
        ApplyIfDirty();
    }

    public void EndStroke()
    {
        strokeActive = false;
    }

    private void EnsureInitialized()
    {
        if (texture != null)
        {
            return;
        }

        rawImage = GetComponent<RawImage>();
        rectTransform = GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>();

        int count = Resolution * Resolution;
        basePixels = new Color32[count];
        pixels = new Color32[count];
        onPlate = new bool[count];
        onCake = new bool[count];
        painted = new bool[count];
        BuildBasePixels((point, radius) => point.magnitude - radius);

        texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
        texture.name = "ToppingPaintSurface";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        rawImage.texture = texture;
        rawImage.raycastTarget = false;
        Clear();
    }

    private void BuildBasePixels(Func<Vector2, float, float> shapeDistance)
    {
        cakePixelCount = 0;

        float center = Resolution * 0.5f;
        float plateRadius = Resolution * PlateRadiusFraction - 1f;
        float cakeRadius = Resolution * CakeRadiusFraction;
        Color32 plate = plateColor;
        Color32 cake = cakeColor;
        Color32 cakeEdge = cakeEdgeColor;
        Color32 clear = new Color32(0, 0, 0, 0);

        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                int index = y * Resolution + x;
                Vector2 point = new Vector2(x + 0.5f - center, y + 0.5f - center);
                float cakeDistance = shapeDistance(point, cakeRadius);
                onPlate[index] = point.magnitude <= plateRadius;
                onCake[index] = cakeDistance <= 0f;
                if (onCake[index])
                {
                    cakePixelCount++;
                    basePixels[index] = cakeDistance > -CakeEdgeWidthPixels ? cakeEdge : cake;
                }
                else
                {
                    basePixels[index] = onPlate[index] ? plate : clear;
                }
            }
        }
    }

    private bool TryGetTexturePoint(Vector2 screenPosition, out Vector2 texturePoint)
    {
        texturePoint = Vector2.zero;
        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPosition, GetEventCamera(), out localPoint))
        {
            return false;
        }

        Rect rect = rectTransform.rect;
        float u = (localPoint.x - rect.xMin) / rect.width;
        float v = (localPoint.y - rect.yMin) / rect.height;
        texturePoint = new Vector2(u * Resolution, v * Resolution);
        return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
    }

    private static bool IsInside(bool[] mask, Vector2 texturePoint)
    {
        int x = Mathf.FloorToInt(texturePoint.x);
        int y = Mathf.FloorToInt(texturePoint.y);
        return x >= 0 && y >= 0 && x < Resolution && y < Resolution && mask[y * Resolution + x];
    }

    // Stamps the brush along the segment so quick mouse moves leave a solid line instead of dots.
    private void StampLine(Vector2 from, Vector2 to)
    {
        float distance = Vector2.Distance(from, to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / (brushRadiusPixels * 0.5f)));
        int coveredBefore = paintedPixels;
        for (int i = 0; i <= steps; i++)
        {
            StampCircle(Vector2.Lerp(from, to, (float)i / steps));
        }

        if (paintedPixels != coveredBefore && Painted != null)
        {
            Painted.Invoke();
        }
    }

    private void StampCircle(Vector2 center)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - brushRadiusPixels));
        int maxX = Mathf.Min(Resolution - 1, Mathf.CeilToInt(center.x + brushRadiusPixels));
        int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - brushRadiusPixels));
        int maxY = Mathf.Min(Resolution - 1, Mathf.CeilToInt(center.y + brushRadiusPixels));
        float radiusSquared = brushRadiusPixels * brushRadiusPixels;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int index = y * Resolution + x;
                if (painted[index] || !onCake[index])
                {
                    continue;
                }

                float dx = x + 0.5f - center.x;
                float dy = y + 0.5f - center.y;
                if (dx * dx + dy * dy > radiusSquared)
                {
                    continue;
                }

                painted[index] = true;
                pixels[index] = brushColor;
                paintedPixels++;
                textureDirty = true;
            }
        }
    }

    private void ApplyIfDirty()
    {
        if (!textureDirty || texture == null)
        {
            return;
        }

        texture.SetPixels32(pixels);
        texture.Apply(false);
        textureDirty = false;
    }

    private Camera GetEventCamera()
    {
        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }

        if (rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return rootCanvas.worldCamera;
    }
}
