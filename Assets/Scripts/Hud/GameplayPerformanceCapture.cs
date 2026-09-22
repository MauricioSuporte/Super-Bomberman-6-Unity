using System;
using System.Diagnostics;
using UnityEngine;

public enum GameplayPerformancePhase
{
    ComUpdate, ComThink, References, Abilities, Danger, Pathfinding, Candidates,
    MovementSafety, DecisionDiagnostics, SceneQueries, Capture, Report, Count
}

[Flags]
public enum GameplayPerformanceInterruption
{
    None = 0, FocusLost = 1, ApplicationPause = 2, EditorPause = 4,
    SceneChange = 8, GamePaused = 16, OutsideGameplay = 32
}

// Main-thread only. Scopes are structs and all frame storage is allocated at capture start.
public static class GameplayPerformanceCapture
{
    public const int Capacity = 2048;
    public const int MetricCapacity = 32;
    static Frame[] frames;
    public static bool Enabled { get; private set; }

    public struct PhaseSample
    {
        public int Calls;
        public long TotalTicks;
        public long MaximumTicks;
        internal int Depth;
        internal long OuterStart;
        public double Milliseconds => TotalTicks * 1000d / Stopwatch.Frequency;
        public double MaximumMilliseconds => MaximumTicks * 1000d / Stopwatch.Frequency;
    }

    public struct ExpensiveCall
    {
        public string Operation;
        public int PlayerId;
        public double Milliseconds;
        public long AllocatedBytes;
    }

    public sealed class Frame
    {
        public int Number = -1;
        public string Scene;
        public float Milliseconds;
        public double WallGapMilliseconds;
        public float TimeScale;
        public int Players;
        public int ComPlayers;
        public int Bombs;
        public int Mounted;
        public int Eggs;
        public bool HasState;
        public GameplayPerformanceInterruption Interruption;
        public double ResumedAfterMilliseconds;
        public GameplayPerformanceInterruption ResumedReason;
        public bool IsGameplaySample => Interruption == GameplayPerformanceInterruption.None;
        public readonly PhaseSample[] Phases = new PhaseSample[(int)GameplayPerformancePhase.Count];
        public readonly double[] Metrics = new double[MetricCapacity];
        public readonly ExpensiveCall[] ExpensiveCalls = new ExpensiveCall[3];

        internal void Reset(int number)
        {
            Number = number;
            Scene = null;
            Milliseconds = 0;
            WallGapMilliseconds = 0;
            TimeScale = 0;
            Players = ComPlayers = Bombs = Mounted = Eggs = 0;
            HasState = false;
            Interruption = GameplayPerformanceInterruption.None;
            ResumedAfterMilliseconds = 0;
            ResumedReason = GameplayPerformanceInterruption.None;
            Array.Clear(Phases, 0, Phases.Length);
            Array.Clear(ExpensiveCalls, 0, ExpensiveCalls.Length);
            for (int i = 0; i < Metrics.Length; i++)
                Metrics[i] = double.NaN;
        }

        internal void RecordExpensiveCall(string operation, int playerId, long ticks, long allocatedBytes)
        {
            double milliseconds = ticks * 1000d / Stopwatch.Frequency;
            if (milliseconds < 0.5d)
                return;
            for (int rank = 0; rank < ExpensiveCalls.Length; rank++)
            {
                if (milliseconds <= ExpensiveCalls[rank].Milliseconds)
                    continue;
                for (int i = ExpensiveCalls.Length - 1; i > rank; i--)
                    ExpensiveCalls[i] = ExpensiveCalls[i - 1];
                ExpensiveCalls[rank] = new ExpensiveCall
                {
                    Operation = operation, PlayerId = playerId,
                    Milliseconds = milliseconds, AllocatedBytes = allocatedBytes
                };
                break;
            }
        }
    }

    public static void Start()
    {
        if (frames == null)
        {
            frames = new Frame[Capacity];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = new Frame();
        }
        for (int i = 0; i < frames.Length; i++)
            frames[i].Reset(-1);
        Enabled = true;
    }

