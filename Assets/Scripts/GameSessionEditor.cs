#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameSession))]
public sealed class GameSessionEditor : Editor
{
    static readonly GUIContent activePlayersLabel = new("Active Players");
    static readonly GUIContent activePlayerCountLabel = new("Active Player Count");
    static readonly GUIContent[] playerLabels =
    {
        new("Player 1"),
        new("Player 2"),
        new("Player 3"),
        new("Player 4"),
        new("Player 5"),
        new("Player 6")
    };

    public override void OnInspectorGUI()
    {
        DrawScriptField();

        serializedObject.Update();

        GameSession session = (GameSession)target;
        int currentMask = session.ActivePlayerMask;
        int nextMask = DrawActivePlayersMask(currentMask);

        if (nextMask != currentMask)
        {
            Undo.RecordObject(session, "Change Active Players");
            session.SetActivePlayerMask(nextMask);
            EditorUtility.SetDirty(session);
        }

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.IntField(activePlayerCountLabel, session.ActivePlayerCount);

        serializedObject.ApplyModifiedProperties();
    }

    int DrawActivePlayersMask(int currentMask)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(activePlayersLabel, EditorStyles.boldLabel);

        int nextMask = currentMask;
        bool hasChanged = false;

        for (int playerId = GameSession.MinPlayerId; playerId <= GameSession.MaxPlayerId; playerId++)
        {
            int playerMask = 1 << (playerId - 1);
            bool isActive = (currentMask & playerMask) != 0;
            bool nextValue = EditorGUILayout.ToggleLeft(playerLabels[playerId - 1], isActive);

            if (nextValue == isActive)
                continue;

            hasChanged = true;

            if (nextValue)
                nextMask |= playerMask;
            else
                nextMask &= ~playerMask;
        }

        if (hasChanged && nextMask == 0)
        {
            EditorGUILayout.HelpBox("Ative pelo menos um jogador.", MessageType.Warning);
            return currentMask;
        }

        return nextMask;
    }

    void DrawScriptField()
    {
        using (new EditorGUI.DisabledScope(true))
        {
            MonoScript script = MonoScript.FromMonoBehaviour((GameSession)target);
            EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
        }
    }
}
#endif
