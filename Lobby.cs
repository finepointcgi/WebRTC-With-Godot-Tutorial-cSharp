using System.Collections.Generic;

public class Lobby
{
    public int HostID;
    public List<PlayerInfo> Players = new();

    public string LobbyValue;

    public Lobby(){
        
    }
    public Lobby(int hostID){
        HostID = hostID;
    }
    
    public PlayerInfo AddPlayer(int id, string name){
        PlayerInfo playerInfo = new PlayerInfo(){
            Id = id,
            Name = name,
            Index = Players.Count + 1
        };
        Players.Add(playerInfo);
        return playerInfo;
    }
}