using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMODUnity;

/// <summary>
/// Coloque num objeto com Collider marcado como "Is Trigger".
/// Ao entrar: executa a lista de ações nos parâmetros do evento.
/// Ao sair: cada ação com "Revert On Exit" volta o parâmetro ao valor
/// que ele tinha antes do player entrar.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AudioChangeTrigger : MonoBehaviour
{
    public enum AudioFunction { ChangeValue, SetLabel }

    [System.Serializable]
    public class AudioAction
    {
        public AudioFunction audioFunction = AudioFunction.ChangeValue;

        // Preenchidos pelo dropdown do Inspector (lidos do evento FMOD)
        public string parameter;
        public bool isGlobal;

        // Change Value
        [Tooltip("Se ligado, parte do valor atual do parâmetro. Se desligado, usa Start Value.")]
        public bool useCurrentAsStart = true;
        public float startValue = 0f;
        public float targetValue = 1f;
        [Tooltip("Tempo em segundos da transição. 0 = instantâneo.")]
        public float lerpDuration = 1f;
        public AnimationCurve valueCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        // Set Label
        public string labelValue;

        // Saída
        [Tooltip("Ao sair do trigger, volta o parâmetro ao valor que tinha antes de entrar.")]
        public bool revertOnExit = true;
        [Tooltip("Tempo em segundos da volta. 0 = instantâneo. (Labels sempre voltam na hora.)")]
        public float exitDuration = 1f;

        // Estado em runtime (não aparece no Inspector)
        [System.NonSerialized] public float previousValue;
        [System.NonSerialized] public bool hasPrevious;
        [System.NonSerialized] public bool reverting;
    }

    [Tooltip("Emitter que está tocando o evento (ex.: a música da fase).")]
    public StudioEventEmitter emitter;

    [Tooltip("Tag do objeto que ativa o trigger.")]
    public string playerTag = "Player";

    [Tooltip("Dispara só na primeira vez que o player entrar.")]
    public bool triggerOnce = false;

    public List<AudioAction> audioFunctions = new List<AudioAction>();

    bool _triggered;
    bool _applied;   // a entrada foi executada e ainda não houve saída
    int _inside;     // quantos colliders do player estão dentro
    readonly Dictionary<string, Coroutine> _running = new Dictionary<string, Coroutine>();

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnDisable()
    {
        _inside = 0;
        _applied = false;
        _running.Clear();
    }

    // ---------------- Entrada / Saída ----------------

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        _inside++;
        if (_inside > 1) return;                 // já estava dentro
        if (triggerOnce && _triggered) return;

        _triggered = true;
        _applied = true;
        foreach (var a in audioFunctions) Enter(a);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        _inside = Mathf.Max(0, _inside - 1);
        if (_inside > 0 || !_applied) return;    // ainda tem parte do player dentro

        _applied = false;
        foreach (var a in audioFunctions) Exit(a);
    }

    void Enter(AudioAction a)
    {
        if (!CanAccess(a)) return;
        StopRunning(a.parameter);

        // Guarda o valor de antes de entrar. Se estava no meio de uma volta,
        // mantém o valor guardado original em vez de pegar o valor do meio.
        if (a.revertOnExit && !a.reverting)
        {
            a.previousValue = GetValue(a);
            a.hasPrevious = true;
        }
        a.reverting = false;

        switch (a.audioFunction)
        {
            case AudioFunction.SetLabel:
                SetLabel(a);
                break;

            case AudioFunction.ChangeValue:
                float from = a.useCurrentAsStart ? GetValue(a) : a.startValue;
                _running[a.parameter] = StartCoroutine(Lerp(a, from, a.targetValue, a.lerpDuration, false));
                break;
        }
    }

    void Exit(AudioAction a)
    {
        if (!a.revertOnExit || !a.hasPrevious) return;
        if (!CanAccess(a)) return;
        StopRunning(a.parameter);

        if (a.audioFunction == AudioFunction.SetLabel || a.exitDuration <= 0f)
        {
            SetValue(a, a.previousValue);
            return;
        }

        a.reverting = true;
        _running[a.parameter] = StartCoroutine(Lerp(a, GetValue(a), a.previousValue, a.exitDuration, true));
    }

    // ---------------- Transição ----------------

    IEnumerator Lerp(AudioAction a, float from, float to, float duration, bool isExit)
    {
        if (duration > 0f)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float k = a.valueCurve.Evaluate(Mathf.Clamp01(t));
                SetValue(a, Mathf.LerpUnclamped(from, to, k));
                yield return null;
            }
        }

        SetValue(a, to);
        if (isExit) a.reverting = false;
        _running.Remove(a.parameter);
    }

    void StopRunning(string parameter)
    {
        if (_running.TryGetValue(parameter, out var c) && c != null)
            StopCoroutine(c);
        _running.Remove(parameter);
    }

    // ---------------- Acesso ao FMOD ----------------

    bool CanAccess(AudioAction a)
    {
        if (string.IsNullOrEmpty(a.parameter)) return false;
        if (a.isGlobal) return true;

        if (emitter == null || !emitter.EventInstance.isValid())
        {
            Debug.LogWarning($"[AudioChangeTrigger] '{name}': emitter vazio ou evento não está tocando.", this);
            return false;
        }
        return true;
    }

    float GetValue(AudioAction a)
    {
        float v = 0f;
        if (a.isGlobal) RuntimeManager.StudioSystem.getParameterByName(a.parameter, out v);
        else emitter.EventInstance.getParameterByName(a.parameter, out v);
        return v;
    }

    void SetValue(AudioAction a, float value)
    {
        if (a.isGlobal) RuntimeManager.StudioSystem.setParameterByName(a.parameter, value);
        else if (emitter != null) emitter.EventInstance.setParameterByName(a.parameter, value);
    }

    void SetLabel(AudioAction a)
    {
        if (a.isGlobal) RuntimeManager.StudioSystem.setParameterByNameWithLabel(a.parameter, a.labelValue);
        else emitter.EventInstance.setParameterByNameWithLabel(a.parameter, a.labelValue);
    }

    void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.15f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
