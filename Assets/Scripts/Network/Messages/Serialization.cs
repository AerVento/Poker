using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Game.Network
{
    public static class Serialization
    {
        public static void WriteRoomInfo(this NetworkWriter writer, RoomInfo roomInfo)
        {
            writer.WriteString(JsonSerializer.Serialize(roomInfo));
        }

        public static RoomInfo ReadRoomInfo(this NetworkReader reader)
        {
            return JsonSerializer.Deserialize<RoomInfo>(reader.ReadString());
        }

        public static void WriteRoomList(this NetworkWriter writer, RoomList roomList)
        {
            writer.WriteString(JsonSerializer.Serialize(roomList));
        }

        public static RoomList ReadRoomList(this NetworkReader reader)
        {
            return JsonSerializer.Deserialize<RoomList>(reader.ReadString());
        }

        public static void WriteClientGameInfo(this NetworkWriter writer, ClientGameInfo info)
        {
            writer.WriteString(JsonSerializer.Serialize(info));
        }

        public static ClientGameInfo ReadClientGameInfo(this NetworkReader reader)
        {
            return JsonSerializer.Deserialize<ClientGameInfo>(reader.ReadString());
        }

        public static void WriteRoundResult(this NetworkWriter writer, RoundResult result)
        {
            writer.WriteString(JsonSerializer.Serialize(result));
        }

        public static RoundResult ReadRoundResult(this NetworkReader reader)
        {
            return JsonSerializer.Deserialize<RoundResult>(reader.ReadString());
        }

        public static void WriteGameResult(this NetworkWriter writer, GameResult result)
        {
            writer.WriteString(JsonSerializer.Serialize(result));
        }

        public static GameResult ReadGameResult(this NetworkReader reader)
        {
            return JsonSerializer.Deserialize<GameResult>(reader.ReadString());
        }
    }
}
