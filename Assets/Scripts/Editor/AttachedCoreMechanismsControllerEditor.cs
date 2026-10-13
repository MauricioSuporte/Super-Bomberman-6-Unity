using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AttachedCoreMechanismsController))]
public sealed class AttachedCoreMechanismsControllerEditor : Editor
{
    int direction;
    int enemyFrame;
    int coreFrame;
    float zoom = 6f;
    bool playLoop;
    double playbackStarted;
    static readonly string[] Directions = { "Up", "Down", "Left", "Right" };
    static readonly string[] OffsetFields = { "upFrameOffsets", "downFrameOffsets", "leftFrameOffsets", "rightFrameOffsets" };

    void OnEnable() => EditorApplication.update += UpdatePreview;
    void OnDisable() => EditorApplication.update -= UpdatePreview;

    void UpdatePreview()
    {
        if (!playLoop || target == null)
            return;
        var attachment = (AttachedCoreMechanismsController)target;
        if (attachment.host == null)
            return;
        double elapsed = EditorApplication.timeSinceStartup - playbackStarted;
        AnimatedSpriteRenderer animation = GetAnimation(attachment);
        if (animation != null && animation.animationSprite != null && animation.animationSprite.Length > 0)
            enemyFrame = GetLoopFrame(animation, elapsed);
        if (attachment.attachedFrames != null && attachment.attachedFrames.Length > 0)
            coreFrame = (int)(elapsed / Mathf.Max(0.01f, attachment.frameSeconds) % attachment.attachedFrames.Length);
        Repaint();
    }

