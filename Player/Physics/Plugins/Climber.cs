using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Climber : PhysicsPlugin {
    private string CLIMB_TIMER = "ClimberTimer";
    private Vector3 PreviousWallPos;
    private Vector3 PreviousWallNormal;
    private Climbable PreviousWall;
    public float ClimbSpeedDampMult = 40f;
    public float ClimbSpeedMult = 60f;

    public Climber(PhysicsPropHandler context) : base(context) { }

    protected override void Setup() {
        if (!IsOwner) return;
        utils.CreateTimer(CLIMB_TIMER, 0.1f);
    }

    public override void FixedUpdate() {
        if (!IsOwner) return;
        if (utils.CheckTimer(CLIMB_TIMER)) PreviousWall = null;
        HandleClimbableSurfaces();
    }

    private void HandleClimbableSurfaces() {
        // Make sure we are running into a wall
        if (!PreviousWall) return;
        if (player.OnGround() && Vector3.Dot(player.GetMoveVector(), PreviousWallNormal) < 0) player.Accelerate(-Physics.gravity * 10f);
        if (!player.IsOnWall()) return;

        if (!player.OnGround()) player.SetDisableMovement();
        player.SetGravity(0f);
        player.Accelerate(-PreviousWallNormal * player.cc.radius * 20f);
        if (Vector3.Dot(player.GetVelocity(), PreviousWallNormal) > 0) {
            player.SetVelocity(Vector3.ProjectOnPlane(player.GetVelocity(), PreviousWallNormal));
        }
        player.Accelerate(-Vector3.ProjectOnPlane(player.GetVelocity(), PreviousWallNormal) * ClimbSpeedDampMult * player.cc.radius);
        Vector3 playerMove = player.GetMoveVector();
        float wallAxisMove = Vector3.Dot(playerMove, -PreviousWallNormal);

        //Debug.DrawRay(player.transform.position, player.GetMoveVector(), Color.green);
        //Debug.DrawRay(player.transform.position, player.transform.up * wallAxisMove, Color.red);
        Vector3 climbMove = (player.transform.up * input_manager.GetMoveVertical()) + (player.transform.right * input_manager.GetMoveHorizontal());
        player.Accelerate(Vector3.ClampMagnitude(climbMove, 1f) * ClimbSpeedMult * player.cc.radius);
        player.player_camera.RotatePlayerToward(Vector3.ProjectOnPlane(-PreviousWallNormal, player.transform.up), 0.5f);
        player.player_camera.RotateCameraToward((player.transform.up * wallAxisMove) * (wallAxisMove > 0 ? 2f : 1f) + player.transform.forward, 0.003f);
        player.PreventLedgeGrab();
        //Debug.DrawRay(PreviousWallPos, PreviousWallNormal, Color.blue, 10f);
    }

    public bool IsClimbing() {
        return !utils.CheckTimer(CLIMB_TIMER);
    }

    public override void OnWallHit(Vector3 normal, Vector3 point, GameObject go, PhysicsProp prop) {
        if (!IsOwner) return;
        PreviousWallNormal = normal;
        PreviousWallPos = point;
        PreviousWall = prop as Climbable;
        utils.ResetTimer(CLIMB_TIMER);
    }
}