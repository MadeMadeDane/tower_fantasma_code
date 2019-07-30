using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Swimmer : PhysicsPlugin {
    private Climbable PreviousWall;
    public float SwimSpeedDampMult = 40f;
    public float SwimSpeedMult = 60f;
    public Water currentWater;

    public Swimmer(PhysicsPropHandler context) : base(context) { }

    protected override void Setup() {
        return;
    }

    public override void FixedUpdate() {
        if (!IsOwner) return;
        if (!player.IsSwimming()) currentWater = null;
        HandleSwimming();
    }

    private void HandleSwimming() {
        if (!currentWater) return;
        player.SetGravity(0f);
        return;
    }

    public override void OnTriggerStay(Collider other, PhysicsProp prop) {
        if (!IsOwner) return;
        currentWater = prop as Water;
        player.SetSwimming();
    }
}