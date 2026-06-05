using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BallAudio : MonoBehaviour
{
    [Header("Clip — trascina i file audio qui")]
    public AudioClip suonoCalcio;
    public AudioClip suonoMuro;
    public AudioClip suonoTerreno;

    [Header("Volume base")]
    [Range(0f, 1f)] public float volumeCalcio  = 1.0f;
    [Range(0f, 1f)] public float volumeMuro    = 1.0f;
    [Range(0f, 1f)] public float volumeTerreno = 0.7f;

    [Header("Soglie impatto")]
    public float sogliaImpatto     = 0.5f;  // OnCollisionEnter: impatti forti (muro, primo atterraggio)
    public float sogliaMicroBounce = 0.15f; // Update: micro-rimbalzi sul terreno

    [Header("Variazione pitch per realismo")]
    [Range(0f, 0.3f)] public float variazionePitch = 0.12f;

    private AudioSource src;
    private Rigidbody rb;
    private float velYPrec = 0f;
    private float cooldown = 0f;

    [Header("Audio 3D — distanza")]
    public float distanzaMinima = 8f;   // dentro questa distanza = volume massimo
    public float distanzaMassima = 30f; // oltre questa distanza = silenzio

    void Awake()
    {
        src = GetComponent<AudioSource>();
        src.playOnAwake = false;
        src.Stop();
        src.clip = null;

        // 3D audio completo: il volume scala con la distanza dalla camera
        src.spatialBlend = 1.0f;
        src.rolloffMode = AudioRolloffMode.Logarithmic; // calo realistico (non lineare)
        src.minDistance = distanzaMinima;
        src.maxDistance = distanzaMassima;
        src.dopplerLevel = 0.3f; // leggero effetto doppler quando la palla passa vicino

        rb = GetComponent<Rigidbody>();
    }

    public void SuonaCalcio(float intensita)
    {
        if (suonoCalcio == null) return;
        src.pitch = 1f + Random.Range(-variazionePitch, variazionePitch);
        src.PlayOneShot(suonoCalcio, volumeCalcio * Mathf.Clamp01(0.4f + intensita * 0.6f));
    }

    void Update()
    {
        // Sincronizza le distanze 3D con i valori dell'Inspector in tempo reale
        src.minDistance = distanzaMinima;
        src.maxDistance = distanzaMassima;

        if (rb == null || suonoTerreno == null) return;

        cooldown -= Time.deltaTime;

        float velY = rb.linearVelocity.y;

        // Rimbalzo rilevato: velY era negativo (scendeva) e ora e positivo (risale)
        // Questo cattura anche i micro-rimbalzi che OnCollisionEnter non vede
        if (velYPrec < -sogliaMicroBounce && velY > 0.02f && cooldown <= 0f)
        {
            float impatto = Mathf.Abs(velYPrec);
            float volScale = Mathf.Max(Mathf.Sqrt(impatto / 5f), 0.2f);
            volScale = Mathf.Clamp01(volScale);
            src.pitch = 1f + Random.Range(-variazionePitch, variazionePitch);
            src.PlayOneShot(suonoTerreno, volumeTerreno * volScale);
            cooldown = 0.08f; // evita doppio trigger nello stesso rimbalzo
            Debug.Log($"[AUDIO-BOUNCE] micro-rimbalzo | velYPrec={velYPrec:F2} | volume={volumeTerreno * volScale:F2}");
        }

        velYPrec = velY;
    }

    // OnCollisionEnter gestisce il primo impatto forte (muro + primo atterraggio)
    void OnCollisionEnter(Collision col)
    {
        float velocita = col.relativeVelocity.magnitude;
        if (velocita < sogliaImpatto) return;

        string nome = col.gameObject.name.ToLower();
        bool eMuro    = nome.Contains("wall") || nome.Contains("muro");
        bool eTerreno = nome.Contains("plane") || nome.Contains("ground");

        float volScale = Mathf.Max(Mathf.Sqrt(velocita / 10f), 0.25f);
        src.pitch = 1f + Random.Range(-variazionePitch, variazionePitch);

        if (eMuro && suonoMuro != null)
        {
            src.PlayOneShot(suonoMuro, volumeMuro * volScale);
            Debug.Log($"[AUDIO] MURO | vel={velocita:F1} | volume={volumeMuro * volScale:F2}");
        }
        else if (eTerreno && suonoTerreno != null)
        {
            // Il primo atterraggio forte lo gestiamo qui, i successivi li prende Update
            if (cooldown <= 0f)
            {
                src.PlayOneShot(suonoTerreno, volumeTerreno * volScale);
                cooldown = 0.08f;
                Debug.Log($"[AUDIO] TERRENO | vel={velocita:F1} | volume={volumeTerreno * volScale:F2}");
            }
        }
    }
}
