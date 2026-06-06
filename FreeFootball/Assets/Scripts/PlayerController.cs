using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Riferimenti (auto-trovati se vuoti)")]
    public Transform palla;

    [Header("Movimento giocatore")]
    public float velocita = 4f;

    [Header("Camera — tasto destro mouse")]
    public float sensibilita = 0.25f;
    [Range(10f, 89f)] public float pitchMax = 80f;

    [Header("Ball Control")]
    [Tooltip("Distanza massima dalla palla per interagire con essa")]
    public float distanzaControllo = 3f;
    [Tooltip("Forza continua WASD sulla palla quando sei vicino (N)")]
    public float forzaWASDSuPalla = 2f;

    public bool InBallControlMode { get; private set; }

    private float yaw, pitch;
    private Vector2 prevMousePos;
    private BallLauncher ballLauncher;

    void Start()
    {
        ballLauncher = FindAnyObjectByType<BallLauncher>();
        if (palla == null && ballLauncher != null)
            palla = ballLauncher.transform;

        // Inizializza rotazione dalla posizione attuale della camera in scena
        Vector3 eu = transform.eulerAngles;
        yaw   = eu.y;
        pitch = eu.x > 180f ? eu.x - 360f : eu.x;
    }

    void Update()
    {
        GestisciRotazione();
        GestisciMovimento();
        ControllaDistanzaPalla();

        if (Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            ballLauncher?.ResetPartita();
    }

    void GestisciRotazione()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            prevMousePos = mousePos;
            return;
        }

        if (!Mouse.current.rightButton.isPressed) return;

        Vector2 delta = mousePos - prevMousePos;
        prevMousePos  = mousePos;

        yaw   += delta.x * sensibilita;
        pitch -= delta.y * sensibilita;
        pitch  = Mathf.Clamp(pitch, -pitchMax, pitchMax);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void GestisciMovimento()
    {
        float h = 0f, v = 0f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)    v =  1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)  v = -1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)  h = -1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h =  1f;
        if (h == 0f && v == 0f) return;

        Vector3 avanti = transform.forward; avanti.y = 0f;
        Vector3 destra  = transform.right;  destra.y  = 0f;
        if (avanti.sqrMagnitude > 0f) avanti.Normalize();
        if (destra.sqrMagnitude  > 0f) destra.Normalize();

        Vector3 dir = (avanti * v + destra * h).normalized;

        // Muove il giocatore (camera)
        transform.position += dir * velocita * Time.deltaTime;

    }

    void ControllaDistanzaPalla()
    {
        if (palla == null) return;
        Vector2 me   = new Vector2(transform.position.x, transform.position.z);
        Vector2 ball = new Vector2(palla.position.x,     palla.position.z);
        bool era = InBallControlMode;
        InBallControlMode = Vector2.Distance(me, ball) <= distanzaControllo;
    }
}
