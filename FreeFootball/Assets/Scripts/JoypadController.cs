using UnityEngine;
using UnityEngine.InputSystem;

// MAPPING:
//   Left stick       → muovi giocatore
//   Right stick      → ruota camera (libero) / effetto tiro (durante RT)
//   LT               → tocco palla (direzione left stick, forza dal picco analogico)
//   RT               → tiro (forza + angolo right stick Y + curva right stick X)
//   LB               → toggle modalità interno del piede
//   B / Start        → reset partita

public class JoypadController : MonoBehaviour
{
    [Header("Riferimenti (auto-trovati se vuoti)")]
    public Transform playerCamera;
    public Transform palla;

    [Header("Movimento giocatore")]
    public float velocita = 4f;
    [Range(0f, 0.5f)] public float deadZoneLS = 0.15f;

    [Header("Camera — stick destro")]
    public float sensibilitaCamera = 3f;
    [Range(10f, 89f)] public float pitchMax = 80f;
    [Range(0f, 0.5f)] public float deadZoneRS = 0.15f;

    [Header("Ball Control")]
    public float distanzaControllo = 3f;

    [Header("Tocco — LT")]
    [Tooltip("Forza minima tocco (trigger appena premuto)")]
    public float toccoForzaMin = 0.25f;
    [Tooltip("Forza massima tocco (trigger a fondo)")]
    public float toccoForzaMax = 0.50f;

    [Header("Tiro — RT")]
    [Tooltip("Forza minima tiro (appena sopra soglia)")]
    public float tiroForzaMin = 3f;
    [Tooltip("Forza massima tiro (trigger a fondo)")]
    public float tiroForzaMax = 8f;
    public float angoloMassimo        = 32f;
    public float angoloMinimo         = -3f;
    public float spinMaxColloDelPiede = 35f;
    public float spinMaxInterno       = 60f;
    public float spinMaxVerticale     = 45f;

    // ── stato interno ─────────────────────────────────────────────────────────
    private Rigidbody    ballRb;
    private BallAudio    ballAudio;
    private BallLauncher ballLauncher;

    private float yaw, pitch;
    private bool  inBallControlMode;

    // LT (tocco)
    private float ltPeakValue;
    private bool  ltWasPressed;

    // RT (tiro)
    private float   rtPeakValue;
    private bool    rtWasPressed;
    private Vector2 effectStick;   // right stick campionato durante RT press

    // LB toggle (interno del piede)
    private bool internoMode;

    private LineRenderer lineAim;
    private string       hudText = "";
    private Vector2      lsCache;

    // ── setup ─────────────────────────────────────────────────────────────────
    void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main.transform;

        ballLauncher = FindAnyObjectByType<BallLauncher>();
        if (palla == null && ballLauncher != null) palla = ballLauncher.transform;

        if (palla != null)
        {
            ballRb    = palla.GetComponent<Rigidbody>();
            ballAudio = palla.GetComponent<BallAudio>();
        }

        Vector3 eu = playerCamera.eulerAngles;
        yaw   = eu.y;
        pitch = eu.x > 180f ? eu.x - 360f : eu.x;

