using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Bersaglio")]
    public Transform target;
    [Tooltip("Trascina qui lo script BallLauncher dalla palla")]
    public BallLauncher ballLauncher;

    [Header("Fluidità")]
    [Range(1f, 20f)] public float smoothSpeed = 6f;

    // Calcolato automaticamente in Start() dalla posizione attuale della camera
    private Vector3 offset;
    private Vector3 velocitaCorrente;

    void Start()
    {
        // PlayerController gestisce la camera — CameraFollow si disabilita automaticamente
        if (GetComponent<PlayerController>() != null)
        {
            enabled = false;
            Debug.Log("[CameraFollow] Disabilitato — PlayerController attivo sulla stessa camera");
            return;
        }
        if (target == null) return;
        if (ballLauncher == null)
            ballLauncher = FindObjectOfType<BallLauncher>();
        offset = transform.position - target.position;
        Debug.Log($"[CameraFollow] Offset calcolato dalla camera: {offset}");
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Durante il tiro la camera resta ferma — il giocatore guarda la palla da dove ha tirato
        if (ballLauncher != null && ballLauncher.InVolo) return;

        Vector3 posizioneDesiderata = target.position + offset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            posizioneDesiderata,
            ref velocitaCorrente,
            1f / smoothSpeed
        );
    }
}
