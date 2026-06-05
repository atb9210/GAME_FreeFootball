using UnityEngine;

public class CampettoBuilder : MonoBehaviour
{
    [Header("Dimensioni campo (metri)")]
    public float lunghezza    = 25f;
    public float larghezza    = 15f;
    public float altezzaMuro  = 1.2f;
    public float spessoreMuro = 0.25f;

    [Header("Aspetto")]
    public Material materialeMuro;
    [Tooltip("Ripetizioni texture per metro — orizzontale")]
    public float tilingX = 1f;
    [Tooltip("Ripetizioni texture per metro — verticale (separa da X per correggere proporzioni)")]
    public float tilingY = 1f;

    void Start()
    {
        // Costruisce al Play se non ci sono gia muri figli (es. se il ContextMenu non era stato usato)
        if (transform.childCount == 0)
        {
            CostruisciMuri();
            AdattaTerreno();
        }
        else
        {
            Debug.Log($"[Campetto] Trovati {transform.childCount} muri gia presenti.");
        }
    }

    [ContextMenu("1 — Costruisci Muri")]
    void CostruisciMuri()
    {
        // Rimuovi muri precedenti (figli di questo GameObject)
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        float lun = lunghezza;
        float lar = larghezza;
        float alt = altezzaMuro;
        float sp  = spessoreMuro;

        // I muri si chiamano Wall_* cosi BallAudio li riconosce gia
        CreaParete("Wall_Fronte",   new Vector3(0,        alt/2f,  lun/2f), new Vector3(lar + sp*2f, alt, sp));
        CreaParete("Wall_Retro",    new Vector3(0,        alt/2f, -lun/2f), new Vector3(lar + sp*2f, alt, sp));
        CreaParete("Wall_Sinistra", new Vector3(-lar/2f,  alt/2f, 0),       new Vector3(sp, alt, lun));
        CreaParete("Wall_Destra",   new Vector3( lar/2f,  alt/2f, 0),       new Vector3(sp, alt, lun));

        Debug.Log($"[Campetto] Costruito {lar}m x {lun}m | altezza muri {alt}m — elimina manualmente il vecchio Wall");
    }

    [ContextMenu("2 — Adatta Terreno alle Dimensioni")]
    void AdattaTerreno()
    {
        GameObject terreno = GameObject.Find("Ground") ?? GameObject.Find("Plane");
        if (terreno == null) { Debug.LogWarning("[Campetto] Terreno non trovato (deve chiamarsi Plane o Ground)!"); return; }

        // Un Plane Unity e 10x10m a scala 1 → divido per 10
        terreno.transform.position   = Vector3.zero;
        terreno.transform.localScale = new Vector3(larghezza / 10f, 1f, lunghezza / 10f);
        Debug.Log($"[Campetto] Terreno adattato a {larghezza}x{lunghezza}m");
    }

    void CreaParete(string nome, Vector3 posizione, Vector3 scala)
    {
        GameObject muro = GameObject.CreatePrimitive(PrimitiveType.Cube);
        muro.name = nome;
        muro.transform.SetParent(transform);
        muro.transform.position   = posizione;
        muro.transform.localScale = scala;
        muro.AddComponent<WallPhysics>();

        if (materialeMuro != null)
        {
            var rend = muro.GetComponent<Renderer>();
            rend.material = new Material(materialeMuro);

            float larghezzaFaccia = Mathf.Max(scala.x, scala.z);
            Vector2 tiling = new Vector2(larghezzaFaccia * tilingX, scala.y * tilingY);

            // Built-in pipeline
            rend.material.mainTextureScale = tiling;
            // URP / HDRP usano _BaseMap invece di _MainTex
            if (rend.material.HasProperty("_BaseMap"))
                rend.material.SetTextureScale("_BaseMap", tiling);
            if (rend.material.HasProperty("_BaseColorMap"))
                rend.material.SetTextureScale("_BaseColorMap", tiling);
        }
    }
}
