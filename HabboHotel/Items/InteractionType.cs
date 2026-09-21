namespace Plus.HabboHotel.Items;

public enum InteractionType
{
    None,
    Gate,
    Postit,
    Moodlight,
    Trophy,
    Bed,
    Scoreboard,
    VendingMachine,
    Alert,
    OneWayGate,
    LoveShuffler,
    HabboWheel,
    Dice,
    Bottle,
    Hopper,
    Teleport,
    Pool,
    Roller,
    FootballGate,
    Pet,
    IceSkates,
    NormalSkates,
    Lowpool,
    Haloweenpool,
    Football,
    FootballGoalGreen,
    FootballGoalYellow,
    FootballGoalBlue,
    FootballGoalRed,
    Footballcountergreen,
    Footballcounteryellow,
    Footballcounterblue,
    Footballcounterred,
    Banzaigateblue,
    Banzaigatered,
    Banzaigateyellow,
    Banzaigategreen,
    Banzaifloor,
    Banzaiscoreblue,
    Banzaiscorered,
    Banzaiscoreyellow,
    Banzaiscoregreen,
    Banzaicounter,
    Banzaitele,
    Banzaipuck,
    Banzaipyramid,
    Freezetimer,
    Freezeexit,
    Freezeredcounter,
    Freezebluecounter,
    Freezeyellowcounter,
    Freezegreencounter,
    FreezeYellowGate,
    FreezeRedGate,
    FreezeGreenGate,
    FreezeBlueGate,
    FreezeTileBlock,
    FreezeTile,
    Jukebox,
    MusicDisc,
    PuzzleBox,
    Toner,


    PressurePad,

    /// <summary>
    /// pixelrp: a pressure pad that only lets an on-duty worker across.
    ///
    /// Lights exactly as PressurePad does - it IS one, with a condition - but
    /// anyone not clocked in cannot step onto it at all. Corporation-agnostic
    /// on purpose: this marks staff-only ground, not one company's ground.
    /// </summary>
    CorpGate,

    /// <summary>
    /// pixelrp: the hospital's drop-off point.
    ///
    /// Inert for everyone except a paramedic who is carrying somebody: when one
    /// steps onto it, the patient they are escorting is laid on the nearest bed
    /// in the room and the transport ends. Anyone else - including a police
    /// officer marching a suspect across it - walks over an ordinary tile.
    ///
    /// The behaviour is on the TILE rather than on the bed so a hospital can
    /// decide where the ambulance bay is, and so a ward can be rearranged
    /// without re-flagging every bed in it.
    /// </summary>
    ParamedicDropoff,

    /// <summary>
    /// pixelrp: a hospital bed that actually treats people.
    ///
    /// Three things at once, which is why it is a behaviour and not three:
    /// anyone resting on it has their health refilled (two minutes, one for
    /// VIP), nobody can be escorted off it, and once they are whole again they
    /// are discharged to one of the furni named in the behaviour's id list.
    ///
    /// The immunity is the part that is easy to miss the point of: without it
    /// a patient mid-treatment could simply be carried away, which makes the
    /// bed a decoration rather than a place that finishes a job.
    /// </summary>
    MedicalBed,

    WfFloorSwitch1,
    WfFloorSwitch2,

    Gift,
    Background,
    Mannequin,
    GateVip,
    GuildItem,
    GuildGate,
    GuildForum,

    Tent,
    TentSmall,
    BadgeDisplay,
    Stacktool,
    Television,

    WiredEffect,
    WiredTrigger,
    WiredCondition,

    Wallpaper,
    Floor,
    Landscape,

    Badge,
    CrackableEgg,
    Effect,
    Deal,
    Roomdeal,

    HorseSaddle1,
    HorseSaddle2,
    HorseHairstyle,
    HorseBodyDye,
    HorseHairDye,

    GnomeBox,
    Bot,
    PurchasableClothing,
    PetBreedingBox,
    Arrow,
    Lovelock,
    MonsterplantSeed,
    Cannon,
    Counter,
    CameraPicture,
    FxProvider,
    Exchange,
    DressingBooth,

    // pixelrp: step on it and the Clothing Store opens. A BEHAVIOUR rather than
    // a classname check, so any piece can be a shop doorway - a mat, a till, a
    // rail - without a code change. This replaced the :zara command outright:
    // a shop you walk into beats a word you type from anywhere in the hotel.
    ZaraShop,

    // pixelrp: the ATM. A BEHAVIOUR rather than a classname check, so
    // a bank set can put it on a teller window or a wall panel without a code
    // change - the same route the jukebox took.
    Atm
}