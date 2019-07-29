using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using MLAPI;

[AddComponentMenu("PhysicsProps/Water")]
public class Water : PhysicsProp {
    [Header("Constant forces")]
    public float waterFriction;
    public bool constrainToSurface = false;
    public bool constrainToVolume = false;
    // TODO: Currents
}