    public static void Stop() => Enabled = false;

    public static Frame GetFrame(int number)
    {
        Frame frame = frames[number % Capacity];
        if (frame.Number != number)
            frame.Reset(number);
        return frame;
    }

    public static Scope Measure(GameplayPerformancePhase phase, int playerId = 0,
        [System.Runtime.CompilerServices.CallerMemberName] string operation = null)
    {
        return Enabled ? new Scope(GetFrame(Time.frameCount), phase, playerId, operation) : default;
    }

    public readonly struct Scope : IDisposable
    {
        readonly Frame frame;
        readonly int phase;
        readonly long started;
        readonly int playerId;
        readonly string operation;
        readonly long allocationStart;

        internal Scope(Frame frame, GameplayPerformancePhase phase, int playerId, string operation)
        {
            this.frame = frame;
            this.phase = (int)phase;
            this.playerId = playerId;
            // Keep aggregate wrappers out of the top calls so the concrete candidate
            // or search responsible for the work remains visible. No per-call strings.
            this.operation = playerId > 0 && operation != "BuildCandidates" &&
                (phase == GameplayPerformancePhase.Candidates || phase == GameplayPerformancePhase.Pathfinding ||
                 phase == GameplayPerformancePhase.DecisionDiagnostics || phase == GameplayPerformancePhase.SceneQueries)
                ? operation : null;
            allocationStart = this.operation != null ? GC.GetAllocatedBytesForCurrentThread() : 0;
            started = Stopwatch.GetTimestamp();
            ref PhaseSample sample = ref frame.Phases[this.phase];
            if (sample.Depth++ == 0)
                sample.OuterStart = started;
            sample.Calls++;
        }

        public void Dispose()
        {
            if (frame == null)
                return;
            long ended = Stopwatch.GetTimestamp();
            ref PhaseSample sample = ref frame.Phases[phase];
            sample.MaximumTicks = Math.Max(sample.MaximumTicks, ended - started);
            // Nested calls of the same category count as calls, but only their outer
            // scope contributes to total time. Different categories remain inclusive.
            if (--sample.Depth == 0)
                sample.TotalTicks += ended - sample.OuterStart;
            if (operation != null)
                frame.RecordExpensiveCall(operation, playerId, ended - started,
                    GC.GetAllocatedBytesForCurrentThread() - allocationStart);
        }
    }
}

// Exact nearest-rank percentiles for the current bounded report window.
public sealed class GameplayFrameStatistics
{
    public const int Capacity = 1024;
    readonly float[] durations = new float[Capacity];
    bool sorted;
    public int Count { get; private set; }
    public double TotalMilliseconds { get; private set; }
    public float MaximumMilliseconds { get; private set; }
    public int OverBudget { get; private set; }
    public int Slow20 { get; private set; }
    public int Slow33 { get; private set; }
    public double AverageMilliseconds => Count > 0 ? TotalMilliseconds / Count : 0;

    public bool Add(float milliseconds)
    {
        if (Count == Capacity || float.IsNaN(milliseconds) || float.IsInfinity(milliseconds) || milliseconds < 0)
            return false;
        durations[Count++] = milliseconds;
        sorted = false;
        TotalMilliseconds += milliseconds;
        MaximumMilliseconds = Math.Max(MaximumMilliseconds, milliseconds);
        if (milliseconds > 1000f / 60f) OverBudget++;
        if (milliseconds >= 20f) Slow20++;
        if (milliseconds >= 1000f / 30f) Slow33++;
        return true;
    }

    public float Percentile(float percentile)
    {
        if (Count == 0)
            return 0;
        if (!sorted)
        {
            Array.Sort(durations, 0, Count);
            sorted = true;
        }
        int index = Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(percentile) * Count) - 1, 0, Count - 1);
        return durations[index];
    }

    public void Reset()
    {
        Count = OverBudget = Slow20 = Slow33 = 0;
        TotalMilliseconds = MaximumMilliseconds = 0;
        sorted = false;
    }
}
