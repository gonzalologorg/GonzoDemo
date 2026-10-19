using UnityEngine;
using Unity.Netcode;

public class NetworkPermissionTest : NetworkBehaviour
{
    private NetworkList<int> testList = new(
        null,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        Debug.Log($"[TEST Awake] {testList.WritePerm}");
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log(
            $"[TEST Spawn] IsServer={IsServer}, " +
            $"IsOwner={IsOwner}, " +
            $"WritePerm={testList.WritePerm}"
        );
    }
}