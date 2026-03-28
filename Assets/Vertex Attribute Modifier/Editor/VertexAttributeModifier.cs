using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

[FilePath("Managers/StateFile.foo", FilePathAttribute.Location.ProjectFolder)]
public class VertexAttributeModifier : ScriptableSingleton<VertexAttributeModifier>
{
    public List<MeshRule> meshRules = new List<MeshRule>();
        
    private void OnValidate()
    {
        if (meshRules == null)
        {
            meshRules = new List<MeshRule>();
        }

        foreach (var rule in meshRules)
        {
            foreach (var entry in rule.vertexAttributeOverrides)
            {
                entry?.Normalize();
            }
        }
    }
        
    public MeshRule FindMatchingRule(string assetPath)
    {
        if (meshRules == null)
        {
            return null;
        }

        foreach (var rule in meshRules)
        {
            if (rule != null && rule.MatchesAssetPath(assetPath))
            {
                return rule;
            }
        }

        return null;
    }
    
    public AttributeOverride GetAttributeOverride(List<AttributeOverride> attributeOverrides, VertexAttribute attribute)
    {
        foreach (var attributeOverride in attributeOverrides)
        {
            if (attributeOverride.attribute == attribute)
            {
                return attributeOverride;
            }
        }

        return null;
    }

    public void ApplySingleRule(int index)
    {
        var rule = meshRules[index];
        
        foreach (var folder in rule.sourceDirectories)
        {
            var folderPath = AssetDatabase.GetAssetPath(folder);

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                break;
            }

            var guids = AssetDatabase.FindAssets("t:Mesh", new[] { folderPath });

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                
                ApplyModifications(mesh, rule);
            }
        }
        
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    public void ApplyAllRules()
    {
        for (int i = 0; i < meshRules.Count; i++)
        {
            ApplySingleRule(i);
        }
    }

    public void DoSave()
    {
        Save(true);
    }
    
    public void ApplyModifications(Mesh mesh, MeshRule rule)
    {
        var attributesToRemove = new HashSet<int>();
        var modified = false;
        
        var descriptors = mesh.GetVertexAttributes();
        for (var i = 0; i < descriptors.Length; i++)
        {
            var targetAttribute = descriptors[i].attribute;
            var attributeOverride = GetAttributeOverride(rule.vertexAttributeOverrides, targetAttribute);

            if (attributeOverride == null)
            {
                continue;
            }

            if (attributeOverride.modification == VertexAttributeModification.Strip)
            {
                attributesToRemove.Add(i);
                modified = true;
            }
            else if (attributeOverride.modification == VertexAttributeModification.Override)
            {
                if (descriptors[i].format != attributeOverride.format)
                {
                    descriptors[i].format = attributeOverride.format;
                    modified = true;
                }

                if (descriptors[i].dimension != attributeOverride.dimension)
                {
                    descriptors[i].dimension = attributeOverride.dimension;
                    modified = true;
                }

                /*if (descriptors[i].stream != attributeOverride.stream)
                {
                    descriptors[i].stream = attributeOverride.stream;
                    modified = true;
                }*/
            }
        }

        if (!modified)
        {
            return;
        }

        if (attributesToRemove.Count > 0)
        {
            var newCount = descriptors.Length - attributesToRemove.Count;
            var newArray = new VertexAttributeDescriptor[newCount];
            var adjustedIndex = 0;
            for (int i = 0; i < descriptors.Length; i++)
            {
                if (attributesToRemove.Contains(i))
                {
                    continue;
                }

                newArray[adjustedIndex] = descriptors[i];

                adjustedIndex++;
            }
            mesh.SetVertexBufferParams(mesh.vertexCount, newArray);
            return;
        }
        
        mesh.SetVertexBufferParams(mesh.vertexCount, descriptors);
    }
}

[CustomEditor(typeof(VertexAttributeModifier))]
public class VertexAttributeModifierEditor : Editor
{
    Vector2 scrollPosition;
    
