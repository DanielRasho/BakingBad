using UnityEngine;

// Cada sonido tiene un cooldown por guardia para que entrar y salir de su rango no lo repita.
// El AudioManager ademas limita que varios guardias suenen a la vez (minInterval del cue).

[DisallowMultipleComponent]
[RequireComponent(typeof(CopEnemy))]
public class CopAudio : MonoBehaviour
{
    [Tooltip("Segundos minimos entre dos sonidos de sospecha de este guardia.")]
    [SerializeField, Min(0f)] private float suspiciousCooldown = 4f;

    [Tooltip("Segundos minimos entre dos sonidos de deteccion de este guardia.")]
    [SerializeField, Min(0f)] private float detectCooldown = 5f;

    [Tooltip("Segundos minimos entre dos sonidos de atrapar de este guardia.")]
    [SerializeField, Min(0f)] private float catchCooldown = 3f;

    private CopEnemy guard;
    private CopEnemy.GuardState previousState;
    private float nextSuspiciousTime = float.NegativeInfinity;
    private float nextDetectTime = float.NegativeInfinity;
    private float nextCatchTime = float.NegativeInfinity;

    private void Awake()
    {
        guard = GetComponent<CopEnemy>();
    }

    private void OnEnable()
    {
        if (guard == null)
        {
            return;
        }

        previousState = guard.State;
        guard.StateChanged += HandleStateChanged;
        guard.PlayerCaught += HandlePlayerCaught;
    }

    private void OnDisable()
    {
        if (guard == null)
        {
            return;
        }

        guard.StateChanged -= HandleStateChanged;
        guard.PlayerCaught -= HandlePlayerCaught;
    }

    private void HandleStateChanged(CopEnemy.GuardState newState)
    {
        // "Detectado" = el medidor de alerta se lleno y el guardia empieza a perseguir.
        bool startedChase = newState == CopEnemy.GuardState.Chase && previousState != CopEnemy.GuardState.Chase;
        // "Sospecha" = pasa de patrullar a notar algo (circulo amarillo).
        bool startedSuspicion = newState == CopEnemy.GuardState.Suspicious && previousState == CopEnemy.GuardState.Patrol;
        previousState = newState;

        if (startedSuspicion && Time.time >= nextSuspiciousTime)
        {
            nextSuspiciousTime = Time.time + suspiciousCooldown;
            AudioManager.Play(SfxId.GuardSuspicious);
        }

        if (startedChase && Time.time >= nextDetectTime)
        {
            nextDetectTime = Time.time + detectCooldown;
            AudioManager.Play(SfxId.GuardDetect);
        }
    }

    private void HandlePlayerCaught()
    {
        if (Time.time < nextCatchTime)
        {
            return;
        }

        nextCatchTime = Time.time + catchCooldown;
        AudioManager.Play(SfxId.GuardCatch);
    }
}
