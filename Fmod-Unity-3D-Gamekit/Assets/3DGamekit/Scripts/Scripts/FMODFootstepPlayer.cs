using UnityEngine;
using FMODUnity;
using FMOD.Studio;

namespace Gamekit3D
{
    /// <summary>
    /// Substitui o RandomAudioPlayer para o caso do footstep da Ellen (e pode ser
    /// reaproveitado para o landingPlayer, que usa a mesma assinatura).
    /// Mantém os mesmos campos publicos "playing" e "canPlay" e o mesmo metodo
    /// PlayRandomClip(Material, int) para que o PlayerController.PlayAudio() nao
    /// precise mudar nada alem do tipo do campo.
    /// </summary>
    public class FMODFootstepPlayer : MonoBehaviour
    {
        [System.Serializable]
        public class MaterialSurfaceOverride
        {
            public Material[] materials;
            public string surfaceLabel; // precisa bater com um label do parametro "Surface" no FMOD Studio
        }

        [Header("Evento FMOD")]
        public EventReference footstepEvent;

        [Header("Mapeamento de material -> label do parametro Surface")]
        public string defaultSurfaceLabel = "default";
        public MaterialSurfaceOverride[] overrides;

        [Header("Parametro de velocidade (opcional)")]
        public string speedParameterName = "Speed"; // continuo, ou troque por labeled se preferir

        // Mesmos campos publicos que o PlayerController.PlayAudio() ja manipula diretamente.
        [HideInInspector] public bool playing;
        [HideInInspector] public bool canPlay;

        EventInstance m_Instance;
        System.Collections.Generic.Dictionary<Material, string> m_Lookup =
            new System.Collections.Generic.Dictionary<Material, string>();

        void Awake()
        {
            m_Instance = RuntimeManager.CreateInstance(footstepEvent);
            RuntimeManager.AttachInstanceToGameObject(m_Instance, transform);

            for (int i = 0; i < overrides.Length; i++)
                foreach (var mat in overrides[i].materials)
                    m_Lookup[mat] = overrides[i].surfaceLabel;
        }

        /// <summary>
        /// Mesma assinatura do RandomAudioPlayer original: material da superficie
        /// e um "bankId" (0 = andando, 1 = correndo) que agora vira o parametro Speed.
        /// </summary>
        public void PlayRandomClip(Material overrideMaterial, int bankId = 0)
        {
            string label = defaultSurfaceLabel;
            if (overrideMaterial != null)
                m_Lookup.TryGetValue(overrideMaterial, out label);

            m_Instance.setParameterByNameWithLabel("Surface", label ?? defaultSurfaceLabel);
            m_Instance.setParameterByName(speedParameterName, bankId); // 0 ou 1 -> crossfade/switch no evento
            m_Instance.start();
        }

        void OnDestroy()
        {
            m_Instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            m_Instance.release();
        }
    }
}
