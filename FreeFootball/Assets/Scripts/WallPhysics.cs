using UnityEngine;

// Valori basati su:
// - Mattoni rossi: COR stimato 0.62 (superfice ruvida, assorbe ~25% energia in più del cemento)
// - Cemento muro: COR ≈ 0.80 (simile a pavimento cemento ma verticale)
// - Frizione muro mattoni: alta (0.60-0.70) → trasferisce molto spin al rimbalzo
// - Formula: effective_COR = (palla.bounciness + muro.bounciness) / 2 con Average combine
// - Palla ha bounciness=0.83
public class WallPhysics : MonoBehaviour
{
    public enum TipoMuro { MattoniRossi, Cemento, Legno, MetalloLiscio }

    [Header("Preset (tasto destro → Carica Preset)")]
    public TipoMuro tipo = TipoMuro.MattoniRossi;

    [Header("Rimbalzo  |  effective = (palla 0.83 + muro) / 2")]
    [Range(0f, 1f)] public float rimbalzo = 0.41f;
    public PhysicsMaterialCombine combineRimbalzo = PhysicsMaterialCombine.Average;

    [Header("Frizione  |  effective = (palla 0.28 + muro) / 2")]
    [Range(0f, 1f)] public float frizione = 0.62f;
    [Range(0f, 1f)] public float frizioneStatica = 0.68f;
    public PhysicsMaterialCombine combineFrizione = PhysicsMaterialCombine.Average;

    private PhysicsMaterial mat;

    void Start()
    {
        var col = GetComponent<Collider>();
        if (col == null) { Debug.LogWarning("[WallPhysics] Nessun Collider!"); return; }
        mat = new PhysicsMaterial("Muro_" + tipo);
        col.material = mat;
        AggiornaMateriale();
        Debug.Log($"[WallPhysics] {tipo} avviato | COR effettivo ≈ {(0.83f + rimbalzo) / 2f:F2} | frizione eff. ≈ {(0.28f + frizione) / 2f:F2}");
    }

    void Update()
    {
        if (mat != null) AggiornaMateriale();
    }

    void AggiornaMateriale()
    {
        mat.bounciness      = rimbalzo;
        mat.dynamicFriction = frizione;
        mat.staticFriction  = frizioneStatica;
        mat.bounceCombine   = combineRimbalzo;
        mat.frictionCombine = combineFrizione;
    }

    [ContextMenu("Carica Preset")]
    void CaricaPreset()
    {
        switch (tipo)
        {
            case TipoMuro.MattoniRossi:
                // Target COR 0.62 (mattoni assorbono ~25% in più del cemento, sup. ruvida)
                // terreno = 2*0.62 - 0.83 = 0.41
                // Frizione alta (mattoni rugosi): target eff. 0.45 → muro = 2*0.45 - 0.28 = 0.62
                rimbalzo = 0.41f; frizione = 0.62f; frizioneStatica = 0.68f;
                combineRimbalzo = PhysicsMaterialCombine.Average;
                combineFrizione = PhysicsMaterialCombine.Average;
                break;

            case TipoMuro.Cemento:
                // Target COR 0.80 (cemento compatto, poche deformazioni)
                // muro = 2*0.80 - 0.83 = 0.77
                // Frizione media-alta: target eff. 0.38 → muro = 2*0.38 - 0.28 = 0.48
                rimbalzo = 0.77f; frizione = 0.48f; frizioneStatica = 0.54f;
                combineRimbalzo = PhysicsMaterialCombine.Average;
                combineFrizione = PhysicsMaterialCombine.Average;
                break;

            case TipoMuro.Legno:
                // Target COR 0.72 (pannelli legno, palestra)
                // muro = 2*0.72 - 0.83 = 0.61
                // Frizione media: target eff. 0.33 → muro = 2*0.33 - 0.28 = 0.38
                rimbalzo = 0.61f; frizione = 0.38f; frizioneStatica = 0.44f;
                combineRimbalzo = PhysicsMaterialCombine.Average;
                combineFrizione = PhysicsMaterialCombine.Average;
                break;

            case TipoMuro.MetalloLiscio:
                // Target COR 0.88 (metallo quasi elastico, poco assorbimento)
                // muro = 2*0.88 - 0.83 = 0.93
                // Frizione bassissima: target eff. 0.15 → muro = 2*0.15 - 0.28 = 0.02
                rimbalzo = 0.93f; frizione = 0.02f; frizioneStatica = 0.04f;
                combineRimbalzo = PhysicsMaterialCombine.Average;
                combineFrizione = PhysicsMaterialCombine.Minimum;
                break;
        }

        if (mat != null) AggiornaMateriale();
        Debug.Log($"[WallPhysics] Preset {tipo} → COR eff. ≈ {(0.83f + rimbalzo) / 2f:F2} | frizione eff. ≈ {(0.28f + frizione) / 2f:F2}");
    }
}
