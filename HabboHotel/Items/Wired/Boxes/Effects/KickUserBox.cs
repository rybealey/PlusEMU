using System.Collections;
using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Items.Wired.Boxes.Effects;

internal class KickUserBox : IWiredItem, IWiredCycle
{
    private readonly Queue _toKick;

    public KickUserBox(Room instance, Item item)
    {
        Instance = instance;
        Item = item;
        SetItems = new();
        TickCount = Delay;
        _toKick = new();
        if (SetItems.Count > 0)
            SetItems.Clear();
    }

    public int TickCount { get; set; }
    public int Delay { get; set; }

    public bool OnCycle()
    {
        if (Instance == null)
            return false;
        if (_toKick.Count == 0)
        {
            TickCount = 3;
            return true;
        }
        lock (_toKick.SyncRoot)
        {
            while (_toKick.Count > 0)
            {
                var player = (Habbo)_toKick.Dequeue();
                if (player == null || !player.InRoom || player.CurrentRoom != Instance)
                    continue;
                Instance.GetRoomUserManager().RemoveUserFromRoom(player.Client, true);
            }
        }
        TickCount = 3;
        return true;
    }

    public Room Instance { get; set; }
    public Item Item { get; set; }
    public WiredBoxType Type => WiredBoxType.EffectKickUser;
    public ConcurrentDictionary<uint, Item> SetItems { get; set; }
    public string StringData { get; set; }
    public bool BoolData { get; set; }
    public string ItemsData { get; set; }

    public void HandleSave(IIncomingPacket packet)
    {
        if (SetItems.Count > 0)
            SetItems.Clear();
        var unknown = packet.ReadInt();
        var message = packet.ReadString();
        StringData = message;
    }

    public bool Execute(params object[] @params)
    {
        if (@params.Length != 1)
            return false;
        var player = (Habbo)@params[0];
        if (player == null)
            return false;
        if (TickCount <= 0)
            TickCount = 3;
        if (!_toKick.Contains(player))
        {
            var user = Instance.GetRoomUserManager().GetRoomUserByHabbo(player.Id);
            if (user == null)
                return false;
            if (player.Permissions.HasRight("mod_tool") || Instance.OwnerId == player.Id)
            {
                player.Client.Send(new WhisperComposer(user.VirtualId, "Wired Kick Exception: Unkickable Player", 0, 1));
                return false;
            }
            _toKick.Enqueue(player);
            player.Client.Send(new WhisperComposer(user.VirtualId, StringData, 0, 0));
        }
        return true;
    }
}