using UnityEngine;
using System.Collections.Generic;
using System;
using MLAPI;
using MLAPI.NetworkedVar.Collections;
using MLAPI.NetworkedVar;
using System.Linq;
public class RopePoint : NetworkedBehaviour {
    public Vector3 bullsEyeOffset;
    public NetworkedDictionary<ulong, int> connectedPlayers = new NetworkedDictionary<ulong, int>();

    Dictionary<ulong, RenderedRope> ropes;

    public GameObject ropePiecePrefab;

    public float ropePieceLen = 0.1f;

    public override void NetworkStart() {
        connectedPlayers.Settings.WritePermission = NetworkedVarPermission.Everyone;
        connectedPlayers.Settings.ReadPermission = NetworkedVarPermission.Everyone;
        connectedPlayers.OnDictionaryChanged += OnDictChanged;
    }
    private void Awake() {
        ropes = new Dictionary<ulong, RenderedRope>();
    }
    private void Update() {
        if (connectedPlayers.Count == 0) return;
        foreach (KeyValuePair<ulong, int> kvp in connectedPlayers) {
            drawRope(kvp.Key, kvp.Value);
        }
    }
    private void drawRope(ulong playerID, int index) {
        GameObject player = MovingGeneric.GetMovingObjectAt(playerID, index).gameObject;
        if (!ropes.ContainsKey(playerID)) return;
        ropes[playerID].render(player.transform.position, transform.position);
    }
    public void AddPlayer(ulong playerID, int index) {
        GameObject player = MovingGeneric.GetMovingObjectAt(playerID, index).gameObject;
        if (!connectedPlayers.ContainsKey(playerID)) {
            connectedPlayers[playerID] = index;
            if (!ropes.ContainsKey(playerID))
                ropes[playerID] = new RenderedRope(ropePiecePrefab, ropePieceLen);
        }
    }
    public void RemovePlayer(ulong playerID, int index) {
        connectedPlayers.Remove(playerID);
        ropes[playerID].destroy();
    }
    private void OnDictChanged(NetworkedDictionaryEvent<ulong, int> ev) {
        IEnumerable<ulong> playersToGiveRopes = connectedPlayers.Keys.Except(ropes.Keys);
        IEnumerable<ulong> playersToLoseRopes = ropes.Keys.Except(connectedPlayers.Keys);
        foreach (ulong playerID in playersToGiveRopes) {
            ropes[playerID] = new RenderedRope(ropePiecePrefab, ropePieceLen);
        }
        foreach (ulong playerID in playersToLoseRopes) {
            ropes[playerID].destroy();
        }
    }
}

public class RenderedRope {
    private List<GameObject> ropePieces;
    private GameObjectPool objectPool;
    private Vector3 previousShortestPath;

    private float ropePieceLen;

    public RenderedRope(GameObject prefab, float ropePieceLen) {
        this.objectPool = Utilities.Instance.GetPool(prefab);
        this.ropePieces = new List<GameObject>();
        this.ropePieceLen = ropePieceLen;
    }
    public void render(Vector3 startPoint, Vector3 endPoint) {
        Vector3 shortestPath = endPoint - startPoint;
        if (previousShortestPath == shortestPath) return;
        previousShortestPath = shortestPath;

        int numberOfRopePieces = (int)Math.Round(shortestPath.magnitude / ropePieceLen, 0);
        objectPool.request(ropePieces, numberOfRopePieces);
        for (int i = 0; i < ropePieces.Count; i++) {
            ropePieces[i].transform.position = startPoint + shortestPath.normalized * i * ropePieceLen;
            ropePieces[i].transform.rotation = Quaternion.LookRotation(-shortestPath, Vector3.up);
        }
    }
    public void destroy() {
        objectPool.destroy(ropePieces);
    }
}