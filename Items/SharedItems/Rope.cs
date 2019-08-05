using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using MLAPI;
using MLAPI.NetworkedVar.Collections;

public class Rope : SharedItem {
    public override string name() => "Rope";
    // Use this for initialization
    private Projectile projectile;
    public GameObject prefab;

    public RopePoint connectedRopePoint;
    private bool ropeFired = false;

    private GameObject ropeObject;

    public GameObject ropeProjectilePrefab;
    public GameObject ropePiecePrefab;
    public float ropePieceLen = 0.1f;

    public float hitDistance;
    public float ropeLenMult = 15.0f;
    public float ropeSpeedMult = 75f;

    public float ropeProjectileRadius = 0.5f;
    public RenderedRope visualRope;

    public override void Start() {
        base.Start();
        ropePiecePrefab = (GameObject)Resources.Load("RadioactiveKey");
        ropeProjectilePrefab = (GameObject)Resources.Load("RadioactiveLock");
        visualRope = new RenderedRope(ropePiecePrefab, ropePieceLen);
    }

    private RopePoint GetTargetRopePoint() {
        RopePoint[] targetedRps = GameObject.FindObjectsOfType<RopePoint>();
        Func<RopePoint, Vector3> GetDist = (RopePoint rp) => rp.transform.position - player.transform.position;
        bool player_controlled = true;
        Vector3 intended_dir = player.GetMoveVector();
        if (intended_dir.magnitude < 0.3f) {
            intended_dir = player.transform.forward;
            player_controlled = false;
        }
        Func<RopePoint, float> GetAng = (RopePoint rp) => Vector3.Dot(GetDist(rp).normalized, intended_dir);
        Func<RopePoint, float> GetFutureMag = (RopePoint rp) => {
            float ropeSpeed = ropeSpeedMult * player.cc.radius;
            float ropeRelSpeed = ((ropeSpeed * GetDist(rp).normalized) - player.GetVelocity()).magnitude;
            // relative speed is an approximation assuming the player moves at constant velocity.
            // for extreme cases where we think the rope will never be caught this approximation is innacurate.
            // limit the speed to at minimum 1/5 of the intended speed
            ropeRelSpeed = Mathf.Max(ropeRelSpeed, ropeSpeed / 5f);
            float ropeTravelDist = ropeLenMult * player.cc.radius;
            return (rp.transform.position - (player.transform.position + (player.GetVelocity() * ropeTravelDist / ropeRelSpeed))).magnitude;
        };

        RopePoint[] sortedRps = targetedRps.Where((RopePoint rp) => Mathf.Min(GetDist(rp).magnitude, GetFutureMag(rp)) < (2f * player.cc.radius * ropeLenMult))
                                           .OrderByDescending(GetAng).ToArray();
        // If we are in the air, prefer rope points that are above us if there are any. Disable this if the player wants to go somewhere else
        if (!player.OnGround() && !player_controlled) {
            RopePoint idealRp = sortedRps.Where((RopePoint rp) => Vector3.Dot(GetDist(rp), player.transform.up) > 0).FirstOrDefault();
            if (idealRp) return idealRp;
        }
        // If not just return the one closest to our angle
        return sortedRps.FirstOrDefault();
    }

    public override void Update() {
        if (SharedItemButtonPress() && !ropeFired && ropeObject == null) {
            RopePoint targetedRp = GetTargetRopePoint();
            if (!targetedRp) return;

            ropeObject = utils.FireProjectile(
                shooter: player.gameObject,
                cb: hit_cb,
                projectileRadius: ropeProjectileRadius,
                // startingVelocity: player.player_camera.transform.forward * player.cc.radius * 75,
                startingVelocity: (targetedRp.transform.position - player.transform.position).normalized * player.cc.radius * ropeSpeedMult,
                lifetime: 100f,
                maxDistance: player.cc.radius * ropeLenMult,
                prefab: ropeProjectilePrefab);
            ropeFired = true;
            player.ShortHopTempDisable = true;
            utils.WaitUntilCondition(() => !SharedItemButtonHold(), () => { ropeFired = false; });
        }
        // if (!ropeObject) ropeFired = false; <-- use for press instead of hold

        if (ropeFired && !connectedRopePoint && ropeObject) {
            visualRope.render(player.transform.position, ropeObject.transform.position);
        }
        else {
            if (ropeObject) GameObject.Destroy(ropeObject);
            visualRope.destroy();
        }
    }
    public override void FixedUpdate() {
        if (connectedRopePoint == null) return;
        if (!ropeFired) {
            connectedRopePoint.RemovePlayer(player.NetworkId, utils.get<MovingPlayer>().GetMovingObjectIndex());
            Vector3 forceDir = (connectedRopePoint.transform.position - player.transform.position).normalized + player.GetVelocity().normalized;
            player.SetVelocity(player.GetVelocity() + (forceDir * player.GetVelocity().magnitude * 1f * player.cc.radius));
            player.AllowLedgeGrab();
            connectedRopePoint = null;
            return;
        }
        player.PreventLedgeGrab();
        Vector3 distanceVec = connectedRopePoint.transform.position - player.transform.position;
        // float max_length = (player.cc.radius * ropeLenMult) + ropeProjectileRadius;
        if (distanceVec.magnitude > hitDistance) {
            if (Vector3.Dot(distanceVec, -Physics.gravity.normalized) > hitDistance) {
                if (player.OnGround()) player.DoJump();
                player.SetLostTraction();
            }
            Vector3 relativeVel = player.GetVelocity();
            MovingGeneric movingTarget = connectedRopePoint.GetComponent<MovingGeneric>();
            if (movingTarget) relativeVel = relativeVel - movingTarget.velocity;
            Vector3 distance = distanceVec - (hitDistance * distanceVec.normalized);
            // Debug.DrawRay(player.transform.position, -distance, Color.blue, 100f);
            // Debug.DrawRay(player.transform.position, distanceVec, Color.green, 100f);
            // Vector3 project = Vector3.Project(relativeVel, distanceVec);
            // Vector3 force = distance * 2.0f - project * 1.0f;

            Vector3 newRelativeVel = relativeVel;
            if (Vector3.Dot(distanceVec, newRelativeVel) < 0) {
                newRelativeVel = Vector3.ProjectOnPlane(newRelativeVel, distanceVec);
            }
            newRelativeVel += distance * 2.0f;
            player.SetVelocity(player.GetVelocity() + (newRelativeVel - relativeVel));
        }

        player.Accelerate(-player.GetVelocity() * player.cc.radius * 0.5f);
    }
    private bool hit_cb(GameObject hit_entity) {
        RopePoint ropePoint = hit_entity.GetComponentInChildren<RopePoint>();
        if (ropePoint) {
            connectedRopePoint = ropePoint;
            ropePoint.AddPlayer(player.NetworkId, utils.get<MovingPlayer>().GetMovingObjectIndex());
            hitDistance = (player.transform.position - ropePoint.transform.position).magnitude;
        }
        return true;
    }
}
