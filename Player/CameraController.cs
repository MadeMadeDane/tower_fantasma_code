using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;



public enum ViewMode {
    Shooter,
    Third_Person,
    Third_Person_Shooter
}

public delegate void CameraMovementFunction();

public class CameraController : NetworkedBehaviour {
    private static string ZOOM_TIMER = "CameraZoom";
    private static string IDLE_TIMER = "CameraIdle";
    private static string LOCK_TIMER = "CameraLock";
    [Header("Linked Components")]
    public Camera controlled_camera;
    public GameObject home;
    [Header("Camera Settings")]
    public Vector3 target_follow_distance;
    public Vector3 target_follow_angle;
    public bool ManualCamera;
    [HideInInspector]
    public GameObject yaw_pivot;
    [HideInInspector]
    public GameObject pitch_pivot;

    // Camera state
    private ViewMode view_mode;
    private PlayerController current_player;
    private GameObject target_lock;

    private GameObject player_container;
    private Vector2 mouseAccumulator = Vector2.zero;
    private Vector2 idleOrientation = Vector2.zero;
    private CameraMovementFunction handleCameraMove;
    private CameraMovementFunction handlePlayerRotate;

    // Managers
    private InputManager input_manager;
    private Utilities utils;

    // Other Settings
    private float transparency_divider_mult;
    private Material opaque_material;
    public Material fade_material;
    private Mesh original_model;
    public Mesh headless_model;
    public bool show_model_in_inspection = false;

    // Constants
    private float upVelTrackingPointMult = 4.615f;
    private float upVelTrackingLimitMult = 9f;

    // Use this for initialization
    private void Setup() {
        QualitySettings.vSyncCount = 0;
        // Application.targetFrameRate = 45;
        transparency_divider_mult = 8;
        if (home == null) {
            home = transform.parent.gameObject;
        }
        player_container = home.transform.parent.gameObject;
        yaw_pivot = new GameObject("yaw_pivot");
        yaw_pivot.transform.parent = player_container.transform;
        pitch_pivot = new GameObject("pitch_pivot");
        pitch_pivot.transform.parent = yaw_pivot.transform;
        if (controlled_camera == null) {
            controlled_camera = gameObject.AddComponent<Camera>();
            controlled_camera.nearClipPlane = 0.01f;
        }

        input_manager = InputManager.Instance;
        utils = Utilities.Instance;
        utils.CreateTimer(ZOOM_TIMER, 0.5f);
        utils.CreateTimer(IDLE_TIMER, 1.0f);
        utils.CreateTimer(LOCK_TIMER, 0.1f);

        opaque_material = home.GetComponentInChildren<SkinnedMeshRenderer>().material;
        original_model = home.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;

        PlayerController player_home = home.GetComponent<PlayerController>();
        if (player_home == null) {
            throw new Exception("Failed initializing camera.");
        }
        //SetShooterVars(player_home);
        SetThirdPersonActionVars(player_home);
        //SetThirdPersonShooterVars(player_home);
    }