    static int GetLoopFrame(AnimatedSpriteRenderer animation, double elapsed)
    {
        int count = animation.animationSprite.Length;
        int steps = animation.pingPong && count > 1 ? count * 2 - 2 : count;
        bool customDurations = animation.frameDurations != null && animation.frameDurations.Length == count;
        float defaultDuration = animation.useSequenceDuration && !customDurations
            ? animation.sequenceDuration / steps : animation.animationTime;
        double cycle = 0;
        for (int step = 0; step < steps; step++)
        {
            int frame = step < count ? step : steps - step;
            cycle += Mathf.Max(0.0001f, customDurations ? animation.frameDurations[frame] : defaultDuration);
        }
        double remaining = elapsed % cycle;
        for (int step = 0; step < steps; step++)
        {
            int frame = step < count ? step : steps - step;
            remaining -= Mathf.Max(0.0001f, customDurations ? animation.frameDurations[frame] : defaultDuration);
            if (remaining < 0)
                return frame;
        }
        return 0;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var attachment = (AttachedCoreMechanismsController)target;
        if (attachment.host == null || attachment.attachedRenderer == null)
            return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Attachment Preview (no Play Mode)", EditorStyles.boldLabel);
        int previousDirection = direction;
        if (attachment.additionalAnimations != null && attachment.additionalAnimations.Length > 0)
        {
            var labels = new string[4 + attachment.additionalAnimations.Length];
            System.Array.Copy(Directions, labels, 4);
            for (int i = 0; i < attachment.additionalAnimations.Length; i++)
            {
                var entry = attachment.additionalAnimations[i];
                labels[4 + i] = entry != null && entry.animation != null
                    ? entry.animation.name + (entry.mirrored ? " (Right / mirrored)" : "") : "Unassigned";
            }
            direction = EditorGUILayout.Popup("Host animation", Mathf.Clamp(direction, 0, labels.Length - 1), labels);
        }
        else
            direction = GUILayout.Toolbar(Mathf.Clamp(direction, 0, 3), Directions);
        bool nextPlayLoop = EditorGUILayout.ToggleLeft("Play animation loop (enemy + Core)", playLoop);
        if (nextPlayLoop != playLoop || previousDirection != direction)
        {
            playbackStarted = EditorApplication.timeSinceStartup;
            playLoop = nextPlayLoop;
        }
        AnimatedSpriteRenderer animation = GetAnimation(attachment);
        if (animation == null || animation.animationSprite == null || animation.animationSprite.Length == 0)
            return;
        using (new EditorGUI.DisabledScope(playLoop))
        {
            enemyFrame = EditorGUILayout.IntSlider("Enemy frame (index)", enemyFrame, 0, animation.animationSprite.Length - 1);
            if (attachment.attachedFrames != null && attachment.attachedFrames.Length > 0)
                coreFrame = EditorGUILayout.IntSlider("Core frame (index)", coreFrame, 0, attachment.attachedFrames.Length - 1);
        }
        zoom = EditorGUILayout.Slider("Zoom", zoom, 2f, 12f);

        serializedObject.Update();
        SerializedProperty offsets = direction < 4 ? serializedObject.FindProperty(OffsetFields[direction])
            : serializedObject.FindProperty("additionalAnimations").GetArrayElementAtIndex(direction - 4).FindPropertyRelative("frameOffsets");
        if (offsets.arraySize <= enemyFrame)
        {
            if (GUILayout.Button("Create offsets for every animation frame"))
            {
                int previousSize = offsets.arraySize;
                offsets.arraySize = animation.animationSprite.Length;
                for (int i = previousSize; i < offsets.arraySize; i++)
                    offsets.GetArrayElementAtIndex(i).vector2Value = Vector2.zero;
            }
        }
        Vector2 offset = Vector2.zero;
        if (offsets.arraySize > enemyFrame)
        {
            SerializedProperty frameOffset = offsets.GetArrayElementAtIndex(enemyFrame);
            using (new EditorGUI.DisabledScope(playLoop))
                EditorGUILayout.PropertyField(frameOffset, new GUIContent("Frame offset (pixels)"));
            offset = frameOffset.vector2Value;
        }
        serializedObject.ApplyModifiedProperties();

        Sprite enemy = animation.animationSprite[enemyFrame];
        Sprite core = attachment.attachedFrames != null && attachment.attachedFrames.Length > 0
            ? attachment.attachedFrames[coreFrame] : attachment.attachedRenderer.sprite;
        float ppu = Mathf.Max(1f, attachment.pixelsPerUnit);
        Vector3 corePosition = attachment.backOffset + (Vector3)(offset / ppu) + attachment.attachedRenderer.transform.localPosition;
        Vector3 enemyPosition = attachment.host.transform.InverseTransformPoint(animation.transform.position);
        if (direction < 4 && attachment.host is SubmarineBubbleMovementController submarine)
        {
            float time = playLoop ? (float)(EditorApplication.timeSinceStartup - playbackStarted) : 0f;
            Vector3 hover = submarine.GetWalkingHoverOffset(time);
            enemyPosition += hover;
            corePosition += hover;
        }
        corePosition.x = Mathf.Round(corePosition.x * ppu) / ppu;
        corePosition.y = Mathf.Round(corePosition.y * ppu) / ppu;
        Rect area = GUILayoutUtility.GetRect(100f, Mathf.Max(180f, 48f * zoom), GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(area, new Color(0.12f, 0.17f, 0.22f));
        Vector2 center = area.center + new Vector2(0f, 8f * zoom);
        EditorGUI.DrawRect(new Rect(center.x - 1f, area.y, 1f, area.height), new Color(1f, 1f, 1f, 0.15f));
        EditorGUI.DrawRect(new Rect(area.x, center.y, area.width, 1f), new Color(1f, 1f, 1f, 0.15f));
        bool flip = direction < 4 ? direction == 3 && attachment.host.spriteRight == null
            : attachment.additionalAnimations[direction - 4].mirrored;
        SpriteRenderer enemyRenderer = animation.GetComponent<SpriteRenderer>();
        bool coreOnTop = enemyRenderer == null ||
            SortingLayer.GetLayerValueFromID(attachment.attachedRenderer.sortingLayerID) > SortingLayer.GetLayerValueFromID(enemyRenderer.sortingLayerID) ||
            (attachment.attachedRenderer.sortingLayerID == enemyRenderer.sortingLayerID && attachment.attachedRenderer.sortingOrder >= enemyRenderer.sortingOrder);
        if (!coreOnTop)
            DrawSprite(core, corePosition, center, ppu * zoom, false);
        DrawSprite(enemy, enemyPosition, center, ppu * zoom, flip);
        if (coreOnTop)
            DrawSprite(core, corePosition, center, ppu * zoom, false);
        EditorGUILayout.HelpBox("Frame indices start at 0. Offsets are in pixels and are saved on this component. The preview does not change scene sprites or run gameplay.", MessageType.Info);
    }

    AnimatedSpriteRenderer GetAnimation(AttachedCoreMechanismsController attachment)
    {
        if (direction >= 4)
            return attachment.additionalAnimations != null && direction - 4 < attachment.additionalAnimations.Length
                ? attachment.additionalAnimations[direction - 4]?.animation : null;
        switch (direction)
        {
            case 0: return attachment.host.spriteUp;
            case 1: return attachment.host.spriteDown;
            case 2: return attachment.host.spriteLeft;
            default: return attachment.host.spriteRight != null ? attachment.host.spriteRight : attachment.host.spriteLeft;
        }
    }

    static void DrawSprite(Sprite sprite, Vector3 position, Vector2 center, float scale, bool flip)
    {
        if (sprite == null)
            return;
        Rect pixels = sprite.rect;
        float width = pixels.width / sprite.pixelsPerUnit * scale;
        float height = pixels.height / sprite.pixelsPerUnit * scale;
        Vector2 pivot = sprite.pivot / sprite.pixelsPerUnit * scale;
        if (flip)
            pivot.x = width - pivot.x;
        Rect destination = new Rect(center.x + position.x * scale - pivot.x,
            center.y - position.y * scale - (height - pivot.y), width, height);
        Rect source = new Rect(pixels.x / sprite.texture.width, pixels.y / sprite.texture.height,
            pixels.width / sprite.texture.width, pixels.height / sprite.texture.height);
        if (flip)
        {
            source.x += source.width;
            source.width = -source.width;
        }
        GUI.DrawTextureWithTexCoords(destination, sprite.texture, source, true);
    }
}
