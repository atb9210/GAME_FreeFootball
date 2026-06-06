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

    [Header("Dribbling")]
    [Tooltip("Sotto questa intensità (0-1) il tocco è un dribble leggero, sopra è un tiro vero")]
    [Range(0f, 1f)] public float sogliaDribbling = 0.35f;
    [Tooltip("Forza massima del tocco dribbling")]
    public float forzaDribbling = 3.5f;

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
    private bool trascinando = false;
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

        playerController = FindAnyObjectByType<PlayerController>();
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

        timerLog += Time.fixedDeltaTime;
        if (timerLog >= 0.2f) timerLog = 0f; // log disabilitato

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

    void OnMouseDown()
    {
        if (lanciata) return;
        if (playerController != null && !playerController.InBallControlMode) return;

        trascinando    = true;
        puntoPressione = OttieniPosizioneMouseSulPiano();

        Ray ray = camPrincipale.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.gameObject == gameObject)
            offsetColpo = hit.point - transform.position;
        else
            offsetColpo = Vector3.zero;

        rb.isKinematic = true;
    }

    void OnMouseDrag()
    {
        if (!trascinando) return;
        Vector3 vettore = OttieniPosizioneMouseSulPiano() - puntoPressione;
        vettore = Vector3.ClampMagnitude(vettore, distanzaMassimaTrascinamento);
        lineIndicatore.enabled = true;
        lineIndicatore.SetPosition(0, transform.position + Vector3.up * 0.05f);
        lineIndicatore.SetPosition(1, transform.position + Vector3.up * 0.05f + vettore);
    }

    void OnMouseUp()
    {
        if (!trascinando) return;
        trascinando            = false;
        lineIndicatore.enabled = false;

        Vector3 vettore = OttieniPosizioneMouseSulPiano() - puntoPressione;
        vettore = Vector3.ClampMagnitude(vettore, distanzaMassimaTrascinamento);

        if (vettore.magnitude < 0.1f) { rb.isKinematic = false; return; }

        float intensita = vettore.magnitude / distanzaMassimaTrascinamento;
        bool  interno   = Keyboard.current.leftShiftKey.isPressed;
        float potenza   = interno ? 0.82f : 1f;

        float raggio      = 0.11f;
        float offsetYNorm = Mathf.Clamp(offsetColpo.y / raggio, -1f, 1f);
        float angoloGradi = Mathf.Lerp(angoloMassimo, angoloMinimo, (offsetYNorm + 1f) / 2f);
        if (interno) angoloGradi = Mathf.Max(angoloGradi, 20f);
        float angoloRad = angoloGradi * Mathf.Deg2Rad;

        Vector3 dirOrizz  = new Vector3(vettore.x, 0f, vettore.z).normalized;
        Vector3 direzione = Mathf.Cos(angoloRad) * dirOrizz + Mathf.Sin(angoloRad) * Vector3.up;

        rb.isKinematic = false;

        if (intensita < sogliaDribbling)
        {
            float forzaTocco = forzaDribbling * intensita / sogliaDribbling;
            rb.AddForce(dirOrizz * forzaTocco, ForceMode.Impulse);
            ballAudio?.SuonaCalcio(intensita * 0.4f);
            Debug.Log($"[DRIBBLE] intensita={intensita:F2} | forza={forzaTocco:F2} N*s");
        }
        else
        {
            transform.position += Vector3.up * 0.015f;
            rb.AddForce(direzione * forceMassima * intensita * potenza, ForceMode.Impulse);
            ballAudio?.SuonaCalcio(intensita);
            ApplicaSpin(interno, intensita);
            timerLog = 0f;
            Debug.Log($"[LANCIO] intensita={intensita:F2} | tipo={(interno?"INTERNO":"COLLO")} | angolo={angoloGradi:F1}° | forza={forceMassima*intensita*potenza:F2} N*s");
            lanciata     = true;
            timerSblocco = 0.5f;
        }
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
