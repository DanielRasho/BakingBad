using System;
using UnityEngine;

public enum SfxId
{
    UiClick = 0,
    KitchenUiClick = 1,
    PieceRotate = 2,
    OrderPickup = 3,
    OrderDeliveredCorrect = 4,
    OrderDeliveredWrong = 5,
    OrderActivate = 6
}

[Serializable]
public class SfxCue
{
    [Tooltip("Una o mas variaciones. Se elige una al azar sin repetir la anterior.")]
    [SerializeField] private AudioClip[] clips = new AudioClip[0];
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField, Range(0.5f, 2f)] private float pitchMin = 1f;
    [SerializeField, Range(0.5f, 2f)] private float pitchMax = 1f;
    [Tooltip("Segundos de espera antes de sonar (para encadenar capas, p. ej. papel y luego dinero).")]
    [SerializeField, Min(0f)] private float delay = 0f;
    [Tooltip("Tiempo minimo entre dos reproducciones de este mismo efecto.")]
    [SerializeField, Min(0f)] private float minInterval = 0.04f;

    [NonSerialized] private int lastClipIndex = -1;
    [NonSerialized] private int sequencePosition;
    [NonSerialized] private float lastPlayTime = float.NegativeInfinity;

    public SfxCue()
    {
    }

    public SfxCue(float pitchMin, float pitchMax, float delay = 0f)
    {
        this.pitchMin = pitchMin;
        this.pitchMax = pitchMax;
        this.delay = delay;
    }

    public float Volume
    {
        get { return volume; }
    }

    public float Delay
    {
        get { return delay; }
    }

    public bool HasClips
    {
        get
        {
            if (clips == null)
            {
                return false;
            }

            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public float NextPitch()
    {
        return pitchMax > pitchMin ? UnityEngine.Random.Range(pitchMin, pitchMax) : pitchMin;
    }

    public AudioClip NextClip()
    {
        if (!HasClips)
        {
            return null;
        }

        for (int attempt = 0; attempt < 8; attempt++)
        {
            int index = UnityEngine.Random.Range(0, clips.Length);
            if (clips[index] != null && (index != lastClipIndex || clips.Length == 1))
            {
                lastClipIndex = index;
                return clips[index];
            }
        }

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
            {
                lastClipIndex = i;
                return clips[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Recorre los clips en orden como los dientes de un engranaje.
    /// direction mayor que 0 avanza (1, 2, 3, 4, 1...); menor que 0 retrocede (4, 3, 2, 1, 4...).
    /// </summary>
    public AudioClip NextClipInSequence(int direction)
    {
        if (!HasClips)
        {
            return null;
        }

        int count = clips.Length;
        int index;
        if (direction >= 0)
        {
            index = sequencePosition;
            sequencePosition = (sequencePosition + 1) % count;
        }
        else
        {
            sequencePosition = (sequencePosition - 1 + count) % count;
            index = sequencePosition;
        }

        lastClipIndex = index;
        return clips[index];
    }

    /// <summary>Devuelve false si el efecto sono hace menos de minInterval.</summary>
    public bool TryMarkPlayed(float now)
    {
        if (now - lastPlayTime < minInterval)
        {
            return false;
        }

        lastPlayTime = now;
        return true;
    }
}