    void Start() {
        if (!IsOwner) return;
        Setup();
        // TODO: Move this mouse hiding logic somewhere else
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SetShooterVars(PlayerController target) {
        view_mode = ViewMode.Shooter;
        handleCameraMove = FirstPersonCameraMove;
        handlePlayerRotate = FirstPersonPlayerRotate;
        target_follow_angle = Vector3.zero;

        if (current_player != null) {
            current_player.player_camera = null;
        }
        yaw_pivot.transform.parent = target.transform;
        yaw_pivot.transform.localPosition = Vector3.zero;
        target.player_camera = this;
        current_player = target;
        current_player.UnregisterJumpCallback(ThirdPersonJumpCallback);

        // Attach the camera to the yaw_pivot and set the default distance/angles
        yaw_pivot.transform.localRotation = Quaternion.identity;
        transform.parent = yaw_pivot.transform;
        transform.localRotation = Quaternion.Euler(target_follow_angle);
        SetTargetPos();
    }

    private void SetThirdPersonActionVars(PlayerController target) {
        view_mode = ViewMode.Third_Person;
        handleCameraMove = ThirdPersonCameraMove;
        handlePlayerRotate = ThirdPersonPlayerRotate;
        target_follow_angle = new Vector3(14f, 0, 0);

        if (current_player != null) {
            current_player.player_camera = null;
        }
        target.player_camera = this;
        current_player = target;
        current_player.RegisterJumpCallback(ThirdPersonJumpCallback);

        // Attach the camera to the yaw_pivot and set the default distance/angles
        yaw_pivot.transform.parent = player_container.transform;
        transform.parent = pitch_pivot.transform;
        transform.localRotation = Quaternion.Euler(target_follow_angle);
        SetTargetPos();
    }

    private void SetThirdPersonShooterVars(PlayerController target) {
        view_mode = ViewMode.Third_Person_Shooter;
        handleCameraMove = ThirdPersonShooterCameraMove;
        handlePlayerRotate = delegate { };
        target_follow_angle = new Vector3(0, 0, 0);

        if (current_player != null) {
            current_player.player_camera = null;
        }
        target.player_camera = this;
        current_player = target;
        current_player.UnregisterJumpCallback(ThirdPersonJumpCallback);

        // Attach the camera to the yaw_pivot and set the default distance/angles
        yaw_pivot.transform.parent = player_container.transform;
        transform.parent = pitch_pivot.transform;
        transform.localRotation = Quaternion.Euler(target_follow_angle);
        SetTargetPos();
    }

    public void ThirdPersonJumpCallback() {
        if ((current_player.IsWallRunning() || current_player.CanWallJump()) && !input_manager.GetCenterCameraHold()) {
            RotatePlayerToward(direction: Vector3.ProjectOnPlane(current_player.GetVelocity(), Physics.gravity),
                               lerp_factor: 1.0f);
        }
    }

    public void RotatePlayerToward(Vector3 direction, float lerp_factor) {
        direction.Normalize();
        Vector3 angles = current_player.transform.localEulerAngles;
        float delta_angle = Vector3.SignedAngle(current_player.transform.forward, direction, Vector3.up);
        angles.y += Mathf.LerpAngle(0, delta_angle, lerp_factor);
        current_player.transform.localEulerAngles = angles;
    }

    public void RotateCameraToward(Vector3 direction, float lerp_factor) {
        direction.Normalize();
        Vector2 target_mouse_accum = EulerToMouseAccum(Quaternion.LookRotation(direction).eulerAngles);
        mouseAccumulator.x = Mathf.LerpAngle(mouseAccumulator.x, target_mouse_accum.x, lerp_factor);
        mouseAccumulator.y = Mathf.LerpAngle(mouseAccumulator.y, target_mouse_accum.y, lerp_factor);
        idleOrientation = mouseAccumulator;
    }

    public void RotateIdleCameraToward(Vector3 direction, float lerp_factor) {
        direction.Normalize();
        Vector2 target_mouse_accum = EulerToMouseAccum(Quaternion.LookRotation(direction).eulerAngles);
        idleOrientation.x = Mathf.LerpAngle(idleOrientation.x, target_mouse_accum.x, lerp_factor);
        idleOrientation.y = Mathf.LerpAngle(idleOrientation.y, target_mouse_accum.y, lerp_factor);
    }

    private void Update() {
        if (!IsOwner) return;
        handleViewToggle();
    }

    private void handleViewToggle() {
        if (input_manager.GetToggleView()) {
            utils.ResetTimer(ZOOM_TIMER);
            if (view_mode == ViewMode.Shooter) {
                SetThirdPersonActionVars(current_player);
                if (original_model && show_model_in_inspection) {
                    SkinnedMeshRenderer[] renderers = home.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach (SkinnedMeshRenderer render in renderers) {
                        render.sharedMesh = original_model;
                    }
                }
            }
            else if (view_mode == ViewMode.Third_Person) {
                SetShooterVars(current_player);
                if (headless_model && show_model_in_inspection) {
                    SkinnedMeshRenderer[] renderers = home.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach (SkinnedMeshRenderer render in renderers) {
                        render.sharedMesh = headless_model;
                    }
                }
            }
        }
    }

    // LateUpdate is called after update. Ensures we are operating on the latest transform changes.
    private void LateUpdate() {
        if (!IsOwner) return;
        UpdateCameraAngles();
    }

    // Rotate the camera
    private void UpdateCameraAngles() {
        // Accumulate the angle changes and ensure x revolves in (-360, 360) and y is clamped in (-90,90)
        Vector2 mouse_input = input_manager.GetMouseMotion();
        if (mouse_input.magnitude > 0.01f) {
            utils.ResetTimer(IDLE_TIMER);
            idleOrientation = mouseAccumulator;
        }
        mouseAccumulator += mouse_input;
        mouseAccumulator.x = mouseAccumulator.x % 360;
        mouseAccumulator.y = Mathf.Clamp(mouseAccumulator.y, -90, 90);
        if (current_player != null) {
            handleCameraMove();
        }
    }

    private void FirstPersonCameraMove() {
        // Set camera pitch
        transform.localRotation = Quaternion.AngleAxis(
            -mouseAccumulator.y, Vector3.right);
        // Set player yaw (and camera with it)
        current_player.transform.localRotation = Quaternion.AngleAxis(
            mouseAccumulator.x, Vector3.up);
    }

    private void ThirdPersonCameraMove() {
        if (mouseAccumulator.x < 0) {
            mouseAccumulator.x = 360 + mouseAccumulator.x;
        }
        mouseAccumulator.y = Mathf.Clamp(mouseAccumulator.y, -65, 75);
        // set the pitch pivots pitch
        pitch_pivot.transform.localRotation = Quaternion.AngleAxis(
            -mouseAccumulator.y, Vector3.right);
        // set the yaw pivots yaw
        yaw_pivot.transform.localRotation = Quaternion.AngleAxis(
            mouseAccumulator.x, Vector3.up);

        if (input_manager.GetCenterCameraRelease()) {
            utils.SetTimerFinished(IDLE_TIMER);
            Vector2 orientation = EulerToMouseAccum(current_player.transform.eulerAngles);
            if (Mathf.Abs(Mathf.DeltaAngle(orientation.x, mouseAccumulator.x)) > 15f) {
                idleOrientation = mouseAccumulator = EulerToMouseAccum(current_player.transform.eulerAngles);
            }
        }
        AvoidWalls();
    }

    private void ThirdPersonShooterCameraMove() {
        // Set camera pitch
        pitch_pivot.transform.localRotation = Quaternion.AngleAxis(
            -mouseAccumulator.y, Vector3.right);
        // Set player yaw (and camera with it)
        yaw_pivot.transform.localRotation = Quaternion.AngleAxis(
            mouseAccumulator.x, Vector3.up);
        // Set the players yaw to match our velocity
        if (!current_player.IsHanging()) {
            current_player.transform.rotation = yaw_pivot.transform.rotation;
        }
        else {
            RotatePlayerToward(direction: -Vector3.ProjectOnPlane(current_player.GetLastWallNormal(), Physics.gravity),
                               lerp_factor: 1.0f);
        }
        yaw_pivot.transform.position = current_player.transform.position;
        AvoidWalls();
    }

    private void FixedUpdate() {
        if (!IsOwner) return;
        SetTargetPos();
        HideHome();
        handlePlayerRotate();
        if (view_mode != ViewMode.Shooter) {
            AvoidWalls();
        }
    }

    private void SetTargetPos() {
        switch (GetViewMode()) {
            case ViewMode.Shooter:
                target_follow_distance = new Vector3(0, current_player.GetHeadHeight(), 0);
                break;
            case ViewMode.Third_Person:
                target_follow_distance = new Vector3(
                    0, current_player.GetHeadHeight() * 0.75f, -current_player.cc.height * 1.75f);
                break;
            case ViewMode.Third_Person_Shooter:
                target_follow_distance = new Vector3(
                    current_player.cc.radius * 2f, current_player.GetHeadHeight(), -current_player.cc.height);
                break;
        }
    }

    private void HideHome() {
        Color textureColor;
        SkinnedMeshRenderer[] renderers = home.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer render in renderers) {
            if (view_mode == ViewMode.Third_Person || !show_model_in_inspection) {
                float distance_to_head = (current_player.GetHeadHeight() * current_player.transform.up + current_player.transform.position - transform.position).magnitude;
                if (distance_to_head < (transparency_divider_mult * current_player.cc.radius)) {
                    if (render.material != fade_material) render.material = fade_material;
                    textureColor = render.material.color;
                    textureColor.a = Mathf.Pow(distance_to_head / (transparency_divider_mult * current_player.cc.radius), 2);
                    render.material.color = textureColor;
                }
                else {
                    if (render.material != opaque_material) render.material = opaque_material;
                }
            }
            else {
                if (render.material != opaque_material) render.material = opaque_material;
            }
        }
    }

