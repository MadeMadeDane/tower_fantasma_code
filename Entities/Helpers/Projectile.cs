using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;

public class Projectile : NetworkedBehaviour {
    public Func<GameObject, bool> callback;
    public Vector3 velocity = Vector3.zero;
    public float lifetime = 0f;
    public float maxDistance = 0f;
    public GameObject target;
    private float desiredSpeed = 0f;

    public GameObject shooter;
    private string LIFETIMER;
    private void Start() {
        LIFETIMER = $"projectile_life_{gameObject.GetInstanceID()}";
        Utilities.Instance.CreateTimer(LIFETIMER, lifetime);
        desiredSpeed = velocity.magnitude;
    }
    private void FixedUpdate() {
        if (Utilities.Instance.CheckTimer(LIFETIMER)) Destroy(gameObject);
        if (maxDistance > 0 && (shooter.transform.position - transform.position).magnitude > maxDistance) Destroy(gameObject);

        if (target) velocity = desiredSpeed * (target.transform.position - transform.position).normalized;
        transform.position += velocity * Time.fixedDeltaTime;

    }
    private void OnTriggerEnter(Collider other) {
        if (other.isTrigger) return;
        if (callback(other.gameObject)) Destroy(gameObject);
    }
}