    public void DrawGUI()
    {
        serializedObject.Update();
        SerializedProperty rulesProp = serializedObject.FindProperty("meshRules");
        
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Rules", EditorStyles.boldLabel);
                
            if (GUILayout.Button("Apply All", GUILayout.Width(80)))
            {
                VertexAttributeModifier.instance.ApplyAllRules();
            }
        }

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        for (int i = 0; i < rulesProp.arraySize; i++)
        {
            SerializedProperty ruleProp = rulesProp.GetArrayElementAtIndex(i);
            DrawRule(ruleProp, i, rulesProp);
            EditorGUILayout.Space(6f);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Rule"))
            {
                int newIndex = rulesProp.arraySize;
                rulesProp.arraySize++;

                SerializedProperty newRule = rulesProp.GetArrayElementAtIndex(newIndex);
                
                SerializedProperty vertexAttr = newRule.FindPropertyRelative("vertexAttributeOverrides");
                vertexAttr.arraySize = 0;

                SerializedProperty folders = newRule.FindPropertyRelative("sourceDirectories");
                folders.arraySize = 0;
            }
        }
        
        GUILayout.EndScrollView();

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed)
        {
            VertexAttributeModifier.instance.DoSave();
        }
    }

    public override void OnInspectorGUI()
    {
        DrawGUI();
    }
    
    private static readonly string[] Int0To4Labels = { "0", "1", "2", "3", "4" };

    private void DrawIntPopup(SerializedProperty intProp, int min, int max, params GUILayoutOption[] options)
    {
        int value = Mathf.Clamp(intProp.intValue, min, max);
        int index = value - min;

        int newIndex = EditorGUILayout.Popup(index, Int0To4Labels, options);
        intProp.intValue = min + newIndex;
    }

    private void DrawRule(SerializedProperty ruleProp, int index, SerializedProperty rulesProp)
    {
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Rule {index + 1}", EditorStyles.boldLabel);
                
                if (GUILayout.Button("Apply", GUILayout.Width(80)))
                {
                    VertexAttributeModifier.instance.ApplySingleRule(index);
                }

                if (GUILayout.Button("X", GUILayout.Width(22)))
                {
                    rulesProp.DeleteArrayElementAtIndex(index);
                    return;
                }
            }

            SerializedProperty foldersProp = ruleProp.FindPropertyRelative("sourceDirectories");
            EditorGUILayout.PropertyField(foldersProp, true);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Vertex Attributes", EditorStyles.boldLabel);
            
            SerializedProperty vertexAttributeOverridesProp = ruleProp.FindPropertyRelative("vertexAttributeOverrides");

            for (int i = 0; i < vertexAttributeOverridesProp.arraySize; i++)
            {
                SerializedProperty entryProp = vertexAttributeOverridesProp.GetArrayElementAtIndex(i);

                using (new EditorGUILayout.HorizontalScope(GUI.skin.box))
                {
                    SerializedProperty attributeProp = entryProp.FindPropertyRelative("attribute");
                    SerializedProperty modificationProp = entryProp.FindPropertyRelative("modification");
                    SerializedProperty formatProp = entryProp.FindPropertyRelative("format");
                    SerializedProperty dimensionProp = entryProp.FindPropertyRelative("dimension");
                    SerializedProperty streamProp = entryProp.FindPropertyRelative("stream");
                    
                    EditorGUILayout.PropertyField(attributeProp, GUIContent.none, GUILayout.ExpandWidth(true));
                    EditorGUILayout.PropertyField(modificationProp, GUIContent.none, GUILayout.Width(90));

                    using (new EditorGUI.DisabledScope(modificationProp.enumValueIndex == (int)VertexAttributeModification.Strip))
                    {
                        EditorGUILayout.PropertyField(formatProp, GUIContent.none, GUILayout.Width(90));

                        DrawIntPopup(dimensionProp, 0, 4, GUILayout.Width(50));
                        DrawIntPopup(streamProp, 0, 4, GUILayout.Width(50));
                    }

                    if (GUILayout.Button("X", GUILayout.Width(22)))
                    {
                        vertexAttributeOverridesProp.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Vertex Attribute"))
                {
                    ShowAddAttributeMenu(vertexAttributeOverridesProp);
                }
            }
        }
    }

    private void ShowAddAttributeMenu(SerializedProperty entriesProp)
    {
        var menu = new GenericMenu();
        
        var vertAttributeEnums = Enum.GetValues(typeof(VertexAttribute));
        foreach (VertexAttribute attribute in vertAttributeEnums)
        {
            bool alreadyPresent = HasAttribute(entriesProp, attribute);
            string label = attribute.ToString();

            if (alreadyPresent)
            {
                menu.AddDisabledItem(new GUIContent(label));
            }
            else
            {
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    serializedObject.Update();

                    entriesProp.arraySize++;
                    SerializedProperty entryProp = entriesProp.GetArrayElementAtIndex(entriesProp.arraySize - 1);
                    entryProp.FindPropertyRelative("attribute").enumValueIndex = (int)attribute;
                    entryProp.FindPropertyRelative("dimension").intValue = 4;
                    entryProp.FindPropertyRelative("stream").intValue = 0;

                    serializedObject.ApplyModifiedProperties();
                });
            }
        }

        menu.ShowAsContext();
    }

    private bool HasAttribute(SerializedProperty entriesProp, VertexAttribute attribute)
    {
        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            SerializedProperty entryProp = entriesProp.GetArrayElementAtIndex(i);
            SerializedProperty attributeProp = entryProp.FindPropertyRelative("attribute");
            if ((VertexAttribute)attributeProp.enumValueIndex == attribute)
            {
                return true;
            }
        }

        return false;
    }
}

