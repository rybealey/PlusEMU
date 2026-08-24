using Dapper;
using Microsoft.Extensions.Logging;
using Plus.Communication.Attributes;
using Plus.Communication.Packets.Outgoing.BuildersClub;
using Plus.Communication.Packets.Outgoing.Handshake;
using Plus.Communication.Packets.Outgoing.Inventory.Achievements;
using Plus.Communication.Packets.Outgoing.Inventory.AvatarEffects;
using Plus.Communication.Packets.Outgoing.Moderation;
using Plus.Communication.Packets.Outgoing.Navigator;
using Plus.Communication.Packets.Outgoing.Notifications;
using Plus.Communication.Packets.Outgoing.Rooms.Session;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.Communication.Packets.Outgoing.Sound;
using Plus.Core;
using Plus.Core.FigureData;
using Plus.Core.Language;
using Plus.Core.Settings;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.Badges;
using Plus.HabboHotel.Cache;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Moderation;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rewards;
using Plus.HabboHotel.Subscriptions;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Authentication;
using Plus.HabboHotel.Users.Messenger.FriendBar;
using Plus.HabboHotel.Catalog.Clothing;
using Plus.HabboHotel.Users.Clothing;

namespace Plus.Communication.Packets.Incoming.Handshake;

[NoAuthenticationRequired]
public class SsoTicketEvent : IPacketEvent
{
    private readonly IAuthenticator _authenticate;
    private readonly IBadgeManager _badgeManager;
    private readonly IModerationManager _moderationManager;
    private readonly IAchievementManager _achievementManager;
    private readonly IPermissionManager _permissionManager;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly ICacheManager _cacheManager;
    private readonly IFigureDataManager _figureManager;
    private readonly ILanguageManager _languageManager;
    private readonly ISettingsManager _settingsManager;
    private readonly IRewardManager _rewardManager;
    private readonly IClothingManager _clothingManager;
    private readonly ILogger _logger;

