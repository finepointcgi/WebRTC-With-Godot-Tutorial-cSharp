using Godot;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;


public partial class Server : Node
{
	WebSocketMultiplayerPeer peer = new();
	private List<long> users = new();
	private Dictionary<string, Lobby> lobbies = new();
	[Export]
	private int hostPort = 8916;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		peer.PeerConnected += peerConnected;
		peer.PeerDisconnected += peerDisconnected;
	}

	private void peerDisconnected(long id)
	{
		GD.Print("Peer Disconnected! " + id.ToString());

	}

	private void peerConnected(long id)
	{
		GD.Print("Peer Connected! " + id.ToString());

		ClientIDData data = new ClientIDData()
		{
			Type = MessageType.ID,
			ID = id
		};
		users.Add(id);
		string json = JsonConvert.SerializeObject(data);
		byte[] bytes = json.ToAsciiBuffer();

		peer.GetPeer((int)id).PutPacket(bytes);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		peer.Poll();
		if (peer.GetAvailablePacketCount() > 0)
		{
			byte[] packet = peer.GetPacket();
			if (packet != null)
			{
				string dataString = packet.GetStringFromAscii();
				GD.Print(dataString);
				ServerData data = JsonConvert.DeserializeObject<ServerData>(dataString);

				if (data.Type == MessageType.Lobby)
				{
					JoinLobbyData joinLobbyData = JsonConvert.DeserializeObject<JoinLobbyData>(dataString);
					joinLobby(joinLobbyData);
				}

				if(data.Type == MessageType.Answer ||data.Type == MessageType.Offer ){
					OfferData offerData = JsonConvert.DeserializeObject<OfferData>(dataString);
					peer.GetPeer(offerData.ID).PutPacket(packet);
				}
				if (data.Type == MessageType.Candidate ){
					IceData iceData = JsonConvert.DeserializeObject<IceData>(dataString);
					peer.GetPeer(iceData.ID).PutPacket(packet);
				}
			}
		}
	}

	private void joinLobby(JoinLobbyData data)
	{
		if (data.LobbyValue == "")
		{
			data.LobbyValue = Guid.NewGuid().ToString();
			lobbies.Add(data.LobbyValue, new Lobby(data.Id));
			lobbies[data.LobbyValue].LobbyValue = data.LobbyValue;
		}

		lobbies[data.LobbyValue].AddPlayer(data.Id, data.Name);

		foreach (var player in lobbies[data.LobbyValue].Players)
		{
			ClientIDData clientIDData = new ClientIDData()
			{
				Type = MessageType.UserConnected,
				ID = data.Id
			};
			sendToPeer(player.Id, clientIDData);

			ClientIDData playerIDData = new ClientIDData()
			{
				Type = MessageType.UserConnected,
				ID = player.Id
			};
			sendToPeer(data.Id, playerIDData);

			sendToPeer(data.Id, new LobbyData()
			{
				Lobby = lobbies[data.LobbyValue],
				Type = MessageType.Lobby
			});


		}

		sendToPeer(data.Id, new LobbyData()
		{
			Lobby = lobbies[data.LobbyValue],
			Type = MessageType.Lobby
		});

	}

	private void sendToPeer(int id, ServerData data)
	{
		string json = JsonConvert.SerializeObject(data);
		byte[] bytes = json.ToAsciiBuffer();

		peer.GetPeer((int)id).PutPacket(bytes);
	}

	private void _on_server_host_button_down()
	{
		peer.CreateServer(hostPort);
		GD.Print("Started Server on " + hostPort.ToString());
	}


}
