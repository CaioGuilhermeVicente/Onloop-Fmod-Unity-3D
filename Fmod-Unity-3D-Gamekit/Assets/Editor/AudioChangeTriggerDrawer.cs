#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using FMODUnity;

/// <summary>
/// Desenha cada AudioAction no Inspector com dropdowns lidos do evento FMOD:
/// lista de parâmetros, slider com min/max do parâmetro e lista de labels.
/// Coloque este arquivo numa pasta "Editor".
/// </summary>
[CustomPropertyDrawer(typeof(AudioChangeTrigger.AudioAction))]
public class AudioChangeTriggerDrawer : PropertyDrawer
{
    const float Gap = 2f;
    static float Line => EditorGUIUtility.singleLineHeight + Gap;

    static EditorEventRef GetEvent(SerializedProperty prop)
    {
        var trig = prop.serializedObject.targetObject as AudioChangeTrigger;
        if (trig == null || trig.emitter == null || trig.emitter.EventReference.IsNull) return null;
        return EventManager.EventFromGUID(trig.emitter.EventReference.Guid);
    }

    public override float GetPropertyHeight(SerializedProperty p, GUIContent label)
    {
        if (!p.isExpanded) return Line;

        int lines = 3; // foldout, função, parâmetro
        var fn = (AudioChangeTrigger.AudioFunction)p.FindPropertyRelative("audioFunction").enumValueIndex;

        if (fn == AudioChangeTrigger.AudioFunction.ChangeValue)
        {
            lines += 4; // usar atual, target, duração, curva
            if (!p.FindPropertyRelative("useCurrentAsStart").boolValue) lines += 1; // start
            lines += 1; // revert on exit
            if (p.FindPropertyRelative("revertOnExit").boolValue) lines += 1; // exit duration
        }
        else lines += 2; // label, revert on exit

        return lines * Line;
    }

    public override void OnGUI(Rect pos, SerializedProperty p, GUIContent label)
    {
        EditorGUI.BeginProperty(pos, label, p);
        var r = new Rect(pos.x, pos.y, pos.width, EditorGUIUtility.singleLineHeight);

        p.isExpanded = EditorGUI.Foldout(r, p.isExpanded, label, true);
        if (!p.isExpanded) { EditorGUI.EndProperty(); return; }

        EditorGUI.indentLevel++;

        var fnProp     = p.FindPropertyRelative("audioFunction");
        var paramProp  = p.FindPropertyRelative("parameter");
        var globalProp = p.FindPropertyRelative("isGlobal");

        r.y += Line;
        EditorGUI.PropertyField(r, fnProp);

        // ---- Dropdown de parâmetros do evento ----
        r.y += Line;
        var evt = GetEvent(p);
        List<EditorParamRef> parms = evt != null ? evt.Parameters : null;
        EditorParamRef selected = null;

        if (parms != null && parms.Count > 0)
        {
            var fn = (AudioChangeTrigger.AudioFunction)fnProp.enumValueIndex;
            // Em Set Label, mostra só parâmetros com labels
            var list = fn == AudioChangeTrigger.AudioFunction.SetLabel
                ? parms.Where(x => x.Type == ParameterType.Labeled).ToList()
                : parms;

            var names = list.Select(x => x.IsGlobal ? x.Name + " (global)" : x.Name).ToArray();
            int idx = list.FindIndex(x => x.Name == paramProp.stringValue);
            int newIdx = EditorGUI.Popup(r, "Parameter", Mathf.Max(idx, 0), names);

            if (list.Count > 0)
            {
                selected = list[newIdx];
                paramProp.stringValue = selected.Name;
                globalProp.boolValue = selected.IsGlobal;
            }
        }
        else
        {
            EditorGUI.LabelField(r, "Parameter", evt == null
                ? "Defina o Emitter (com evento) no componente"
                : "Evento sem parâmetros");
        }

        // ---- Campos de cada função ----
        if ((AudioChangeTrigger.AudioFunction)fnProp.enumValueIndex == AudioChangeTrigger.AudioFunction.ChangeValue)
        {
            var useCur = p.FindPropertyRelative("useCurrentAsStart");
            r.y += Line;
            EditorGUI.PropertyField(r, useCur, new GUIContent("From Current Value"));

            if (!useCur.boolValue)
            {
                r.y += Line;
                ValueField(r, p.FindPropertyRelative("startValue"), "Start Value", selected);
            }

            r.y += Line;
            ValueField(r, p.FindPropertyRelative("targetValue"), "Target Value", selected);

            r.y += Line;
            var dur = p.FindPropertyRelative("lerpDuration");
            dur.floatValue = EditorGUI.Slider(r, "Lerp Duration", dur.floatValue, 0f, 10f);

            r.y += Line;
            EditorGUI.PropertyField(r, p.FindPropertyRelative("valueCurve"));

            var revert = p.FindPropertyRelative("revertOnExit");
            r.y += Line;
            EditorGUI.PropertyField(r, revert);

            if (revert.boolValue)
            {
                r.y += Line;
                var exitDur = p.FindPropertyRelative("exitDuration");
                exitDur.floatValue = EditorGUI.Slider(r, "Exit Duration", exitDur.floatValue, 0f, 10f);
            }
        }
        else
        {
            r.y += Line;
            var labelProp = p.FindPropertyRelative("labelValue");
            if (selected != null && selected.Labels != null && selected.Labels.Length > 0)
            {
                int li = System.Array.IndexOf(selected.Labels, labelProp.stringValue);
                li = EditorGUI.Popup(r, "Label Value", Mathf.Max(li, 0), selected.Labels);
                labelProp.stringValue = selected.Labels[li];
            }
            else
            {
                EditorGUI.LabelField(r, "Label Value", "Escolha um parâmetro com labels");
            }

            r.y += Line;
            EditorGUI.PropertyField(r, p.FindPropertyRelative("revertOnExit"));
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    static void ValueField(Rect r, SerializedProperty prop, string label, EditorParamRef param)
    {
        if (param != null && param.Max > param.Min)
            prop.floatValue = EditorGUI.Slider(r, label, prop.floatValue, param.Min, param.Max);
        else
            prop.floatValue = EditorGUI.FloatField(r, label, prop.floatValue);
    }
}
#endif
