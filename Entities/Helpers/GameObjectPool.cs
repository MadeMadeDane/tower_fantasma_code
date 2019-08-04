using UnityEngine;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;
using System.Linq;

public class GameObjectPool {
    private List<GameObject> pool = new List<GameObject>();
    public GameObject _prefab;
    public GameObjectPool(GameObject prefab) {
        _prefab = prefab;
    }

    private GameObject get_new_instance() {
        GameObject go = GameObject.Instantiate(_prefab);
        return go;
    }

    private IEnumerable<GameObject> get_from_pool(int number) {
        if (number == 0) yield break;
        // Immediately yield new objects if our pool doesn't have enough
        while (number > pool.Count) {
            GameObject go = get_new_instance();
            go.SetActive(true);
            yield return go;
            number--;
        }
        // Grab the remainder out of the pool
        for (int i = 0; i < number; i++) {
            GameObject obj = pool[0];
            pool.RemoveAt(0);
            obj.SetActive(true);
            yield return obj;
        }
    }

    private void destroy(GameObject obj) {
        obj.SetActive(false);
        pool.Add(obj);
    }

    public void request(in List<GameObject> container, int number) {
        if (number < 0) number = 0;
        int delta = number - container.Count;
        if (delta >= 0) {
            container.AddRange(get_from_pool(delta));
        }
        else {
            delta = Mathf.Abs(delta);
            for (int i = 0; i < delta; i++) {
                GameObject obj = container[0];
                container.RemoveAt(0);
                destroy(obj);
            }
        }
    }

    public void destroy(in List<GameObject> toDestroy) {
        request(toDestroy, 0);
    }
}