public class VertexAttributeModifierWindow : EditorWindow
{
    private VertexAttributeModifierEditor editor;

    [MenuItem("Tools/Vertex Attribute Modifier")]
    public static void Open()
    {
        GetWindow<VertexAttributeModifierWindow>("Vertex Attribute Modifier");
    }

    private void OnEnable()
    {
        var instance = VertexAttributeModifier.instance;

        editor = (VertexAttributeModifierEditor)Editor.CreateEditor(instance);
    }

    private void OnDisable()
    {
        if (editor != null)
        {
            DestroyImmediate(editor);
        }
    }

    private void OnGUI()
    {
        if (editor == null)
        {
            OnEnable();
        }

        editor.DrawGUI();
    }
}

[Serializable]
public enum VertexAttributeModification
{
    Override,
    Strip
};

[Serializable]
public class AttributeOverride
{
    public VertexAttribute attribute = VertexAttribute.Position;
    public VertexAttributeModification modification = VertexAttributeModification.Override;
    
    public VertexAttributeFormat format = VertexAttributeFormat.Float32;
    [Range(0, 4)]
    public int dimension = 4;
    [Range(0, 4)]
    public int stream = 0;

    public void Normalize()
    {
        dimension = Mathf.Clamp(dimension, 0, 4);
        stream = Mathf.Clamp(stream, 0, 4);
    }
}

[Serializable]
public class MeshRule
{
    public List<AttributeOverride> vertexAttributeOverrides = new();
    public List<DefaultAsset> sourceDirectories = new();
        
    public bool MatchesAssetPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || sourceDirectories == null || sourceDirectories.Count == 0)
        {
            return false;
        }
    
        foreach (var folder in sourceDirectories)
        {
            if (folder == null)
            {
                continue;
            }
                
            string folderPath = AssetDatabase.GetAssetPath(folder);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                continue;
            }

            if (assetPath.Equals(folderPath, StringComparison.OrdinalIgnoreCase) ||
                assetPath.StartsWith(folderPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

public class MeshPostProcessorImporter : AssetPostprocessor
{
    private static VertexAttributeModifier cachedSettings;

    private static VertexAttributeModifier LoadSettings()
    {
        if (cachedSettings != null)
        {
            return cachedSettings;
        }

        return VertexAttributeModifier.instance;
    }
    
    void OnPostprocessModel(GameObject root)
    {
        VertexAttributeModifier vam = LoadSettings();
        if (vam == null)
        {
            return;
        }

        MeshRule matchingRule = vam.FindMatchingRule(assetImporter.assetPath);
        if (matchingRule == null)
        {
            return;
        }
        
        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter meshFilter in meshFilters)
        {
            Mesh mesh = meshFilter.sharedMesh;

            vam.ApplyModifications(mesh, matchingRule);
        }

        // modelImporter.userData;
    }
}