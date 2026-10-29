using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Assets.Scripts.Netcode;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed partial class OnlineRoomEditModeTests
{
    const BindingFlags NativeAuditFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    static IEnumerable NativeClientAuthorityEntryPoints()
    {
        var roots = new[] { "Assets/Scripts/Enemies", "Assets/Scripts/Bosses", "Assets/Scripts/Bombers" };
        var pattern = new Regex(@"void\s+(\w+)\s*\([^\r\n]*\)\s*\r?\n\s*\{\s*if\s*\(\s*!Assets\.Scripts\.Netcode\.NetSync\.ShouldSimulateLocally\s*\)\s*return;");
        foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", roots).OrderBy(value => value))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var type = AssetDatabase.LoadAssetAtPath<MonoScript>(path)?.GetClass();
            if (type == null || type.IsAbstract || !typeof(MonoBehaviour).IsAssignableFrom(type)) continue;
            var names = new HashSet<string>(pattern.Matches(File.ReadAllText(path, new UTF8Encoding(false, true))).Select(match => match.Groups[1].Value));
            foreach (var method in type.GetMethods(NativeAuditFlags))
                if (!method.IsStatic && !method.IsAbstract && method.ReturnType == typeof(void) && names.Contains(method.Name))
                    yield return new TestCaseData(path, method.Name, method.GetParameters().Length)
                        .SetName("ClientNativeAuthority_" + type.Name + "_" + method.Name + "_" + method.GetParameters().Length);
        }
    }

    [TestCaseSource(nameof(NativeClientAuthorityEntryPoints))]
    public void NativeSimulationEntryPointDoesNotMutateClientStateOrDrawRandom(string path, string methodName, int parameterCount)
    {
        var previousMode = NetSync.Mode;
        var previousRandom = UnityEngine.Random.state;
        var actor = new GameObject("ClientAuthorityAudit"); actor.SetActive(false);
        actor.AddComponent<BoxCollider2D>();
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            var type = AssetDatabase.LoadAssetAtPath<MonoScript>(path).GetClass();
            var component = actor.AddComponent(type);
            var method = type.GetMethods(NativeAuditFlags).Single(value => value.Name == methodName && value.GetParameters().Length == parameterCount);
            var fields = type.GetFields(NativeAuditFlags).Where(field => !field.IsStatic &&
                (field.FieldType.IsPrimitive || field.FieldType.IsEnum || field.FieldType == typeof(Vector2) ||
                 field.FieldType == typeof(Vector3) || field.FieldType == typeof(Quaternion))).ToArray();
            var before = fields.Select(field => field.GetValue(component)).ToArray();
            var random = UnityEngine.Random.state; var position = actor.transform.position;
            var arguments = method.GetParameters().Select(parameter => parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null).ToArray();
            method.Invoke(component, arguments);
            for (int i = 0; i < fields.Length; i++) Assert.AreEqual(before[i], fields[i].GetValue(component), fields[i].Name);
            Assert.AreEqual(random, UnityEngine.Random.state, "Clients must not advance gameplay RNG.");
            Assert.AreEqual(position, actor.transform.position);
            Assert.IsFalse(actor.activeSelf);
        }
        finally
        {
            Object.DestroyImmediate(actor); NetSync.Mode = previousMode; UnityEngine.Random.state = previousRandom;
        }
    }
}