        CreaLineaAim();
    }

    void CreaLineaAim()
    {
        lineAim = gameObject.AddComponent<LineRenderer>();
        lineAim.startWidth    = 0.08f;
        lineAim.endWidth      = 0.02f;
        lineAim.positionCount = 32;
        lineAim.enabled       = false;
        lineAim.useWorldSpace = true;
        var mat = new Material(Shader.Find("Sprites/Default"));
        lineAim.material = mat;
    }

    // ── loop ──────────────────────────────────────────────────────────────────
    void Update()
    {
        if (Gamepad.current == null) return;

        lsCache = Gamepad.current.leftStick.ReadValue();

        // LB = interno del piede: prova hold via ReadValue, fallback toggle
        internoMode = Gamepad.current.leftShoulder.ReadValue() > 0.5f;

        ControllaDistanzaPalla();
        GestisciMovimento();
        GestisciCamera();
        GestisciLT();
        GestisciRT();
        GestisciReset();
    }

    // ── distanza palla ────────────────────────────────────────────────────────
    void ControllaDistanzaPalla()
    {
        if (palla == null) return;
        Vector2 me   = new Vector2(playerCamera.position.x, playerCamera.position.z);
        Vector2 ball = new Vector2(palla.position.x,        palla.position.z);
        inBallControlMode = Vector2.Distance(me, ball) <= distanzaControllo;
    }

    // ── movimento ─────────────────────────────────────────────────────────────
    void GestisciMovimento()
    {
        if (lsCache.magnitude < deadZoneLS) return;

        Vector3 avanti = playerCamera.forward; avanti.y = 0f;
        Vector3 destra = playerCamera.right;   destra.y = 0f;
        if (avanti.sqrMagnitude > 0f) avanti.Normalize();
        if (destra.sqrMagnitude  > 0f) destra.Normalize();

        playerCamera.position += (avanti * lsCache.y + destra * lsCache.x).normalized
                                 * velocita * Time.deltaTime;
    }

    // ── camera ────────────────────────────────────────────────────────────────
    void GestisciCamera()
    {
        // Right stick è usato per l'effetto durante RT — blocca camera
        if (rtWasPressed) return;

        Vector2 rs = Gamepad.current.rightStick.ReadValue();
        if (rs.magnitude < deadZoneRS) return;

        Vector3 eu = playerCamera.eulerAngles;
        yaw   = eu.y;
        pitch = eu.x > 180f ? eu.x - 360f : eu.x;

        yaw   += rs.x * sensibilitaCamera;
        pitch -= rs.y * sensibilitaCamera;
        pitch  = Mathf.Clamp(pitch, -pitchMax, pitchMax);
        playerCamera.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    // ── LT: tocco ────────────────────────────────────────────────────────────
    void GestisciLT()
    {
        if (!inBallControlMode || ballRb == null)
        {
            ltWasPressed = false; ltPeakValue = 0f;
            return;
        }

        float lt        = Gamepad.current.leftTrigger.ReadValue();
        bool  ltPressed = lt > 0.1f;

        if (ltPressed)
        {
            ltPeakValue  = Mathf.Max(ltPeakValue, lt);
            ltWasPressed = true;
            hudText      = $"TOCCO  [{(int)(ltPeakValue * 100)}%]";
        }
        else if (ltWasPressed)
        {
            EseguiTocco(ltPeakValue);
            ltWasPressed = false;
            ltPeakValue  = 0f;
        }
    }

    // ── RT: tiro ─────────────────────────────────────────────────────────────
    void GestisciRT()
    {
        if (!inBallControlMode || ballRb == null)
        {
            if (rtWasPressed) { lineAim.enabled = false; hudText = ""; }
            rtWasPressed = false; rtPeakValue = 0f;
            return;
        }

        float rt        = Gamepad.current.rightTrigger.ReadValue();
        bool  rtPressed = rt > 0.1f;

        if (rtPressed)
        {
            rtPeakValue  = Mathf.Max(rtPeakValue, rt);
            effectStick  = Gamepad.current.rightStick.ReadValue();
            rtWasPressed = true;

            string altezza = effectStick.y >  0.35f ? "LOB"       :
                             effectStick.y < -0.35f ? "RASOTERRA" : "TIRO";
            string curva   = effectStick.x >  0.35f ? " +DX"      :
                             effectStick.x < -0.35f ? " +SX"      : "";
            string mod     = internoMode ? "INTERNO  " : "";
            hudText = $"{mod}{altezza}{curva}  [{(int)(rtPeakValue * 100)}%]";

            AggiorneFreccia();
        }
        else if (rtWasPressed)
        {
            lineAim.enabled = false;
            hudText         = "";
            EseguiTiro(rtPeakValue, effectStick);
            rtWasPressed = false;
            rtPeakValue  = 0f;
        }
    }

    // ── freccia direzionale ───────────────────────────────────────────────────
    void AggiorneFreccia()
    {
        if (palla == null) { lineAim.enabled = false; return; }

        Vector3 av  = playerCamera.forward; av.y = 0f; av.Normalize();
        Vector3 dx  = playerCamera.right;   dx.y = 0f; dx.Normalize();
        Vector3 dir = lsCache.magnitude > deadZoneLS
            ? (av * lsCache.y + dx * lsCache.x).normalized
            : av;

        // Lunghezza: 0.4 m a riposo → 1.8 m a piena potenza
        float lunghezza = Mathf.Lerp(0.4f, 1.8f, rtPeakValue);

        // Curva parabolica: right stick X = deviazione max (in metri) alla punta
        // interno raddoppia la deviazione visiva
        float spinX      = Mathf.Clamp(effectStick.x, -1f, 1f);
        float devMax     = spinX * (internoMode ? 0.9f : 0.5f);
        // Perpendicolare alla direzione di tiro (destra del pallone)
        Vector3 perp = new Vector3(-dir.z, 0f, dir.x);

        // Colore: rosso normale, arancione con LB
        Color colBase = internoMode ? new Color(1f, 0.45f, 0f) : Color.red;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(colBase, 0f),
                new GradientColorKey(colBase, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f,  0f),
                new GradientAlphaKey(0f,  1f)
            }
        );
        lineAim.colorGradient = grad;

        // Disegna la curva: deviazione laterale cresce con t²
        int     n     = lineAim.positionCount;
        Vector3 start = palla.position + Vector3.up * 0.05f;
        for (int i = 0; i < n; i++)
        {
            float   t   = i / (float)(n - 1);
            Vector3 p   = start + dir * (lunghezza * t);
            p += perp * (devMax * t * t);
            lineAim.SetPosition(i, p);
        }

        lineAim.enabled = true;
    }

    // ── tocco ─────────────────────────────────────────────────────────────────
    void EseguiTocco(float intensita)
    {
        if (ballRb == null) return;
        intensita = Mathf.Clamp01(intensita);

        Vector3 avanti = playerCamera.forward; avanti.y = 0f; avanti.Normalize();
        Vector3 destra = playerCamera.right;   destra.y = 0f; destra.Normalize();

        Vector3 dir = lsCache.magnitude >= deadZoneLS
            ? (avanti * lsCache.y + destra * lsCache.x).normalized
            : avanti;

        float forza = Mathf.Lerp(toccoForzaMin, toccoForzaMax, intensita);
        ballRb.AddForce(dir * forza, ForceMode.Impulse);
        ballAudio?.SuonaCalcio(intensita * 0.5f);
        Debug.Log($"[JOYPAD TOCCO] intensita={intensita:F2} | forza={forza:F2}");
    }

    // ── tiro ──────────────────────────────────────────────────────────────────
    void EseguiTiro(float picco, Vector2 effetto)
    {
        if (ballRb == null) return;

        Vector3 avanti = playerCamera.forward; avanti.y = 0f; avanti.Normalize();
        Vector3 destra = playerCamera.right;   destra.y = 0f; destra.Normalize();

        Vector3 dirOrizz = lsCache.magnitude >= deadZoneLS
            ? (avanti * lsCache.y + destra * lsCache.x).normalized
            : avanti;

        // Angolo in base a right stick Y
        float stickY      = Mathf.Clamp(effetto.y, -1f, 1f);
        float angoloGradi = Mathf.Lerp(angoloMinimo, angoloMassimo, (stickY + 1f) / 2f);
        if (internoMode) angoloGradi = Mathf.Max(angoloGradi, 20f);
        float angoloRad  = angoloGradi * Mathf.Deg2Rad;
        Vector3 direzione = Mathf.Cos(angoloRad) * dirOrizz + Mathf.Sin(angoloRad) * Vector3.up;

        float potenza = internoMode ? 0.82f : 1f;
        float forza   = Mathf.Lerp(tiroForzaMin, tiroForzaMax, picco) * potenza;

        ballRb.linearVelocity  = new Vector3(0f, ballRb.linearVelocity.y, 0f);
        palla.position        += Vector3.up * 0.015f;
        ballRb.AddForce(direzione * forza, ForceMode.Impulse);
        ballAudio?.SuonaCalcio(picco);

        // Spin
        float stickX     = Mathf.Clamp(effetto.x, -1f, 1f);
        float spinMaxLat = internoMode ? spinMaxInterno : spinMaxColloDelPiede;
        float spinY      =  stickX * spinMaxLat       * picco;
        float spinX      = -stickY * spinMaxVerticale  * picco;
        ballRb.angularVelocity = new Vector3(spinX, spinY, 0f);

        Debug.Log($"[JOYPAD TIRO] picco={picco:F2} | forza={forza:F2} | angolo={angoloGradi:F1}° | interno={internoMode}");
    }

    // ── reset ─────────────────────────────────────────────────────────────────
    void GestisciReset()
    {
        if (!Gamepad.current.buttonEast.wasPressedThisFrame &&
            !Gamepad.current.startButton.wasPressedThisFrame) return;

        ballLauncher?.ResetPartita();
        ltWasPressed    = false; ltPeakValue = 0f;
        rtWasPressed    = false; rtPeakValue = 0f;
        lineAim.enabled = false;
        hudText         = "";
        Debug.Log("[JOYPAD] Reset partita");
    }

    // ── HUD ───────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        if (!inBallControlMode) return;

        float w = 520f, h = 44f;
        float x = (Screen.width  - w) * 0.5f;
        float y =  Screen.height - 90f;

        GUIStyle stile = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        // Indicatore INTERNO persistente (piccolo, sopra l'HUD principale)
        if (internoMode)
        {
            GUIStyle lbStile = new GUIStyle(stile) { fontSize = 14 };
            lbStile.normal.textColor = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(new Rect(x + 1, y - 25f, w, 24f), "● INTERNO", lbStile);
            lbStile.normal.textColor = new Color(1f, 0.45f, 0f);
            GUI.Label(new Rect(x, y - 26f, w, 24f), "● INTERNO", lbStile);
        }

        if (string.IsNullOrEmpty(hudText)) return;

        stile.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
        GUI.Label(new Rect(x + 1, y + 1, w, h), hudText, stile);

        Color colore = rtWasPressed
            ? (internoMode ? new Color(1f, 0.45f, 0f) : Color.red)
            : Color.yellow;
        stile.normal.textColor = colore;
        GUI.Label(new Rect(x, y, w, h), hudText, stile);
    }
}
