using System;
using ProjectM.Network;
using Unity.Entities;
using Wetstone.API;

namespace troublemaker;

public class Messaging
{

    private static Messaging? _instance;

    private EntityManager _em;

    public Messaging()
    {
        this._em = VWorld.Server.EntityManager;
    }

    public static Messaging getInstance()
    {
        if (_instance == null)
        {
            _instance = new Messaging();
        }
        return _instance;
    }

    public void SendMessage(User user, ServerChatMessageType msg_type, string message)
    {
        var entity = this._em.CreateEntity(
            ComponentType.ReadOnly<NetworkEventType>(),      //event type
            ComponentType.ReadOnly<SendEventToUser>(),       //send it to user
            ComponentType.ReadOnly<ChatMessageServerEvent>() // what event
        );

        NetworkId nid = this._em.GetComponentData<NetworkId>(user.LocalCharacter._Entity);

        var ev1 = new ChatMessageServerEvent();
        ev1.MessageText = message;
        ev1.MessageType = msg_type;
        ev1.FromUser = nid;
        ev1.TimeUTC = DateTime.Now.ToFileTimeUtc();

        this._em.SetComponentData<SendEventToUser>(entity, new()
        {
            UserIndex = user.Index
        });

        this._em.SetComponentData<NetworkEventType>(entity, new()
        {
            EventId = NetworkEvents.EventId_ChatMessageServerEvent,
            IsAdminEvent = false,
            IsDebugEvent = false
        });

        //fire off the event
        this._em.SetComponentData(entity, ev1);
    }

}