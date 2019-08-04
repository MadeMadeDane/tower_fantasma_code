using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;

public class SharedItem : Item {
    protected InputManager im;
    protected Utilities utils;
    protected PlayerController player;
    public override void Start() {
        base.Start();
        im = InputManager.Instance; 
        utils = Utilities.Instance;
        player = utils.get<PlayerController>();
    }
    public static bool isSharedItem(Item item) {
        return item.type == typeof(SharedItem);
    }
    public SharedItem(NetworkedBehaviour context = null) : base(context) {
        type = typeof(SharedItem);
    }
    protected bool SharedItemButtonPress() {
        bool ret = im.GetSharedItem();
        return ret;
    }
    protected bool SharedItemButtonHold() {
        bool ret = im.GetSharedItemHold();
        return ret;
    }
}
public class NetworkSharedItem {
    public string name;

    public NetworkSharedItem(string name) {
        this.name = name;
    }
}