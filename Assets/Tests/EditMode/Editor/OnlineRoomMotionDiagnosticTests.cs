using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed partial class OnlineRoomEditModeTests
{
    [Test]
    public void MotionReleaseWaitsForPresentedFixedMovementBeforeDeclaringCancellation()
    {
        var tracker = new MotionDeferredObservations();
        Assert.IsTrue(tracker.Defer(new MotionDeferredObservations.Pending {
            direction = Vector2.left, startedAt = 10, changedAt = 10.2, held = 4, commandTick = 7 }));
        Assert.AreEqual(1, tracker.Count);
        var observed = new List<(bool response, double duration, int held)>();
        tracker.Observe(Vector2.left * .5f, 10.29, (item, response, duration, _) => observed.Add((response, duration, item.held)));
        Assert.AreEqual(1, observed.Count);
        Assert.IsTrue(observed[0].response, "Fixed movement in this frame remains a response even after Update released the input.");
        Assert.AreEqual(290, observed[0].duration, .001);
        Assert.AreEqual(4, observed[0].held);
        Assert.AreEqual(0, tracker.Count);
    }

    [Test]
    public void MotionCancellationWithoutDisplacementUsesInputChangeTime()
    {
        var tracker = new MotionDeferredObservations();
        tracker.Defer(new MotionDeferredObservations.Pending { direction = Vector2.right, startedAt = 10, changedAt = 10.2 });
        bool called = false;
        tracker.Observe(Vector2.zero, 10.29, (_, response, duration, _) => {
            called = true; Assert.IsFalse(response); Assert.AreEqual(200, duration, .001);
        });
        Assert.IsTrue(called);
    }

    [Test]
    public void MotionDeferredObservationsAreBoundedAndReusable()
    {
        var tracker = new MotionDeferredObservations();
        for (int i = 0; i < MotionDeferredObservations.Capacity; i++) Assert.IsTrue(tracker.Defer(default));
        Assert.IsFalse(tracker.Defer(default));
        tracker.Observe(Vector2.zero, 1, (_, _, _, _) => { });
        Assert.AreEqual(0, tracker.Count);
        Assert.IsTrue(tracker.Defer(default)); tracker.Reset(); Assert.AreEqual(0, tracker.Count);
    }
}