    private void FirstPersonPlayerRotate() {
        if (!utils.CheckTimer(ZOOM_TIMER)) {
            transform.localPosition = Vector3.Lerp(transform.localPosition, target_follow_distance, utils.GetTimerPercent(ZOOM_TIMER));
        }
        else {
            transform.localPosition = target_follow_distance;
        }
    }

    private void ThirdPersonPlayerRotate() {
        // Set the players yaw to match our velocity
        Vector3 move_vector = current_player.GetMoveVector();
        Vector3 ground_velocity = Vector3.ProjectOnPlane(current_player.cc.velocity, Physics.gravity);

        Vector3 desired_move = Vector3.zero;
        float interp_multiplier = 1f;

        if (ground_velocity.magnitude < (current_player.RunSpeedMult * current_player.cc.radius / 3)) {
            //Debug.Log("Controller move");
            desired_move = move_vector.normalized;
            interp_multiplier = 0.5f;
        }
        else {
            //Debug.Log("Velocity move");
            desired_move = Vector3.ProjectOnPlane(current_player.GetVelocity(), Physics.gravity).normalized;
        }

        // Rotate a player toward the last static ledge they were hanging on
        if (current_player.IsHanging() && !current_player.InMovingCollision()) {
            desired_move = -Vector3.ProjectOnPlane(current_player.GetLastHangingNormal(), Physics.gravity).normalized;
        }
        // Snap the player to the wall if they are climbing and not wall running
        else if (current_player.IsWallClimbing() && current_player.CanGrabLedge()) {
            desired_move = -Vector3.ProjectOnPlane(current_player.GetLastWallNormal(), Physics.gravity).normalized;
        }
        // Allow the player to press up against a wall
        else if (current_player.IsOnWall()) {
            if (Vector3.Dot(current_player.GetLastWallNormal(), current_player.GetMoveVector()) < -0.7f) {
                desired_move = -Vector3.ProjectOnPlane(current_player.GetLastWallNormal(), Physics.gravity).normalized;
            }
        }
        // If we have a valid move, rotate the player toward it
        if (desired_move != Vector3.zero && (current_player.IsHanging() || (!input_manager.GetCenterCameraHold() && !input_manager.GetCenterCameraRelease()))) {
            RotatePlayerToward(direction: desired_move, lerp_factor: 0.1f * interp_multiplier);
        }

        HandleTargetLock();
        FollowPlayerVelocity();
        ApplyForcesToCamera();
        if (utils.CheckTimer(IDLE_TIMER)) {
            RotateTowardIdleOrientation();
        }
    }

