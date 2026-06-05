using UnityEngine;

// Valori basati su:
// - FIFA drop test spec: COR 0.79-0.88 su superficie dura (drop 2m, rimbalzo 1.25-1.55m)
// - ResearchGate "Coefficient of Friction of Soccer Balls": frizione dinamica 0.25-0.35
// - Formula Unity: effective_COR = (palla.bounciness + terreno.bounciness) / 2 con Average combine
// - Palla ha bounciness=0.83 → per COR target, terreno = 2*target - 0.83
public class GroundPhysics : MonoBehaviour
{
    public enum TipoTerreno { Cemento, Erba, Parquet, Terra }

    [Header("Preset (tasto destro → Carica Preset)")]
    public TipoTerreno tipo = TipoTerreno.Cemento;

    [Header("Rimbalzo  |  effective = (palla 0.83 + terreno) / 2")]
    [Range(0f, 1f)] public float rimbalzo = 0.83f;
    public PhysicsMaterialCombine combineRimbalzo = PhysicsMaterialCombine.Average;

    [Header("Frizione  |  effective = (palla 0.28 + terreno) / 2")]
    [Range(0f, 1f)] public float frizione = 0.42f;
    [Range(0f, 1f)] public float frizioneStatica = 0.48f;
    public PhysicsMaterialCombine combineFrizione = PhysicsMaterialCombine.Average;

    private PhysicsMaterial mat;

    void Start()
    {
        var col = GetComponent<Collider>();
        if (col == null) { Debug.LogWarning("[GroundPhysics] Nessun Collider!"); return; }
        mat = new PhysicsMaterial("Terreno_" + tipo);
        col.material = mat;
        AggiornaMateriale();
        Debug.Log($"[GroundPhysics] {tipo} avviato | COR effettivo ≈ {(0.83f + rimbalzo) / 2f:F2} | frizione eff. ≈ {(0.28f + frizione) / 2f:F2}");
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
            case TipoTerreno.Cemento:
                // Target COR 0.83 (FIFA max) → terreno = 2*0.83 - 0.83 = 0.83
                // Frizione: rolling resistance cemento μ≈0.03, frizione laterale ≈0.35
                // Target frizione eff. 0.35 → terreno = 2*0.35 - 0.28 = 0.42
                rimbalzo = 0.83f; frizione = 0.42f; frizioneStatica = 0.48f;
                combineRimbalzo = PhysicsMaterialCombine.Average;
                combineFrizione = PhysicsMaterialCombine.Average;
                break;

            case TipoTerreno.Erba:
                // Target COR 0.68 (FIFA grass spec: rimbalzo ~46% da 2m) → terreno = 2*0.68 - 0.83 = 0.53
                // Frizione alta su erba: target eff. 0.55 → terreno = 2*0.55 - 0.28 = 0.82
                rimbalzo = 0.53f; frizione = 0.82f; frizioneStatica = 0.88f;
                combineRimbalzo = PhysicsMaterialCombine.Average;
                combineFrizione = PhysicsMaterialCombine.Average;
                break;

            case TipoTerreno.Parquet:
                // Target COR 0.78 (legno duro, palestra) → terreno = 2*0.78 - 0.83 = 0.73
                // Frizione media: target eff. 0.32 → terreno = 2*0.32 - 0.28 = 0.36
                rimbalzo = 0.73f; frizione = 0.36f; frizioneStatica = 0.42f;
                combineRimbalzo = PhysicsMaterialCombine.Average;
                combineFrizione = PhysicsMaterialCombine.Average;
                break;

            case TipoTerreno.Terra:
                // Target COR 0.58 (terra compatta, assorbe molto) → terreno = 2*0.58 - 0.83 = 0.33
                // Frizione alta: target eff. 0.60 → terreno = 2*0.60 - 0.28 = 0.92
                rimbalzo = 0.33f; frizione = 0.92f; frizioneStatica = 0.95f;
                combineRimbalzo = PhysicsMaterialCombine.Average;
                combineFrizione = PhysicsMaterialCombine.Average;
                break;
        }

        if (mat != null) AggiornaMateriale();
        Debug.Log($"[GroundPhysics] Preset {tipo} → COR eff. ≈ {(0.83f + rimbalzo) / 2f:F2} | frizione eff. ≈ {(0.28f + frizione) / 2f:F2}");
    }
}
