#if UNITY_EDITOR || DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    // Opt-in local Development fixture. Messages cannot enable it on a normal server.
    struct BombAbilitySmokeStep : NetworkMessage { public int serial, held; public string label; }
    struct BombAbilitySmokeAck : NetworkMessage { public int serial; public uint tick; }

    public partial class BombermanNetworkManager
    {
        bool bombAbilitySmokeRegistered, bombAbilitySmokeStarted;
        int bombAbilitySmokeSerial;
        readonly Dictionary<int, BombAbilitySmokeAck> bombAbilitySmokeAcks = new();

        void TickBombAbilitySmoke()
        {
            if (!HasArg("-load-test-bomb-abilities")) return;
            if (!bombAbilitySmokeRegistered)
            {
                bombAbilitySmokeRegistered = true;
                NetworkServer.RegisterHandler<BombAbilitySmokeAck>((peer, ack) =>
                {
                    if (peer.isReady && peer.identity != null) bombAbilitySmokeAcks[peer.connectionId] = ack;
                });
            }
            if (bombAbilitySmokeStarted || InLobbyScene() || BattleStartPending || Time.timeSinceLevelLoad < 12 ||
                NetworkServer.connections.Count < 2 || bombAbilitySmokeAcks.Count < 2) return;
            bombAbilitySmokeStarted = true;
            StartCoroutine(CheckBombAbilities());
        }

        IEnumerator BombAbilityInput(NetworkConnectionToClient peer, int held, string label)
        {
            int serial = ++bombAbilitySmokeSerial;
            peer.Send(new BombAbilitySmokeStep { serial = serial, held = held, label = label });
            float until = Time.realtimeSinceStartup + 8;
            while (Time.realtimeSinceStartup < until)
            {
                if (bombAbilitySmokeAcks.TryGetValue(peer.connectionId, out var ack) && ack.serial == serial &&
                    !NetworkInputHistory.IsNewer(ack.tick, peer.identity.GetComponent<NetworkPlayerInput>().CurrentInputTick))
                    yield break;
                yield return null;
            }
            throw new InvalidOperationException("A14 hardware input acknowledgement timeout: " + label);
        }

        IEnumerator CheckBombAbilities()
        {
            var peers = new List<NetworkConnectionToClient>(NetworkServer.connections.Values);
            int actors = 0;
            foreach (var peer in peers)
            {
                if (peer.identity == null) throw new InvalidOperationException("A14 missing actor");
                var actor = peer.identity;
                var mover = actor.GetComponent<MovementController>();
                var controller = actor.GetComponent<BombController>();
                var fx = actor.GetComponent<NetworkBombFx>();
                var companion = actor.GetComponent<PlayerMountCompanion>();
                var abilities = actor.GetComponent<AbilitySystem>();
                Vector2 origin = actor.transform.position;
                Vector2 direction = origin.x <= 0 ? Vector2.right : Vector2.left;
                int move = direction.x > 0 ? 8 : 4;
                actor.GetComponent<CharacterHealth>().SetExternalInvulnerability(true);
                // Explicitly remove destructibles in the authored spawn corridor and publish
                // that same geometry. Do not disable client collision to make predictions pass.
                var map = GameManager.Instance.destructibleTilemap;
                var cells = new List<Vector3Int>();
                for (int i = 0; i < 6; i++)
                {
                    var cell = map.WorldToCell(origin + direction * i);
                    if (map.HasTile(cell)) { map.SetTile(cell, null); cells.Add(cell); }
                }
                if (cells.Count > 0) fx.ServerEmitDestroyed(cells.ToArray());
                foreach (string family in new[] { "Kick", "Punch", "Glove", "Blue", "Yellow", "Rubber" })
                {
                    yield return BombAbilityInput(peer, 0, family + " neutral");
                    companion.ClearMountedStateForForcedArenaRemoval(); abilities.DisableAll();
                    // NetworkPlayerState publishes runtime stats, not the temporary
                    // component toggles. Seed the same approved loadout on that source
                    // so this fixture actually exercises prediction on the owner too.
                    var runtime = PlayerPersistentStats.GetRuntime(mover.PlayerId);
                    runtime.CanKickBombs = family == "Kick";
                    runtime.CanPunchBombs = family == "Punch" || family == "Rubber";
                    runtime.HasPowerGlove = family == "Glove";
                    runtime.HasRubberBombs = family == "Rubber";
                    // Release native AFK and establish facing through the owner's real
                    // hardware path before the controlled starting-position snapshot.
                    yield return BombAbilityInput(peer, move, family + " face");
                    yield return new WaitForSecondsRealtime(.15f);
                    yield return BombAbilityInput(peer, 0, family + " face neutral");
                    mover.SnapToWorldPoint(origin, roundToGrid: false); mover.ForceFacingDirection(direction);
                    ServerReconcileFixturePlayer(actor.GetComponent<NetworkPlayerInput>());
                    if (family == "Kick") abilities.Enable(BombKickAbility.AbilityId);
                    if (family == "Punch" || family == "Rubber") abilities.Enable(BombPunchAbility.AbilityId);
                    if (family == "Glove") abilities.Enable(PowerGloveAbility.AbilityId);
                    if (family == "Blue") companion.RestoreMounted(MountedType.Blue);
                    if (family == "Yellow") companion.RestoreMounted(MountedType.Yellow);
                    yield return new WaitForSecondsRealtime(.8f); // approved loadout/position reaches both clients
                    if (family == "Blue" || family == "Yellow")
                    {
                        var expected = family == "Blue" ? MountedType.Blue : MountedType.Yellow;
                        if (companion.GetMountedLouieType() != expected) throw new InvalidOperationException("A14 fixture mount missing: " + family);
                        // The native first mounted tick can rebind facing from the stored
                        // mounted state. Establish direction AFTER that tick, using real input.
                        yield return BombAbilityInput(peer, move, family + " mounted face");
                        yield return new WaitForSecondsRealtime(.15f);
                        yield return BombAbilityInput(peer, 0, family + " mounted face neutral");
                        mover.SnapToWorldPoint(origin, roundToGrid: false); mover.ForceFacingDirection(direction);
                        ServerReconcileFixturePlayer(actor.GetComponent<NetworkPlayerInput>());
                        yield return new WaitForSecondsRealtime(.4f);
                    }
                    Debug.Log("[LoadTest] a14 prepared family=" + family + " player=" + mover.PlayerId +
                        " facing=" + mover.FacingDirection + " locked=" + mover.InputLocked + " mounted=" + companion.GetMountedLouieType() +
                        " punch=" + actor.GetComponent<BombPunchAbility>().IsEnabled);
                    Bomb bomb;
                    if (family == "Glove")
                    {
                        controller.bombFuseTime = 20;
                        yield return BombAbilityInput(peer, 16, "Glove plant-and-hold");
                        yield return new WaitForSecondsRealtime(.4f);
                        var placed = controller.GetLastPlacedBomb();
                        if (placed == null) throw new InvalidOperationException("A14 failed to plant through ActionA");
                        bomb = placed.GetComponent<Bomb>();
                        if (actor.GetComponent<PowerGloveAbility>().IsHoldingBomb)
                            throw new InvalidOperationException("A14 one A unexpectedly lifted the planted bomb");
                        yield return BombAbilityInput(peer, 0, "Glove release first A");
                        yield return new WaitForSecondsRealtime(.15f);
                        yield return BombAbilityInput(peer, 16, "Glove second A pickup");
                        yield return new WaitForSecondsRealtime(.4f);
                        if (!actor.GetComponent<PowerGloveAbility>().IsHoldingBomb)
                            throw new InvalidOperationException("A14 explicit second A did not lift");
                        yield return BombAbilityInput(peer, 0, "Glove release throw");
                    }
                    else
                    {
                        var instance = Instantiate(family == "Rubber" ? controller.rubberBombPrefab : controller.bombPrefab,
                            origin + direction, Quaternion.identity);
                        bomb = instance.GetComponent<Bomb>(); bomb.Initialize(null); bomb.SetFuseSeconds(20);
                        if (family == "Rubber") bomb.IsRubberBomb = true;
                        NetSpawn.Server(instance); Physics2D.SyncTransforms();
                        yield return new WaitForSecondsRealtime(.6f);
                        yield return BombAbilityInput(peer, family == "Kick" ? move : 64, family + " start");
                    }
                    float until = Time.realtimeSinceStartup + 3;
                    bool moving = false;
                    while (Time.realtimeSinceStartup < until)
                    {
                        if (bomb != null && (bomb.IsBeingKicked || bomb.IsBeingPunched)) { moving = true; break; }
                        yield return null;
                    }
                    if (!moving) throw new InvalidOperationException("A14 native action never started: " + family +
                        " facing=" + mover.FacingDirection + " position=" + mover.transform.position + " locked=" + mover.InputLocked +
                        " mounted=" + companion.GetMountedLouieType() + " punch=" + actor.GetComponent<BombPunchAbility>().IsEnabled +
                        " bomb=" + (bomb != null ? bomb.transform.position.ToString() : "removed"));
                    Debug.Log("[LoadTest] a14 native started family=" + family + " player=" + mover.PlayerId + " bomb=" + bomb.GetComponent<NetworkIdentity>().netId);
                    yield return BombAbilityInput(peer, 0, family + " neutral after start");
                    ToggleNativePause(true); yield return new WaitForSecondsRealtime(.4f); ToggleNativePause(false);
                    yield return new WaitForSecondsRealtime(2.2f);
                    if (family == "Rubber" && bomb != null && bomb.RubberBounceIndex < 0)
                        throw new InvalidOperationException("A14 Rubber never reached native bounce");
                    if (bomb != null) NetworkServer.Destroy(bomb.gameObject);
                    yield return new WaitForSecondsRealtime(.35f);
                    Debug.Log("[LoadTest] a14 completed family=" + family + " player=" + mover.PlayerId);
                }
                yield return BombAbilityInput(peer, 0, "Bounce native recovery");
                companion.ClearMountedStateForForcedArenaRemoval();
                mover.SnapToWorldPoint(origin, roundToGrid: false);
                ServerReconcileFixturePlayer(actor.GetComponent<NetworkPlayerInput>());
                var blocker = new GameObject("A14 approved recovery blocker", typeof(BoxCollider2D));
                blocker.layer = LayerMask.NameToLayer("Stage"); blocker.transform.position = origin;
                blocker.GetComponent<BoxCollider2D>().size = Vector2.one * .8f;
                Physics2D.SyncTransforms();
                actor.GetComponent<PlayerPushedOutOfInvalidTile>().NotifyExternalPushed(direction);
                yield return new WaitForSecondsRealtime(.03f);
                ToggleNativePause(true); yield return new WaitForSecondsRealtime(.4f); ToggleNativePause(false);
                yield return new WaitForSecondsRealtime(1);
                Destroy(blocker);
                if (Vector2.Distance(origin, mover.transform.position) < .9f || mover.InputLocked)
                    throw new InvalidOperationException("A14 native bounce did not recover the actor");
                Debug.Log("[LoadTest] a14 completed family=Bounce player=" + mover.PlayerId);
                yield return BombAbilityInput(peer, 0, "complete");
                actors++;
            }
            if (actors != 2) throw new InvalidOperationException("A14 requires two real owners");
            Debug.Log("[LoadTest] a14 completed actors=2");
        }
    }
}
#endif