    private void HandleTargetLock() {
        bool lock_requested = !utils.CheckTimer(LOCK_TIMER);
        if (input_manager.GetCenterCameraHold() || lock_requested) {
            utils.ResetTimer(IDLE_TIMER);
            Vector2 orientation = EulerToMouseAccum(current_player.transform.eulerAngles);
            if (target_lock) {
                Vector3 dir = target_lock.transform.position - current_player.transform.position;
                orientation = EulerToMouseAccum(Quaternion.LookRotation(dir, current_player.transform.up).eulerAngles);
            }
            mouseAccumulator.x = Mathf.LerpAngle(mouseAccumulator.x, orientation.x, 0.1f);
            mouseAccumulator.y = Mathf.LerpAngle(mouseAccumulator.y, orientation.y, 0.1f);
            idleOrientation = mouseAccumulator;
        }
        if (!lock_requested) {
            target_lock = null;
        }
    }

    private void RequestTargetLock(GameObject target = null) {
        target_lock = target;
        utils.ResetTimer(LOCK_TIMER);
    }

    // TODO: Convert to using torque
    private void FollowPlayerVelocity() {
        if (utils.CheckTimer(IDLE_TIMER) && !ManualCamera) {
            Vector3 player_ground_vel;
            if (current_player.OnGround()) {
                player_ground_vel = current_player.GetPlaneVelocity(use_cc: true);
            }
            else {
                player_ground_vel = current_player.GetGroundVelocity(use_cc: true);
                Vector3 player_up_vel = current_player.cc.velocity - player_ground_vel;
                if (player_up_vel.magnitude > current_player.GetStandingHeight() * upVelTrackingPointMult) {
                    player_up_vel = Vector3.ClampMagnitude(player_up_vel, current_player.GetStandingHeight() * upVelTrackingLimitMult);
                    player_ground_vel += player_up_vel - (player_up_vel.normalized * current_player.GetStandingHeight() * upVelTrackingPointMult);
                }
            }

            if (player_ground_vel.normalized != Vector3.zero && Vector3.Dot(player_ground_vel.normalized, yaw_pivot.transform.forward) > -0.8) {
                Quaternion velocity_angle = Quaternion.LookRotation(player_ground_vel.normalized, current_player.transform.up);
                idleOrientation = EulerToMouseAccum(velocity_angle.eulerAngles);
            }
            else if (player_ground_vel.normalized == Vector3.zero) {
                idleOrientation.x = Mathf.LerpAngle(idleOrientation.x, mouseAccumulator.x, 0.01f);
                idleOrientation.y = Mathf.LerpAngle(idleOrientation.y, mouseAccumulator.y, 0.01f);
            }
            else {
                //idleOrientation = EulerToMouseAccum(current_player.transform.eulerAngles);
                mouseAccumulator.x += Vector3.Dot(player_ground_vel, yaw_pivot.transform.right) * 0.075f;
                mouseAccumulator.y = Mathf.LerpAngle(mouseAccumulator.y, 0f, 0.01f);
                idleOrientation = mouseAccumulator;
            }
        }
    }

