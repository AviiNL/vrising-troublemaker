using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace troublemaker;

public class Troublemaker : MonoBehaviour
{
    public static Troublemaker? Instance;
    internal Queue<RconMessage> _RconMessages = new Queue<RconMessage>();

    private ulong rconId = 0;

    internal RconMessage queue(System.Func<string> action)
    {
        var msg = new RconMessage() {
            RconId = rconId++,
            action = action
        };

        _RconMessages.Enqueue(msg);
        return _RconMessages.Peek();
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        while (_RconMessages.Count > 0)
        {
            var message = _RconMessages.Dequeue();
            message.Response = message.action?.Invoke();
        }
    }
}