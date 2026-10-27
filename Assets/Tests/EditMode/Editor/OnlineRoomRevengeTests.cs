using Assets.Scripts.Netcode;
using Mirror;
using NUnit.Framework;
using UnityEngine;

public sealed partial class OnlineRoomEditModeTests
{
    [Test]
    public void PureClientCannotLaunchAuthoritativeRevengeBombOrResolveItsWorld()
    {
        var oldMode = NetSync.Mode;
        var actor = new GameObject("RevengeAuthority"); actor.SetActive(false);
        actor.AddComponent<BoxCollider2D>();
        try
        {
            var controller = actor.AddComponent<BombController>();
            var bombPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bombs/MadBomberBomb.prefab");
            controller.revengeBombPrefab = bombPrefab;
            int count = Object.FindObjectsByType<Bomb>(FindObjectsInactive.Include).Length;
            var random = Random.state;
            NetSync.Mode = NetSync.NetMode.Client;
            Assert.IsFalse(controller.LaunchRevengeBomb(Vector2.zero, Vector2.right, 7, 4));
            Assert.AreEqual(count, Object.FindObjectsByType<Bomb>(FindObjectsInactive.Include).Length);
            Assert.AreEqual(random, Random.state);
        }
        finally { Object.DestroyImmediate(actor); NetSync.Mode = oldMode; }
    }

    [Test]
    public void RevengeBombUsesRegisteredRootIdentityAndAuthorityTransform()
    {
        var player = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        var controller = new UnityEditor.SerializedObject(player.GetComponent<BombController>());
        var prefab = (GameObject)controller.FindProperty("revengeBombPrefab").objectReferenceValue;
        Assert.IsNotNull(prefab.GetComponent<Bomb>());
        var identity = prefab.GetComponent<NetworkIdentity>(); Assert.IsNotNull(identity);
        Assert.AreNotEqual(0, identity.assetId);
        Assert.AreEqual(1, prefab.GetComponentsInChildren<NetworkIdentity>(true).Length);
        Assert.AreEqual(SyncDirection.ServerToClient, prefab.GetComponent<NetworkTransformReliable>().syncDirection);
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Scenes/OnlineLobby.unity");
        try
        {
            BombermanNetworkManager manager = null;
            foreach (var root in scene.GetRootGameObjects())
                if ((manager = root.GetComponentInChildren<BombermanNetworkManager>(true)) != null) break;
            Assert.IsNotNull(manager); CollectionAssert.Contains(manager.spawnPrefabs, prefab);
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }
}