    private bool CheckCameraSphere() {
        return CheckCameraSphere(position: transform.position);
    }

    private bool CheckCameraSphere(Vector3 position) {
        return Physics.CheckSphere(position: position, radius: GetCameraRadius());
    }

    private bool SphereCastCamera(Vector3 new_position, out RaycastHit hitInfo) {
        return SphereCastCamera(position: transform.position, direction: new_position - transform.position, hitInfo: out hitInfo);
    }

    private bool SphereCastCamera(Vector3 position, Vector3 direction, out RaycastHit hitInfo) {
        return Physics.SphereCast(origin: position, radius: GetCameraRadius(), direction: direction, hitInfo: out hitInfo, maxDistance: direction.magnitude);
    }

    private void ApplyForcesToCamera() {
        float cameraPushDelta = 30f * GetCameraRadius() * Time.fixedDeltaTime;
        yaw_pivot.transform.position = Vector3.Lerp(yaw_pivot.transform.position, current_player.GetHeadPosition(), 0.025f);
        if (CheckCameraSphere()) {
            transform.localPosition -= target_follow_distance.normalized * cameraPushDelta;
        }
        else {
            Vector3 new_local_pos = Vector3.Lerp(transform.localPosition, target_follow_distance, 0.1f);
            // if (SphereCastCamera(new_position: transform.TransformPoint(new_local_pos), hitInfo: out RaycastHit hitInfo)) {
            if (!CheckCameraSphere(position: pitch_pivot.transform.TransformPoint(new_local_pos))) {
                transform.localPosition = new_local_pos;
            }
        }
    }

    private Vector2 EulerToMouseAccum(Vector3 euler_angle) {
        float pitch = euler_angle.x;
        float yaw = euler_angle.y;
        float adjusted_pitch = 360 - pitch < pitch ? 360 - pitch : -pitch;
        return new Vector2(yaw, adjusted_pitch);
    }

    private float GetCameraRadius() {
        return current_player.cc.radius;
    }

    private void AvoidWalls() {
        Vector3 startpos = pitch_pivot.transform.position;
        Vector3 world_target_vec = pitch_pivot.transform.TransformVector(target_follow_distance);

        if (Physics.Raycast(startpos, world_target_vec.normalized, out RaycastHit hit, world_target_vec.magnitude + GetCameraRadius())) {
            Vector3 distance_to_hit = (hit.point - startpos);
            float target_distance_delta = GetCameraRadius() / Mathf.Abs(Vector3.Dot(hit.normal, world_target_vec.normalized));
            float new_target_magnitude = distance_to_hit.magnitude - target_distance_delta;
            Vector3 feet_to_hit = hit.point - current_player.GetFootPosition();
            float camera_feet_delta = Vector3.Project(feet_to_hit, Physics.gravity).magnitude;
            float foot_cam_coefficient = Mathf.Clamp((camera_feet_delta / GetCameraRadius()) - 1f, 0f, 1f);

            float horizontal_displacement = Mathf.Max(Vector3.ProjectOnPlane(feet_to_hit, Physics.gravity).magnitude - (2f * GetCameraRadius()), 0f);
            float vertical_offset_coefficient = Mathf.Clamp(horizontal_displacement / current_player.cc.height, 0f, 1f);
            transform.localPosition = target_follow_distance.normalized * new_target_magnitude * (foot_cam_coefficient + ((1 - foot_cam_coefficient) * vertical_offset_coefficient));
            transform.localPosition = new Vector3(target_follow_distance.x, transform.localPosition.y, transform.localPosition.z); // Needed for thirdperson shooter mode
        }
    }

    // TODO: Convert to using torque
    private void RotateTowardIdleOrientation() {
        if (ManualCamera) {
            return;
        }
        if (!current_player.IsHanging()) {
            Vector3 player_ground_vel = Vector3.ProjectOnPlane(current_player.cc.velocity, current_player.transform.up);
            float lerp_factor = Mathf.Max(player_ground_vel.magnitude / (current_player.RunSpeedMult * current_player.cc.radius), 0.2f);
            mouseAccumulator.x = Mathf.LerpAngle(mouseAccumulator.x, idleOrientation.x, 0.005f * lerp_factor);
            mouseAccumulator.y = Mathf.LerpAngle(mouseAccumulator.y, idleOrientation.y, 0.005f * lerp_factor);
        }
        else {
            idleOrientation = mouseAccumulator;
        }
    }

    public ViewMode GetViewMode() {
        return view_mode;
    }
}
