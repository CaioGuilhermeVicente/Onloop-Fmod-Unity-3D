using System;
using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

namespace Gamekit3D
{
    /// <summary>
    /// Substitui o RandomAudioPlayer original por um emissor de evento FMOD.
    /// Mantem a MESMA API publica do original (playing, canPlay,
    /// PlayRandomClip() e PlayRandomClip(Material, int)) para que qualquer
    /// script do jogo que hoje aponte pra RandomAudioPlayer -- footstep,
    /// landing, hurt, emotes de jump/death/attack, hit do MeleeWeapon,
    /// sons dos inimigos -- precise so trocar o TIPO do campo, sem mudar
    /// nenhuma linha de logica de chamada.
    /// </summary>
    public class FMODRandomAudioPlayer : MonoBehaviour
    {
        [Serializable]
        public class MaterialSurfaceOverride
        {
            public Material[] materials;
            public string surfaceLabel; // precisa bater com um label do parametro Surface no FMOD Studio
        }

        [Header("Evento FMOD")]
        public EventReference audioEvent;

        [Header("Parametros (deixe em branco se o evento nao tiver)")]
        public string surfaceParameterName = "Surface";
        public string speedParameterName = "Speed";
        public string defaultSurfaceLabel = "default";

        [Header("Mapeamento de material -> label do parametro Surface")]
        public MaterialSurfaceOverride[] overrides = new MaterialSurfaceOverride[0];

        // Mesmos campos publicos que scripts como o PlayerController ja manipulam diretamente.
        [HideInInspector] public bool playing;
        [HideInInspector] public bool canPlay;

        EventInstance m_Instance;
        Dictionary<Material, string> m_Lookup = new Dictionary<Material, string>();

        void Awake()
        {
            m_Instance = RuntimeManager.CreateInstance(audioEvent);
            RuntimeManager.AttachInstanceToGameObject(m_Instance, transform);

            for (int i = 0; i < overrides.Length; i++)
                foreach (var mat in overrides[i].materials)
                    m_Lookup[mat] = overrides[i].surfaceLabel;
        }

        /// <summary>
        /// Equivalente ao RandomAudioPlayer.PlayRandomClip() sem parametros:
        /// para sons que nao dependem de superficie (hurt, jump, death,
        /// grunt, explosao, e o hit do MeleeWeapon quando o alvo nao tem Renderer).
        /// </summary>
        public void PlayRandomClip()
        {
            m_Instance.start();
        }

        /// <summary>
        /// Equivalente ao RandomAudioPlayer.PlayRandomClip(Material, int):
        /// para sons que variam por superficie e velocidade (footstep, landing,
        /// e o hit do MeleeWeapon quando o alvo tem Renderer).
        /// </summary>
        public void PlayRandomClip(Material overrideMaterial, int bankId = 0)
        {
            if (!string.IsNullOrEmpty(surfaceParameterName))
            {
                string label = defaultSurfaceLabel;
                if (overrideMaterial != null)
                    m_Lookup.TryGetValue(overrideMaterial, out label);
                m_Instance.setParameterByNameWithLabel(surfaceParameterName, label ?? defaultSurfaceLabel);
            }

            if (!string.IsNullOrEmpty(speedParameterName))
                m_Instance.setParameterByName(speedParameterName, bankId);

            m_Instance.start();
        }

        void OnDestroy()
        {
            m_Instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            m_Instance.release();
        }
    }
}
