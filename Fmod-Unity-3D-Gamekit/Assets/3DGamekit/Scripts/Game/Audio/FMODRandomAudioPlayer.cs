using System;
using System.Collections;
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
    ///
    /// Extras em relacao ao original:
    ///  - Stop(): substitui o antigo audioSource.Stop().
    ///  - DestroyWhenFinished(): substitui o antigo Destroy(player, clip.length + 0.5f).
    ///  - Se nenhum evento for atribuido, o player fica em silencio (sem erro),
    ///    o que permite trocar os componentes de um prefab e ir criando os
    ///    eventos aos poucos.
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
            for (int i = 0; i < overrides.Length; i++)
                foreach (var mat in overrides[i].materials)
                    m_Lookup[mat] = overrides[i].surfaceLabel;

            // Sem evento atribuido: nao cria instancia, e todos os metodos abaixo viram no-op.
            if (audioEvent.IsNull)
                return;

            m_Instance = RuntimeManager.CreateInstance(audioEvent);
            RuntimeManager.AttachInstanceToGameObject(m_Instance, transform);
        }

        /// <summary>
        /// Equivalente ao RandomAudioPlayer.PlayRandomClip() sem parametros:
        /// para sons que nao dependem de superficie (hurt, jump, death,
        /// grunt, explosao, e o hit do MeleeWeapon quando o alvo nao tem Renderer).
        /// </summary>
        public void PlayRandomClip()
        {
            if (!m_Instance.isValid())
                return;

            m_Instance.start();
        }

        /// <summary>
        /// Equivalente ao RandomAudioPlayer.PlayRandomClip(Material, int):
        /// para sons que variam por superficie e velocidade (footstep, landing,
        /// e o hit do MeleeWeapon quando o alvo tem Renderer).
        /// </summary>
        public void PlayRandomClip(Material overrideMaterial, int bankId = 0)
        {
            PlayRandomClip(overrideMaterial, (float)bankId);
        }

        /// <summary>
        /// Versao com velocidade continua: speed01 vai de 0 (parado) a 1 (velocidade
        /// maxima) e e enviado direto ao parametro Speed do evento, permitindo
        /// crossfade suave entre andar e correr dentro do FMOD Studio.
        /// </summary>
        public void PlayRandomClip(Material overrideMaterial, float speed01)
        {
            if (!m_Instance.isValid())
                return;

            if (!string.IsNullOrEmpty(surfaceParameterName))
            {
                string label = defaultSurfaceLabel;
                if (overrideMaterial != null)
                    m_Lookup.TryGetValue(overrideMaterial, out label);
                m_Instance.setParameterByNameWithLabel(surfaceParameterName, label ?? defaultSurfaceLabel);
            }

            if (!string.IsNullOrEmpty(speedParameterName))
                m_Instance.setParameterByName(speedParameterName, speed01);

            m_Instance.start();
        }

        /// <summary>
        /// Equivalente ao antigo audioSource.Stop(). Respeita o fade-out configurado
        /// no evento (se o evento nao tiver fade-out, o corte e imediato).
        /// </summary>
        public void Stop()
        {
            if (m_Instance.isValid())
                m_Instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        }

        /// <summary>
        /// Equivalente ao antigo Destroy(player, clip.length + 0.5f). Como o FMOD nao
        /// expoe um AudioClip, espera o evento terminar de tocar e so entao destroi este
        /// componente. maxSeconds e um limite de seguranca caso o evento nunca termine.
        /// </summary>
        public void DestroyWhenFinished(float maxSeconds = 10f)
        {
            if (!m_Instance.isValid())
            {
                Destroy(this);
                return;
            }

            StartCoroutine(DestroyWhenFinishedRoutine(maxSeconds));
        }

        IEnumerator DestroyWhenFinishedRoutine(float maxSeconds)
        {
            // O start() do FMOD e assincrono: logo depois dele o estado ainda pode constar
            // como STOPPED. Esperar um instante evita destruir o som antes de ele comecar.
            const float startGrace = 0.25f;
            yield return new WaitForSeconds(startGrace);

            float elapsed = startGrace;
            PLAYBACK_STATE state = PLAYBACK_STATE.PLAYING;
            while (elapsed < maxSeconds && m_Instance.isValid())
            {
                m_Instance.getPlaybackState(out state);
                if (state == PLAYBACK_STATE.STOPPED)
                    break;

                yield return null;
                elapsed += Time.deltaTime;
            }

            Destroy(this);
        }

        void OnDestroy()
        {
            if (!m_Instance.isValid())
                return;

            m_Instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            m_Instance.release();
        }
    }
}
