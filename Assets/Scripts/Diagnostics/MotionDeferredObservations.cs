using System;
using System.Collections.Generic;
using UnityEngine;

// Input may change in Update after FixedUpdate moved the actor. Resolve at presentation.
public sealed class MotionDeferredObservations
{
    public const int Capacity = 32;
    public struct Pending
    {
        public Vector2 direction;
        public double startedAt, changedAt;
        public string inputContext, cancelContext;
        public uint? commandTick;
        public int held;
    }
    readonly List<Pending> items = new();
    public int Count => items.Count;
    public bool Defer(Pending item)
    {
        if (items.Count >= Capacity) return false;
        items.Add(item); return true;
    }
    public void Reset() => items.Clear();
    public void Observe(Vector2 finalDelta, double now, Action<Pending, bool, double, Vector2> emit)
    {
        try
        {
            foreach (var item in items)
            {
                bool responded = Vector2.Dot(finalDelta, item.direction) > .001f;
                emit(item, responded, ((responded ? now : item.changedAt) - item.startedAt) * 1000, finalDelta);
            }
        }
        finally { items.Clear(); }
    }
}