    public SsoTicketEvent(IAuthenticator authenticate,
        IBadgeManager badgeManager,
        IModerationManager moderationManager,
        IAchievementManager achievementManager,
        IPermissionManager permissionManager,
        ISubscriptionManager subscriptionManager,
        ICacheManager cacheManager,
        IFigureDataManager figureManager,
        ILanguageManager languageManager,
        ISettingsManager settingsManager,
        IRewardManager rewardManager,
        IClothingManager clothingManager,
        ILogger<SsoTicketEvent> logger)
    {
        _authenticate = authenticate;
        _badgeManager = badgeManager;
        _moderationManager = moderationManager;
        _achievementManager = achievementManager;
        _permissionManager = permissionManager;
        _subscriptionManager = subscriptionManager;
        _cacheManager = cacheManager;
        _figureManager = figureManager;
        _languageManager = languageManager;
        _settingsManager = settingsManager;
        _rewardManager = rewardManager;
        _clothingManager = clothingManager;
        _logger = logger;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var sso = packet.ReadString();
        var error = await _authenticate.AuthenticateUsingSSO(session, sso);
        if (error == null)
        {
            // pixelrp beta: the beta hotel (compose.beta.yaml) sets
            // STAFF_ONLY_LOGIN=1 so only staff (rank >= 5) can enter; a
            // disconnect here gives the same clear "Handshake Failed" the
            // auth-failure path below produces. Unset in prod.
            if (Environment.GetEnvironmentVariable("STAFF_ONLY_LOGIN") == "1" && session.GetHabbo().Rank < 5)
            {
                _logger.LogWarning("Staff-only hotel: rejecting login for {user} (rank {rank}).",
                    session.GetHabbo().Username, session.GetHabbo().Rank);
                session.Disconnect();
                return;
            }

            session.Send(new AuthenticationOkComposer());

            // TODO @80O: Move to individual incoming message handlers.
            session.Send(new AvatarEffectsComposer(session.GetHabbo().Effects.GetAllEffects));
            session.Send(new NavigatorSettingsComposer(session.GetHabbo().HomeRoom));
            session.Send(new FavouritesComposer(session.GetHabbo().FavoriteRooms));
            session.Send(new FigureSetIdsComposer(FullWardrobeUtility.GetVisibleClothingParts(session.GetHabbo(), _clothingManager)));
            session.Send(new UserRightsComposer(session.GetHabbo().Rank, session.GetHabbo().IsAmbassador));
            session.Send(new AvailabilityStatusComposer());
            session.Send(new AchievementScoreComposer(session.GetHabbo().HabboStats.AchievementPoints));
            session.Send(new BuildersClubMembershipComposer());
            session.Send(new CfhTopicsInitComposer(_moderationManager.UserActionPresets));
            session.Send(new BadgeDefinitionsComposer(_achievementManager.Achievements));
            session.Send(new SoundSettingsComposer(session.GetHabbo().ClientVolume, session.GetHabbo().ChatPreference, session.GetHabbo().AllowMessengerInvites,
                session.GetHabbo().FocusPreference,
                FriendBarStateUtility.GetInt(session.GetHabbo().FriendbarState)));
            // pixelrp: persisted UI settings (chrome color scheme).
            session.GetHabbo().EnsureRpUiSettingsLoaded();
            session.Send(new RpUiSettingsComposer(session.GetHabbo().RpUiChromeColor, session.GetHabbo().RpUiChromeOpacity, session.GetHabbo().RpUiHeaderColor, session.GetHabbo().RpUiUsernameColor, session.GetHabbo().RpUiUsernameIcon, session.GetHabbo().RpUiUsernameIconColor));
            session.Send(new RpInventoryComposer(session.GetHabbo().LoadRpInventory()));
            //SendMessage(new TalentTrackLevelComposer());


            if (_permissionManager.TryGetGroup(session.GetHabbo().Rank, out var group))
            {
                if (!string.IsNullOrEmpty(group.Badge))
                {
                    if (!session.GetHabbo().Inventory.Badges.HasBadge(group.Badge))
                        await _badgeManager.GiveBadge(session.GetHabbo(), group.Badge);
                }
            }
            if (_subscriptionManager.TryGetSubscriptionData(session.GetHabbo().VipRank, out var subData))
            {
                if (!string.IsNullOrEmpty(subData.Badge))
                {
                    if (!session.GetHabbo().Inventory.Badges.HasBadge(subData.Badge))
                        await _badgeManager.GiveBadge(session.GetHabbo(), subData.Badge);
                }
            }
            if (!_cacheManager.ContainsUser(session.GetHabbo().Id))
                _cacheManager.GenerateUser(session.GetHabbo().Id);
            session.GetHabbo().Look = _figureManager.ProcessFigure(session.GetHabbo().Look, session.GetHabbo().Gender, session.GetHabbo().HasFullWardrobe ? null : session.GetHabbo().Clothing.GetClothingParts, true);
            session.GetHabbo().InitProcess();
            if (session.GetHabbo().Permissions.HasRight("mod_tickets"))
            {
                session.Send(new ModeratorInitComposer(
                    session.GetHabbo().Id,
                    _moderationManager.UserMessagePresets,
                    _moderationManager.RoomMessagePresets,
                    _moderationManager.GetTickets));
            }
            if (_settingsManager.TryGetValue("user.login.message.enabled") == "1")
                session.Send(new MotdNotificationComposer(_languageManager.TryGetValue("user.login.message")));
            await _rewardManager.CheckRewards(session);

            // pixelrp last-position restore: forward the user into the room they were
            // last in. Entry validation still runs server-side; on denial the client
            // simply stays on hotel view. Spawn position is applied in AddAvatarToRoom.
            // Any DB failure here must degrade to a normal hotel-view login rather than
            // taking down the freshly-authenticated session - PacketManager's fault
            // handler disconnects on any uncaught handler exception.
            try
            {
                using (var dbClient = PlusEnvironment.DatabaseManager.Connection())
                {
                    var last = dbClient.QuerySingleOrDefault<(uint RoomId, int X, int Y, int Rot)>(
                        "SELECT `last_room_id`, `last_x`, `last_y`, `last_rot` FROM `users` WHERE `id` = @userId LIMIT 1",
                        new { userId = session.GetHabbo().Id });
                    if (last.RoomId > 0)
                    {
                        session.GetHabbo().PendingRestore = new PendingRoomRestore(last.RoomId, last.X, last.Y, last.Rot);
                        session.SendRoomForward(last.RoomId);
                    }
                    else
                    {
                        // No saved position (e.g. a brand-new user): spawn into the
                        // default room (id 1) at the door. No PendingRestore, so normal
                        // door placement applies. Entry validation still runs; on denial
                        // the client stays on hotel view.
                        session.SendRoomForward(1);
                    }
                }
            }
            catch (Exception e)
            {
                ExceptionLogger.LogException(e);
            }
        }
        else
        {
            // Previously: nothing happened here at all. The client sends its SSO
            // ticket and, on any failure (bad/expired/reused ticket, account not
            // found, login prohibited), the server sent no response whatsoever -
            // no error, no disconnect. The client then waits forever for a
            // handshake reply that will never arrive, which looks from the
            // outside exactly like a silent hang (loading screen stuck, no
            // console error, no network activity), as opposed to the clear
            // "Handshake Failed" the client shows when the *connection* itself
            // is closed (see ClientHelloEvent's unknown-revision path, which
            // this mirrors). Disconnecting here gives the same clear failure
            // signal instead of an indefinite silent wait.
            _logger.LogWarning("SSO authentication failed ({error}); disconnecting session.", error);
            session.Disconnect();
        }
    }
}