using System.Reflection;
using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.IO.Compression;
using Aion.Commons.Database;
using Aion.GameServer.Dao;
using MySqlConnector;
using Aion.Commons.Nio;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Aion.GameServer.Utils.Xml;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class HousingPortTests
{
    [Fact]
    public void ExactJavaInflaterAndScriptValidationAgreeAtEveryStreamBoundary()
    {
        using var fixture = Fixture(); var scripts = new PlayerScripts(987613);
        foreach (var c in fixture.RootElement.GetProperty("inflation").EnumerateArray())
        {
            string? hex = c.GetProperty("hex").GetString(); byte[]? bytes = hex == null ? null : Convert.FromHexString(hex); int size = c.GetProperty("size").GetInt32();
            if (bytes != null) Assert.True(CompressUtil.InflatesTo(bytes, size) == c.GetProperty("inflates").GetBoolean(), c.GetProperty("name").GetString());
            var previous = scripts.Get(1); bool valid = c.GetProperty("valid").GetBoolean();
            Assert.True(scripts.Set(1, bytes!, size, false) == valid, c.GetProperty("name").GetString());
            if (valid) { Assert.Same(bytes, scripts.Get(1)!.CompressedBytes); Assert.Equal(size, scripts.Get(1)!.UncompressedSize); }
            else Assert.Same(previous, scripts.Get(1));
        }
    }

    [Fact]
    public void BothProductionReadersMatchJavaIncludingUnknownIdsAndDeclaredBlockSizes()
    {
        using var fixture = Fixture();
        foreach (var c in fixture.RootElement.GetProperty("edits").EnumerateArray())
        {
            var packet = Read<CM_HOUSE_EDIT>(Convert.FromHexString(c.GetProperty("bodyHex").GetString()!));
            Assert.Equal(c.GetProperty("action").GetString(), Get(packet, "action")?.ToString());
            int[] actual = [(int)Get(packet, "itemObjectId")!, BitConverter.SingleToInt32Bits((float)Get(packet, "x")!), BitConverter.SingleToInt32Bits((float)Get(packet, "y")!), BitConverter.SingleToInt32Bits((float)Get(packet, "z")!), (int)Get(packet, "rotation")!, (int)Get(packet, "buildingId")!, packet.GetRemainingBytes()];
            Assert.Equal(c.GetProperty("values").EnumerateArray().Select(v => v.GetInt32()), actual);
        }
        foreach (var c in fixture.RootElement.GetProperty("scripts").EnumerateArray())
        {
            var packet = Read<CM_HOUSE_SCRIPT>(Convert.FromHexString(c.GetProperty("bodyHex").GetString()!)); string[] fields = ["address", "scriptId", "totalSize", "compressedSize", "uncompressedSize"];
            Assert.Equal(c.GetProperty("values").EnumerateArray().Select(v => v.GetInt32()), fields.Select(f => (int)Get(packet, f)!));
            string? content = c.GetProperty("content").GetString(); Assert.Equal(content == null ? null : Convert.FromHexString(content), (byte[]?)Get(packet, "scriptContent"));
            Assert.Equal(c.GetProperty("remaining").GetInt32(), packet.GetRemainingBytes());
        }
    }

    [Fact]
    public void CommandAndContainerLookupUsesExplicitIdsAndKeepsUnknownValuesNull()
    {
        int[] ids = [1, 2, 3, 4, 5, 6, 7, 14, 15, 16];
        Assert.Equal(ids, Enum.GetValues<HousingCommand>().Select(v => v.GetId()));
        foreach (int id in ids) Assert.Equal(id, HousingCommandExtensions.GetById(id)!.Value.GetId());
        foreach (int id in new[] { int.MinValue, -1, 0, 8, 13, 17, 255, int.MaxValue }) Assert.Null(HousingCommandExtensions.GetById(id));
        Assert.Equal(HousingContainer.OBJECTS, HousingContainerExtensions.GetById(1)); Assert.Equal(HousingContainer.DECORATIONS, HousingContainerExtensions.GetById(2));
        foreach (int id in new[] { -1, 0, 3, int.MaxValue }) Assert.Null(HousingContainerExtensions.GetById(id));
    }

    [Fact]
    public void LandUsesTheExactAddressZoneAndStudioInstanceButIgnoresHeight()
    {
        using var s = new Scope(); Assert.True(s.House.IsOnLand(s.Player));
        s.Player.GetPosition().SetXYZH(null, null, 100000, null); Assert.True(s.House.IsOnLand(s.Player));
        s.Player.GetPosition().SetXYZH(s.X + 20, null, null, null); Assert.False(s.House.IsOnLand(s.Player));
        Assert.False(s.House.IsInsideLand(float.NaN, s.Y)); Assert.False(s.House.IsInsideLand(s.X + 20, s.Y));
        s.Player.GetPosition().SetXYZH(s.X, null, s.Z, null);
        s.House.SetPosition(s.Position(2)); Assert.True(s.House.IsOnLand(s.Player)); // Non-studio address is shared across instances.
        s.Address.exitMap = 210010000; Assert.False(s.House.IsOnLand(s.Player));
        s.House.SetPosition(s.Position(1)); Assert.True(s.House.IsOnLand(s.Player));
        Set(s.House.GetPosition(), "_isSpawned", false); Assert.False(s.House.IsOnLand(s.Player));
        Set(s.House.GetPosition(), "_isSpawned", true); s.Address.map++; Assert.False(s.House.IsOnLand(s.Player));
        s.Address.map--; s.Address.id++; Assert.False(s.House.IsInsideLand(s.X, s.Y));
    }

    [Theory]
    [InlineData(0, true)] [InlineData(39.99f, true)] [InlineData(40, false)] [InlineData(40.01f, false)]
    public void DoorRangeUsesTheHouseCenterAndStrictFortyMeterBoundary(float distance, bool expected)
    {
        using var s = new Scope(); var packet = Read<CM_HOUSE_OPEN_DOOR>(new byte[5]); s.Player.GetPosition().SetXYZH(s.X + distance, null, null, null);
        bool actual = (bool)Invoke(packet, "IsAtHouse", s.Player, s.House)!; Assert.Equal(expected, actual);
        s.Address.exitMap = 210010000; s.House.SetPosition(s.Position(2)); Assert.True((bool)Invoke(packet, "IsAtHouse", s.Player, s.House)!);
        s.Address.map++; Assert.False((bool)Invoke(packet, "IsAtHouse", s.Player, s.House)!);
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(14)] [InlineData(16)]
    public void EditingOutsideTheOwnLandHasNoSideEffects(int action)
    {
        using var s = new Scope(); s.Player.GetPosition().SetXYZH(s.X + 100, null, null, null); s.Clear();
        var packet = s.Edit(action, s.Chair.GetObjectId()); Run(packet);
        Assert.Empty(s.Packets); Assert.Equal(0, s.Chair.Spawns); Assert.Same(s.Chair, s.Registry.GetObjectByObjId(s.Chair.GetObjectId()));
        Assert.Same(s.Building, s.House.GetBuilding());
    }

    [Theory]
    [InlineData(2)] [InlineData(15)]
    public void EndingEitherEditModeIsAllowedAfterLeavingTheLand(int action)
    {
        using var s = new Scope(); s.Player.GetPosition().SetXYZH(s.X + 100, null, null, null); s.Clear(); Run(s.Edit(action));
        Assert.Equal((HousingCommand)action, Get(Assert.IsType<SM_HOUSE_EDIT>(Assert.Single(s.Packets)), "action"));
    }

    [Fact]
    public void StartingEditSendsBothContainersAndUnknownCommandsOrMissingHousesDoNothing()
    {
        using var s = new Scope(); s.Clear(); Run(s.Edit(1)); Assert.Equal(3, s.Packets.Length);
        Assert.Equal(new[] { HousingContainer.OBJECTS, HousingContainer.DECORATIONS }, s.Packets.OfType<SM_HOUSE_REGISTRY>().Select(p => (HousingContainer)Get(p, "container")!));
        s.Clear(); Run(s.Edit(255)); Assert.Empty(s.Packets);
        Set(s.Player, "houses", new List<House>()); Run(s.Edit(1)); Assert.Empty(s.Packets);
    }

    [Theory]
    [InlineData(5)] [InlineData(6)]
    public void ObjectsCannotBePlacedBeyondTheLandEvenWhenTheOwnerIsInsideIt(int action)
    {
        using var s = new Scope(); s.Clear(); Run(s.Edit(action, s.Chair.GetObjectId(), s.X + 100));
        Assert.Empty(s.Packets); Assert.Equal(0, s.Chair.Spawns); Assert.False(s.Chair.IsSpawnedByPlayer());
    }

    [Fact]
    public void ValidPlacementUpdatesCoordinatesAndRetainsItsPacketsAndQuestCallback()
    {
        using var s = new Scope(); s.Clear(); Run(s.Edit(5, s.Chair.GetObjectId(), s.X));
        Assert.Equal(1, s.Chair.Spawns); Assert.Equal(s.X, s.Chair.GetX());
        Assert.Equal(new[] { HousingCommand.PUT_OBJECT, HousingCommand.REMOVE_ITEM }, s.Packets.OfType<SM_HOUSE_EDIT>().Select(p => (HousingCommand)Get(p, "action")!));
    }

    [Fact]
    public void ForeignBuildingIdsCannotReachTheRenovationCouponOrChangeTheBuilding()
    {
        using var s = new Scope(); var coupon = new Item(987621, DataManager.ITEM_DATA.GetItemTemplate(s.Player.GetRace() == Aion.GameServer.Model.Race.ELYOS ? 169661003 : 169661007)); coupon.SetItemCount(1); s.Player.GetInventory().OnLoadHandler(coupon);
        s.Clear(); var packet = s.Edit(16); Set(packet, "buildingId", s.Building.GetId() + 1); Run(packet);
        Assert.Same(s.Building, s.House.GetBuilding()); Assert.Equal(1, coupon.GetItemCount()); Assert.Empty(s.Packets);
    }

    [Fact]
    public void DecorationAndScriptsRefuseOffLandMissingObjectsAndMalformedBlocks()
    {
        using var s = new Scope(); var decor = s.Attach(Read<CM_HOUSE_DECORATE>(new byte[10])); Set(decor, "objectId", 999999); Set(decor, "lineNo", 8);
        Run(decor); Assert.Empty(s.Packets);
        s.Player.GetPosition().SetXYZH(s.X + 100, null, null, null); Run(decor); Assert.Empty(s.Packets);
        var packet = s.Attach(Read<CM_HOUSE_SCRIPT>(new byte[7])); Set(packet, "address", s.Address.id); Run(packet); Assert.Empty(s.Packets);
        s.Player.GetPosition().SetXYZH(s.X, null, null, null); Set(packet, "totalSize", 8); Set(packet, "scriptContent", null); Run(packet); Assert.Empty(s.Packets);
        Set(packet, "totalSize", 3073); Run(packet); Assert.Equal(SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_SCRIPT_OVERFLOW().GetId(), Assert.IsType<SM_SYSTEM_MESSAGE>(Assert.Single(s.Packets)).GetId());
    }

    [Theory]
    [InlineData(long.MinValue)] [InlineData(-1)] [InlineData(0)]
    public void NonpositiveAuctionPricesAreRefusedBeforeTheAuctionService(long price)
    {
        using var s = new Scope(); var packet = s.Attach(Read<CM_REGISTER_HOUSE>(new byte[16])); Set(packet, "bidKinah", price); Run(packet); Assert.Empty(s.Packets);
    }

    [Theory]
    [InlineData(null)] [InlineData(0)] [InlineData(2)] [InlineData(int.MaxValue)]
    public void HouseDyeConsumesOnceAndUsesJavaSignedExpiryArithmetic(int? minutes)
    {
        using var s = new Scope(); var item = new Item(987620, DataManager.ITEM_DATA.GetItemTemplate(162000052)); item.SetItemCount(1); s.Player.GetInventory().OnLoadHandler(item);
        var action = new DyeAction { color = "123456" }; Set(action, "minutes", minutes); s.Clear(); action.Act(s.Player, item, null!, s.Chair);
        Assert.Equal(0, item.GetItemCount()); Assert.Equal(0x123456, s.Chair.GetColor()); Assert.Equal(1, s.Chair.Spawns);
        Assert.Equal(minutes == null ? 0 : unchecked((int)(s.Common.Now / 1000 + unchecked(minutes.Value * 60))), s.Chair.GetColorExpireEnd());
        Assert.Equal(new[] { HousingCommand.WITHDRAW_OBJECT, HousingCommand.PUT_OBJECT }, s.Packets.OfType<SM_HOUSE_EDIT>().Select(p => (HousingCommand)Get(p, "action")!));
    }

    [Fact]
    public void BothCompleteProductionWritersMatchJavaIncludingSignedDyeExpiry()
    {
        using var s = new Scope(); using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(PvpExpPortTests.Root(), "parity-artifacts/golden/formulas/HousingPackets.46e8d00b9.json")));
        Set(s.Building, "partsByType", new Dictionary<PartType, int> { [PartType.DOOR] = 111, [PartType.ROOF] = 222 }); s.Registry.PutDecor(new HouseDecoration(987615, 3521001), false);
        var player = PvpExpPortTests.BarePlayer(810000, 65); Set(player, "houses", new List<House> { s.House }); var con = new PvpExpPortTests.Connection(); con.SetActivePlayer(player);
        s.Chair.SetExpireTime(unchecked((int)(s.Common.Now / 1000) + 65));
        foreach (var c in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var color = c.GetProperty("color"); s.Chair.SetColor(color.ValueKind == JsonValueKind.Null ? null : color.GetInt32()); s.Chair.SetColorExpireEnd(c.GetProperty("expiry").GetInt32());
            int command = c.GetProperty("command").GetInt32(); int obj = c.GetProperty("objectId").GetInt32();
            AionServerPacket packet = c.GetProperty("kind").GetString() == "registry" ? new SM_HOUSE_REGISTRY((HousingContainer)command)
                : command == 5 ? new SM_HOUSE_EDIT(HousingCommand.PUT_OBJECT, obj, 1.25f, -2.5f, 3.75f, 65535)
                : command is 3 or 4 ? new SM_HOUSE_EDIT((HousingCommand)command, (HousingContainer)c.GetProperty("container").GetInt32(), obj)
                : command == 7 ? new SM_HOUSE_EDIT(HousingCommand.WITHDRAW_OBJECT, obj) : new SM_HOUSE_EDIT((HousingCommand)command);
            var buffer = ByteBuffer.Allocate(8192).Order(ByteOrder.LITTLE_ENDIAN); packet.SetBuf(buffer); Invoke(packet, "WriteImpl", con); var body = new byte[buffer.Position()]; buffer.Flip(); buffer.Get(body);
            Assert.Equal(Convert.FromHexString(c.GetProperty("payloadHex").GetString()!), body);
        }
    }

    [SkippableFact]
    public void ActualMigrationAndDaoPreserveUtf16BytesAndOpaqueCompressedBlocks()
    {
        string? testConnection = Environment.GetEnvironmentVariable("AION_HOUSING_PORT_TEST_CONNECTION");
        Skip.If(string.IsNullOrWhiteSpace(testConnection), "Set AION_HOUSING_PORT_TEST_CONNECTION to a disposable MySQL server.");
        using var s = new Scope(); ConcurrentDictionary<int, House>? houses = null; House? previousHouse = null;
        string db = "housing_port_" + Guid.NewGuid().ToString("N"); var builder = new MySqlConnectionStringBuilder(testConnection) { Database = "" }; using var admin = new MySqlConnection(builder.ConnectionString); admin.Open();
        try
        {
            Execute(admin, "CREATE DATABASE `" + db + "`"); builder.Database = db; using var connection = new MySqlConnection(builder.ConnectionString); connection.Open();
            string ddl = File.ReadAllText(Path.Combine(PvpExpPortTests.Root(), "game-server/sql/aion_gs.sql"));
            string Table(string name) { string text = Regex.Match(ddl, "CREATE TABLE `" + name + @"` \([\s\S]*?\n\)[^;]*;").Value; Assert.NotEmpty(text); return text; }
            Execute(connection, "CREATE TABLE player_skills(skill_id int,skill_level int)"); Execute(connection, "INSERT INTO player_skills VALUES(40009,300),(30000,7)");
            Execute(connection, "CREATE TABLE players(id int,reposte_energy bigint)"); Execute(connection, "CREATE TABLE player_recipes(recipe_id int)"); Execute(connection, "CREATE TABLE player_appearance(jaw_height int NOT NULL,chin_jut int NOT NULL)");
            Execute(connection, Table("houses")); string scriptTable = Table("house_scripts");
            Execute(connection, scriptTable.Replace("`script` blob NOT NULL,", "`script` mediumtext NOT NULL,").Replace("  `uncompressed_size` int NOT NULL,\n", "").Replace("  `uncompressed_size` int NOT NULL,\r\n", ""));
            Execute(connection, "INSERT INTO houses(id,address,building_id,player_id,sign_notice) VALUES(" + s.House.GetObjectId() + "," + s.Address.id + ",7100," + s.Player.GetObjectId() + ",'')");
            string text = "<script>Ω😀\r\n  </script>";
            using (var seed = new MySqlCommand("INSERT INTO house_scripts(house_id,script_id,script) VALUES(@house,0,@script),(@house,1,'')", connection)) { seed.Parameters.AddWithValue("@house", s.House.GetObjectId()); seed.Parameters.AddWithValue("@script", text); seed.ExecuteNonQuery(); }
            Execute(connection, File.ReadAllText(Path.Combine(PvpExpPortTests.Root(), "game-server/sql/update.sql")));
            using (var query = new MySqlCommand("SELECT script,uncompressed_size FROM house_scripts WHERE script_id=0", connection))
            {
                using var reader = query.ExecuteReader(); Assert.True(reader.Read()); byte[] compressed = (byte[])reader[0]; Assert.Equal(Encoding.Unicode.GetByteCount(text), reader.GetInt32(1));
                using var input = new MemoryStream(compressed); using var zlib = new ZLibStream(input, CompressionMode.Decompress); using var plain = new MemoryStream(); zlib.CopyTo(plain); Assert.Equal(Encoding.Unicode.GetBytes(text), plain.ToArray());
                Assert.True(CompressUtil.InflatesTo(compressed, reader.GetInt32(1)));
            }
            using var source = new MySqlDataSourceBuilder(builder.ConnectionString).Build(); var slot = typeof(DatabaseFactory).GetField("_dataSource", BindingFlags.Static | BindingFlags.NonPublic)!; object? previous = slot.GetValue(null); slot.SetValue(null, source);
            try
            {
                // The real singleton bootstrap reads houses and used player IDs from this owned database.
                houses = (ConcurrentDictionary<int, House>)Get(HousingService.GetInstance(), "customHouses")!;
                houses.TryGetValue(s.Address.id, out previousHouse); houses[s.Address.id] = s.House;
                var restored = HouseScriptsDAO.GetPlayerScripts(s.House.GetObjectId()); Assert.True(restored.Get(0)!.HasData()); Assert.False(restored.Get(1)!.HasData());
                byte[] data = [255, 0, 216]; byte[] compressed = CompressUtil.Compress(data); Assert.True(s.House.GetPlayerScripts().Set(2, compressed, data.Length));
                Assert.Equal(compressed, HouseScriptsDAO.GetPlayerScripts(s.House.GetObjectId()).Get(2)!.CompressedBytes);
                s.Player.GetPosition().SetXYZH(s.X + 100, null, null, null);
                var remove = s.Attach(Read<CM_HOUSE_SCRIPT>(new byte[7])); Set(remove, "address", s.Address.id); Set(remove, "scriptId", 2); Run(remove);
                Assert.Equal(compressed, HouseScriptsDAO.GetPlayerScripts(s.House.GetObjectId()).Get(2)!.CompressedBytes);
                s.Player.GetPosition().SetXYZH(s.X, null, null, null);
                using (var read = new MySqlCommand("SELECT script,uncompressed_size FROM house_scripts WHERE script_id=2", connection)) { using var r = read.ExecuteReader(); Assert.True(r.Read()); Assert.Equal(compressed, (byte[])r[0]); Assert.Equal(3, r.GetInt32(1)); }
                byte[] replacement = CompressUtil.Compress([1, 2, 3, 4]); Assert.True(s.House.GetPlayerScripts().Set(2, replacement, 4));
                HouseScriptsDAO.StoreScript(s.House.GetObjectId(), 3, [1, 2, 3], 5); HouseScriptsDAO.StoreScript(s.House.GetObjectId(), 4, compressed, 3);
                var loaded = HouseScriptsDAO.GetPlayerScripts(s.House.GetObjectId()); Assert.Equal(replacement, loaded.Get(2)!.CompressedBytes); Assert.False(loaded.Get(3)!.HasData()); Assert.Equal(compressed, loaded.Get(4)!.CompressedBytes);
                HouseScriptsDAO.DeleteScript(s.House.GetObjectId(), 2); Assert.False(HouseScriptsDAO.GetPlayerScripts(s.House.GetObjectId()).Get(2)!.HasData()); HouseScriptsDAO.DeleteScriptsForHouse(s.House.GetObjectId());
                Execute(connection, "DROP TABLE house_scripts"); Execute(connection, scriptTable); Assert.True(s.House.GetPlayerScripts().Set(7, compressed, 3)); Assert.Equal(compressed, HouseScriptsDAO.GetPlayerScripts(s.House.GetObjectId()).Get(7)!.CompressedBytes);
            }
            finally { slot.SetValue(null, previous); }
        }
        finally
        {
            Execute(admin, "DROP DATABASE IF EXISTS `" + db + "`");
            if (houses != null) { if (previousHouse == null) houses.TryRemove(s.Address.id, out _); else houses[s.Address.id] = previousHouse; }
        }
    }

    private static void Execute(MySqlConnection connection, string sql) { using var command = new MySqlCommand(sql, connection); command.ExecuteNonQuery(); }

    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(PvpExpPortTests.Root(), "parity-artifacts/golden/formulas/Housing.46e8d00b9.json")));
    private static object? Get(object obj, string name) => Field(obj.GetType(), name).GetValue(obj);
    private static void Set(object obj, string name, object? value) => Field(obj.GetType(), name).SetValue(obj, value);
    private static FieldInfo Field(Type type, string name) { for (Type? t = type; t != null; t = t.BaseType) { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly); if (f != null) return f; } throw new MissingFieldException(type.Name, name); }
    private static object? Invoke(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(obj, args);
    private static void Run(AionClientPacket packet) => Invoke(packet, "RunImpl");
    private static T Read<T>(byte[] body) where T : AionClientPacket
    {
        var packet = (T)Activator.CreateInstance(typeof(T), 0, new HashSet<AionConnection.State>())!; packet.SetConnection(new PvpExpPortTests.Connection());
        packet.SetBuffer(ByteBuffer.Wrap(body).Order(ByteOrder.LITTLE_ENDIAN)); Assert.True(packet.Read()); return packet;
    }

    private sealed class RecordingChair(HouseRegistry registry, int id) : ChairObject(registry, id, 3520001)
    {
        internal int Spawns;
        public override void Spawn() => Spawns++;
    }

    private sealed class Scope : IDisposable
    {
        internal PvpExpPortTests.Scope Common { get; } = new(); internal Player Player { get; }
        internal House House { get; } internal HouseAddress Address { get; } internal Building Building { get; }
        internal HouseRegistry Registry => House.GetRegistry(); internal RecordingChair Chair { get; }
        internal float X { get; } internal float Y { get; } internal float Z { get; }
        internal AionServerPacket[] Packets => ((PvpExpPortTests.Connection)Player.GetClientConnection()).Packets;
        internal Scope()
        {
            Common.Now = 1792410000123; Player = Common.Player(65); X = Player.GetX(); Y = Player.GetY(); Z = Player.GetZ();
            Set(Common.H.Data.StaticData, "<HouseDataDh>k__BackingField", new HouseData());
            Building = new Building { id = 7100, size = HouseType.HOUSE, type = BuildingType.PERSONAL_FIELD }; Set(Building, "partsByType", new Dictionary<PartType, int>());
            Address = new HouseAddress { id = 98761, map = Player.GetWorldId(), x = X, y = Y, z = Z };
            var land = new HousingLand { id = 9876, buildings = [Building], addresses = [Address] }; Address.AfterUnmarshal(land);
            House = new House(987613, Building, Address, 1); House.SetPosition(Position(1)); Set(House, "ownerId", Player.GetObjectId()); Set(House, "houseRegistry", new HouseRegistry(House)); Set(House, "playerScripts", new PlayerScripts(House.GetObjectId())); Set(Player, "houses", new List<House> { House });
            var zones = new ZoneData { zoneList = [new ZoneTemplate { XmlName = "HOUSE_" + Address.id, Mapid = Address.map, AreaType = AreaType.Cylinder, Cylinder = new Cylinder(X, Y, 20, Z + 10, Z - 10) }] }; zones.AfterUnmarshal(null!); Set(Common.H.Data.StaticData, "<ZoneInfo>k__BackingField", zones);
            var objects = new HousingObjectData { housingObjects = [new HousingChair { id = 3520001, canDye = true }] }; objects.AfterUnmarshal(null!); Set(Common.H.Data.StaticData, "<HousingObjectDataDh>k__BackingField", objects);
            using var xml = new StringReader("<item_templates><item_template id='182400001' name='Kinah' level='1' quality='COMMON' max_stack_count='2147483647'/><item_template id='162000052' name='Dye' level='1' quality='COMMON' max_stack_count='10'/><item_template id='169661003' name='Coupon' level='1' quality='COMMON' max_stack_count='10'/><item_template id='169661007' name='Coupon' level='1' quality='COMMON' max_stack_count='10'/></item_templates>"); var items = (ItemData)new XmlSerializer(typeof(ItemData)).Deserialize(xml)!; items.AfterUnmarshal(null!); Set(Common.H.Data.StaticData, "<ItemDataDh>k__BackingField", items);
            Chair = new RecordingChair(Registry, 987614); Registry.PutObject(Chair, false); Clear();
        }
        internal WorldPosition Position(int instance)
        {
            var parent = (WorldMapInstance)RuntimeHelpers.GetUninitializedObject(Player.GetPosition().GetMapRegion()!.GetParent().GetType()); Set(parent, "instanceId", instance);
            var result = new WorldPosition(Player.GetWorldId(), X, Y, Z, 0, new MapRegion(0, parent, [])); Set(result, "_isSpawned", true); return result;
        }
        internal T Attach<T>(T packet) where T : AionClientPacket { packet.SetConnection(Player.GetClientConnection()); return packet; }
        internal CM_HOUSE_EDIT Edit(int action, int obj = 0, float? x = null)
        {
            var packet = Attach(Read<CM_HOUSE_EDIT>(EditBody(action))); Set(packet, "itemObjectId", obj); Set(packet, "x", x ?? X); Set(packet, "y", Y); Set(packet, "z", Z); return packet;
        }
        private static byte[] EditBody(int action) { int size = action is 5 or 6 ? 19 : action is 3 or 4 or 7 or 16 ? 5 : 1; var result = new byte[size]; result[0] = (byte)action; return result; }
        internal void Clear() => ((PvpExpPortTests.Connection)Player.GetClientConnection()).Clear();
        public void Dispose() => Common.Dispose();
    }
}
