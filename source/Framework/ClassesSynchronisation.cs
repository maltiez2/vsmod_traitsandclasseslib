using ProtoBuf;
using System.Diagnostics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace TraitsAndClassesLib;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class SetClassPacket
{
    public long PlayerEntityId { get; set; }
    public byte[] SerializedClases { get; set; } = [];
    public bool GiveNewEquipment { get; set; } = false;
}

public sealed class ClassesSynchronisationSystemClient
{
    public ClassesSynchronisationSystemClient(ICoreClientAPI api)
    {
        _api = api;
        _clientChannel = api.Network.RegisterChannel("TraitsAndClassesLib:ClassesSynchronisation")
            .RegisterMessageType<SetClassPacket>();
    }

    public void SetPlayerClass(EntityPlayer player, PlayerClasses classes, bool giveClassEquipment = false, TraitsAndClassesLibSystem? system = null)
    {
        system ??= _api.ModLoader.GetModSystem<TraitsAndClassesLibSystem>();
        if (system == null) return;

        TreeAttribute classesAttribute = new();
        classes.WriteToAttributes(classesAttribute);

        SetClassPacket packet = new()
        {
            PlayerEntityId = player.EntityId,
            SerializedClases = classesAttribute.ToBytes(),
            GiveNewEquipment = giveClassEquipment
        };

        Debug.WriteLine("Sent SetClassPacket");
        _clientChannel.SendPacket(packet);
    }

    private readonly IClientNetworkChannel _clientChannel;
    private readonly ICoreClientAPI _api;
}

public sealed class ClassesSynchronisationSystemServer
{
    public ClassesSynchronisationSystemServer(ICoreServerAPI api)
    {
        _api = api;
        _serverChannel = api.Network.RegisterChannel("TraitsAndClassesLib:ClassesSynchronisation")
            .RegisterMessageType<SetClassPacket>()
            .SetMessageHandler<SetClassPacket>(SetClassPacketHandler);
    }


    public void SetPlayerClass(EntityPlayer player, PlayerClasses classes, bool giveClassEquipment = false, TraitsAndClassesLibSystem? system = null)
    {
        system ??= _api?.ModLoader.GetModSystem<TraitsAndClassesLibSystem>();
        if (system == null) return;

        classes.WriteToAttributes(player.WatchedAttributes);
        player.WatchedAttributes.MarkPathDirty(PlayerClasses.AttributeCode);
        player.UpdaitTraits(system);

        if (giveClassEquipment)
        {
            player.GiveClassEquipment(system);
        }
    }



    private readonly IServerNetworkChannel? _serverChannel;
    private readonly ICoreServerAPI _api;


    private void SetClassPacketHandler(IServerPlayer player, SetClassPacket packet)
    {
        Debug.WriteLine("Received SetClassPacket");

        TraitsAndClassesLibSystem? system = _api.ModLoader.GetModSystem<TraitsAndClassesLibSystem>();
        if (system == null || player.Entity == null || player.Entity.EntityId != packet.PlayerEntityId) return;

        TreeAttribute classesAttribute = new();
        classesAttribute.FromBytes(packet.SerializedClases);
        PlayerClasses newPlayerClasses = PlayerClasses.FromAttributes(classesAttribute, system);
        newPlayerClasses.WriteToAttributes(player.Entity.WatchedAttributes);
        player.Entity.WatchedAttributes.MarkPathDirty(PlayerClasses.AttributeCode);

        player.Entity.UpdaitTraits(system);

        if (packet.GiveNewEquipment)
        {
            player.Entity.GiveClassEquipment(system);
        }
    }
}

