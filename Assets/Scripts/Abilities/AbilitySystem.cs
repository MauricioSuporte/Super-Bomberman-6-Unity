using System;
using System.Collections.Generic;
using UnityEngine;

public class AbilitySystem : MonoBehaviour
{
    private readonly Dictionary<string, IPlayerAbility> cache = new();

    private int version;
    public int Version => version;

    private void Awake()
    {
        RebuildCache();
    }

    public void RebuildCache()
    {
        cache.Clear();

        var monos = GetComponents<MonoBehaviour>();
        for (int i = 0; i < monos.Length; i++)
        {
            if (monos[i] is IPlayerAbility ability && !string.IsNullOrEmpty(ability.Id))
                cache[ability.Id] = ability;
        }

        version++;
    }

    public IPlayerAbility Get(string id)
    {
        cache.TryGetValue(id, out var a);
        return a;
    }

    public T Get<T>(string id) where T : class, IPlayerAbility
    {
        return Get(id) as T;
    }

    public bool IsEnabled(string id)
    {
        var a = Get(id);
        return a != null && a.IsEnabled;
    }

    public void Enable(string id)
    {
        if (!AbilityRegistry.TryGetType(id, out var type))
            return;

        var ability = EnsureAbilityComponent(id, type);
        if (ability == null)
            return;

        bool wasEnabled = ability.IsEnabled;
        ability.Enable();

        if (!wasEnabled || version == 0)
            version++;
    }

    public void Disable(string id)
    {
        if (!cache.TryGetValue(id, out var ability) || ability == null)
        {
            if (!AbilityRegistry.TryGetType(id, out var type))
                return;

            var existing = GetComponent(type) as MonoBehaviour;
            if (existing != null && existing is IPlayerAbility a2)
            {
                cache[id] = a2;
                ability = a2;
            }
        }

        if (ability == null)
            return;

        bool wasEnabled = ability.IsEnabled;
        ability.Disable();

        if (wasEnabled)
            version++;
    }

    public void DisableAll()
    {
        RebuildCache();

        bool changed = false;

        foreach (var kv in cache)
        {
            var a = kv.Value;
            if (a != null && a.IsEnabled)
            {
                a.Disable();
                changed = true;
            }
        }

        if (changed)
            version++;
    }

    private IPlayerAbility EnsureAbilityComponent(string id, Type type)
    {
        if (cache.TryGetValue(id, out var cached) && cached != null)
            return cached;

        var existing = GetComponent(type) as MonoBehaviour;
        if (existing == null)
            existing = gameObject.AddComponent(type) as MonoBehaviour;

        if (existing is IPlayerAbility ability)
        {
            cache[id] = ability;
            version++;
            return ability;
        }

        return null;
    }
}
