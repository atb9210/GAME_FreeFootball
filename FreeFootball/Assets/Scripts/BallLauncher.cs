using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class BallLauncher : MonoBehaviour
{
    [Header("Lancio")]
    public float forceMassima = 7f;
    public float distanzaMassimaTrascinamento = 4f;
    [Range(0f, 50f)] public float angoloMassimo = 32f;  // gradi lancio colpendo il fondo della palla
    [Range(-10f, 10f)] public float angoloMinimo = -3f; // gradi colpendo la cima (tiro rasoterra/a scendere)

    [Header("Effetto (Left Shift = interno del piede)")]
    public float spinMaxColloDelPiede = 35f;
    public float spinMaxInterno = 60f;
    public float spinMaxVerticale = 45f;
    public float magnusCoefficient = 0.002f;

    [Header("Dribbling / Tiro (Space)")]
    [Tooltip("Sotto questa carica (0-1) il tocco è un dribble, sopra è un tiro vero")]
    [Range(0f, 1f)] public float sogliaDribbling = 0.35f;
    [Tooltip("Forza massima del tocco dribbling")]
    public float forzaDribbling = 3.5f;
    [Tooltip("Secondi per caricare al massimo tenendo Space")]
    public float tempoCaricaMax = 1.5f;

    [Header("Reset / Sblocco")]
    [Tooltip("Click destro per resettare. Controllo torna automaticamente sotto questa velocità (m/s)")]
    public float velocitaSblocco = 0.8f;

    public bool InVolo => lanciata;

    private Rigidbody rb;
    private BallAudio ballAudio;
    private LineRenderer lineIndicatore;
    private Vector3 posizioneIniziale;
    private Vector3 puntoPressione;
    private Vector3 offsetColpo;
    private bool trascinando = false;   // mouse tenuto premuto sulla palla
    private bool inAiming = false;      // mira attiva (mouse giù, in attesa di Space)
    private Vector3 vettoreAim;         // direzione mira dal drag mouse
    private float spaceCharge = 0f;     // 0..1 carica Space corrente
    private bool spaceCharging = false; // Space è tenuto premuto

    private bool lanciata = false;
    private Camera camPrincipale;
    private float timerLog = 0f;
    private float timerSblocco = 0f;
    private PlayerController playerController;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        ballAudio = GetComponent<BallAudio>();
        posizioneIniziale = transform.position;
        camPrincipale = Camera.main;

        rb.mass = 0.43f;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.08f; // ridotto: cemento ha rolling resistance bassa (~0.02-0.04)
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.maxAngularVelocity = 65f; // cap fisico Unity: impedisce spin folli da rimbalzo

        playerController = FindObjectOfType<PlayerController>();
        ImpostaFisicaPallone();
        CreaIndicatore();
    }

    // Chiamato da PlayerController per spingere la palla con WASD (forza continua)
    public void SpingiWASD(Vector3 direzione, float forza)
    {
        if (lanciata) return;
        direzione.y = 0f;
        if (direzione.sqrMagnitude < 0.001f) return;
        rb.AddForce(direzione.normalized * forza, ForceMode.Force);
    }

    public void ResetPartita()
    {
        CancelInvoke();
        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = posizioneIniziale;
        transform.rotation = Quaternion.identity;
        lanciata     = false;
        timerSblocco = 0f;
        Debug.Log("[PALLA] Reset partita");
    }

    void FixedUpdate()
    {
        if (timerSblocco > 0f) timerSblocco -= Time.fixedDeltaTime;
        if (lanciata && timerSblocco <= 0f && rb.linearVelocity.magnitude < velocitaSblocco)
        {
            lanciata = false;
            Debug.Log("[PALLA] Controllo restituito al giocatore (palla ferma)");
        }

        float speed = rb.linearVelocity.magnitude;

        // Log ogni 0.2 secondi mentre la palla e in volo
        timerLog += Time.fixedDeltaTime;
        if (timerLog >= 0.2f)
        {
            timerLog = 0f;
            Vector3 magnus = magnusCoefficient * Vector3.Cross(rb.angularVelocity, rb.linearVelocity);
            Debug.Log($"[PALLA] speed={speed:F2} m/s ({speed * 3.6f:F1} km/h) | " +
                      $"velY={rb.linearVelocity.y:F2} m/s | " +
                      $"spinX={rb.angularVelocity.x:F1} spinY={rb.angularVelocity.y:F1} spinZ={rb.angularVelocity.z:F1} | " +
                      $"Magnus={magnus.magnitude:F3} N");
        }

        if (rb.angularVelocity.magnitude > 65f)
            rb.angularVelocity = rb.angularVelocity.normalized * 65f;

        // spinZ si accumula per frizione laterale ed e fisicamente errato per un pallone
        // che rotola in avanti — smorzarlo impedisce alla palla di curvare fuori dal piano
        Vector3 av = rb.angularVelocity;
        if (Mathf.Abs(av.z) > 3f)
            rb.angularVelocity = new Vector3(av.x, av.y, av.z * 0.80f);

        if (speed < 0.5f || rb.angularVelocity.magnitude < 1f) return;

        Vector3 magnusForce = magnusCoefficient * Vector3.Cross(rb.angularVelocity, rb.linearVelocity);
        magnusForce = Vector3.ClampMagnitude(magnusForce, 1.5f);
        rb.AddForce(magnusForce, ForceMode.Force);
    }

    void ImpostaFisicaPallone()
    {
        var col = GetComponent<Collider>();
        PhysicsMaterial mat = new PhysicsMaterial("Pallone");
        // FIFA standard: COR su superficie dura = 0.83 (rimbalzo 69% da 2m = 1.38m, spec: 1.25-1.55m)
        mat.bounciness = 0.83f;
        // Frizione dinamica 0.28: da ricerca ResearchGate "Coefficient of Friction of Soccer Balls"
        mat.dynamicFriction = 0.28f;
        mat.staticFriction = 0.33f;
        mat.bounceCombine = PhysicsMaterialCombine.Average;
        mat.frictionCombine = PhysicsMaterialCombine.Average;
        col.material = mat;
    }

    void CreaIndicatore()
    {
        lineIndicatore = gameObject.AddComponent<LineRenderer>();
        lineIndicatore.startWidth = 0.04f;
        lineIndicatore.endWidth = 0.01f;
        lineIndicatore.positionCount = 2;
        lineIndicatore.enabled = false;
        lineIndicatore.useWorldSpace = true;
        lineIndicatore.material = new Material(Shader.Find("Sprites/Default"));
        lineIndicatore.startColor = Color.yellow;
        lineIndicatore.endColor = Color.red;
    }

    // ── SPACE: carica tiro in qualsiasi momento (mouse libero) ──────────────────
    void Update()
    {
        if (lanciata) return;
        if (playerController != null && !playerController.InBallControlMode) return;
        if (trascinando) return; // il tocco mouse ha priorità

        if (Keyboard.current.spaceKey.isPressed)
        {
            spaceCharging = true;
            inAiming      = true;
            spaceCharge   = Mathf.Min(spaceCharge + Time.deltaTime / tempoCaricaMax, 1f);

            // Direzione e contatto calcolati dal mouse in tempo reale (anche in movimento)
            vettoreAim  = OttieniPosizioneMouseSulPiano() - transform.position;
            offsetColpo = CalcolaContactPointDaMouse();
            AggiornaFreccia();
        }
        else if (spaceCharging)
        {
            EseguiTiro();
        }
        else if (inAiming)
        {
            CancellaAim();
        }
    }

    // ── MOUSE: tocco / dribble ───────────────────────────────────────────────
    void OnMouseDown()
    {
        if (lanciata) return;
        if (playerController != null && !playerController.InBallControlMode) return;

        trascinando    = true;
        puntoPressione = OttieniPosizioneMouseSulPiano();
        rb.isKinematic = true;
    }

    void OnMouseDrag()
    {
        if (!trascinando) return;
        Vector3 vettore = OttieniPosizioneMouseSulPiano() - puntoPressione;
        vettore = Vector3.ClampMagnitude(vettore, distanzaMassimaTrascinamento);
        // Freccia leggera per il dribble (gialla, corta)
        lineIndicatore.enabled = true;
        lineIndicatore.SetPosition(0, transform.position + Vector3.up * 0.05f);
        lineIndicatore.SetPosition(1, transform.position + Vector3.up * 0.05f + vettore);
    }

    void OnMouseUp()
    {
        if (!trascinando) return;
        trascinando            = false;
        lineIndicatore.enabled = false;
        rb.isKinematic         = false;

        Vector3 vettore = OttieniPosizioneMouseSulPiano() - puntoPressione;
        vettore = Vector3.ClampMagnitude(vettore, distanzaMassimaTrascinamento);
        if (vettore.magnitude < 0.05f) return;

        // Tocco leggero: forza proporzionale al drag, sempre un dribble (mai tiro)
        float distNorm   = Mathf.Clamp01(vettore.magnitude / distanzaMassimaTrascinamento);
        Vector3 dir      = new Vector3(vettore.x, 0f, vettore.z).normalized;
        float forzaTocco = forzaDribbling * distNorm;
        rb.AddForce(dir * forzaTocco, ForceMode.Impulse);
        ballAudio?.SuonaCalcio(distNorm * 0.4f);
        Debug.Log($"[DRIBBLE] dir={dir} | forza={forzaTocco:F2} N*s");
    }

    // ── Helpers Space-shot ───────────────────────────────────────────────────
    void AggiornaFreccia()
    {
        if (vettoreAim.magnitude < 0.05f) { lineIndicatore.enabled = false; return; }
        float lunghezza = Mathf.Max(0.3f, distanzaMassimaTrascinamento * spaceCharge);
        lineIndicatore.enabled = true;
        lineIndicatore.SetPosition(0, transform.position + Vector3.up * 0.05f);
        lineIndicatore.SetPosition(1, transform.position + Vector3.up * 0.05f + vettoreAim.normalized * lunghezza);
    }

    // Calcola il punto di contatto sulla palla dalla posizione dello schermo del mouse
    Vector3 CalcolaContactPointDaMouse()
    {
        float raggio = 0.11f;
        Vector3 ballScreen  = camPrincipale.WorldToScreenPoint(transform.position);
        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector2 diff        = mouseScreen - new Vector2(ballScreen.x, ballScreen.y);

        // Raggio palla in pixel sullo schermo
        Vector3 edgeScreen = camPrincipale.WorldToScreenPoint(
            transform.position + camPrincipale.transform.right * raggio);
        float radiusPx = Vector2.Distance(
            new Vector2(edgeScreen.x, edgeScreen.y),
            new Vector2(ballScreen.x,  ballScreen.y));
        if (radiusPx < 1f) radiusPx = 50f;

        float normX = Mathf.Clamp(diff.x / radiusPx, -1f, 1f); // sinistra/destra → curva
        float normY = Mathf.Clamp(diff.y / radiusPx, -1f, 1f); // su/giù → angolo
        return new Vector3(normX * raggio, normY * raggio, 0f);
    }

    void EseguiTiro()
    {
        float intensita = spaceCharge;
        bool  interno   = Keyboard.current.leftShiftKey.isPressed;
        float potenza   = interno ? 0.82f : 1f;
        CancellaAim();

        if (vettoreAim.magnitude < 0.05f || intensita < 0.02f) return;

        float raggio      = 0.11f;
        float offsetYNorm = Mathf.Clamp(offsetColpo.y / raggio, -1f, 1f);
        float angoloGradi = Mathf.Lerp(angoloMassimo, angoloMinimo, (offsetYNorm + 1f) / 2f);
        if (interno) angoloGradi = Mathf.Max(angoloGradi, 20f);
        float angoloRad = angoloGradi * Mathf.Deg2Rad;

        Vector3 dirOrizz  = new Vector3(vettoreAim.x, 0f, vettoreAim.z).normalized;
        Vector3 direzione = Mathf.Cos(angoloRad) * dirOrizz + Mathf.Sin(angoloRad) * Vector3.up;

        transform.position += Vector3.up * 0.015f;
        rb.AddForce(direzione * forceMassima * intensita * potenza, ForceMode.Impulse);
        ballAudio?.SuonaCalcio(intensita);
        ApplicaSpin(interno, intensita);
        timerLog = 0f;
        Debug.Log($"[LANCIO] carica={intensita:F2} | angolo={angoloGradi:F1}° | forza={forceMassima*intensita*potenza:F2} N*s");
        lanciata     = true;
        timerSblocco = 0.5f;
    }

    void CancellaAim()
    {
        inAiming      = false;
        spaceCharge   = 0f;
        spaceCharging = false;
        lineIndicatore.enabled = false;
    }

    void ApplicaSpin(bool interno, float intensita)
    {
        float raggio = 0.11f;
        float offsetXNorm = Mathf.Clamp(offsetColpo.x / raggio, -1f, 1f);
        float offsetYNorm = Mathf.Clamp(offsetColpo.y / raggio, -1f, 1f);

        // Spin orizzontale Y: curva sinistra/destra
        float spinMaxLat = interno ? spinMaxInterno : spinMaxColloDelPiede;
        float spinY = -offsetXNorm * spinMaxLat * intensita;

        // Spin verticale X: backspin/topspin basato su dove colpisci la palla
        // offsetY < 0 (basso palla) → spinX < 0 → backspin → Magnus force UP = alzata
        // offsetY > 0 (cima palla) → spinX > 0 → topspin → Magnus force DOWN = tuffo
        float spinX = offsetYNorm * spinMaxVerticale * intensita;

        rb.angularVelocity = new Vector3(spinX, spinY, 0f);
    }

    Vector3 OttieniPosizioneMouseSulPiano()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray raggio = camPrincipale.ScreenPointToRay(mousePos);
        Plane piano = new Plane(Vector3.up, transform.position);

        if (piano.Raycast(raggio, out float distanza))
            return raggio.GetPoint(distanza);

        return transform.position;
    }

}
