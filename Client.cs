using Godot;
using System;
using Newtonsoft.Json;
using Godot.Collections;
public partial class Client : Node
{

	WebSocketMultiplayerPeer peer = new();
	WebRtcMultiplayerPeer rtcPeer = new();
	[Export]
	public string ServerURL = "ws://127.0.0.1:8916";
	private Lobby lobby;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Multiplayer.ConnectedToServer += RTCConnectedToServer;
		Multiplayer.PeerConnected += RTCPeerConnected;
		Multiplayer.PeerDisconnected += RTCPeerDisconnected;
	}

    private void RTCPeerConnected(long id)
    {
        GD.Print("Peer Connected! " + id.ToString());
    }

	private void RTCPeerDisconnected(long id)
    {
		GD.Print("Peer Disconnected! " + id.ToString());

    }


    private void RTCConnectedToServer()
    {
		GD.Print("Server Connected ");

    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
	{
		peer.Poll();
		if(peer.GetAvailablePacketCount() > 0){
			byte[] packet = peer.GetPacket();
			if(packet != null){
				string dataString = packet.GetStringFromAscii();
				GD.Print(dataString);
				ServerData data = JsonConvert.DeserializeObject<ServerData>(dataString);
				
				if(data.Type == MessageType.ID){
					ClientIDData clientIDData = JsonConvert.DeserializeObject<ClientIDData>(dataString);
					GD.Print("my id is: " + clientIDData.ID.ToString());
					//sets up multiplayer server/client
					rtcPeer.CreateMesh((int)clientIDData.ID);
					Multiplayer.MultiplayerPeer = rtcPeer;
				}

				if(data.Type == MessageType.Lobby){
					LobbyData lobbyData = JsonConvert.DeserializeObject<LobbyData>(dataString);
					GD.Print(lobbyData);
					lobby = lobbyData.Lobby;
					GameManager.Players = lobby.Players;
				}

				if(data.Type == MessageType.UserConnected){
					ClientIDData clientIDData = JsonConvert.DeserializeObject<ClientIDData>(dataString);
					createPeer((int)clientIDData.ID);
				}

				if(data.Type == MessageType.Candidate){
					IceData iceData = JsonConvert.DeserializeObject<IceData>(dataString);
					if(rtcPeer.HasPeer(iceData.OrgID)){
						WebRtcPeerConnection connection = (WebRtcPeerConnection)rtcPeer.GetPeer(iceData.OrgID)["connection"];
						connection.AddIceCandidate(iceData.Media, (int)iceData.index, iceData.SdpName);
					}
				}

				if(data.Type == MessageType.Offer || data.Type == MessageType.Answer){
					OfferData offerData = JsonConvert.DeserializeObject<OfferData>(dataString);
					if(rtcPeer.HasPeer(offerData.OrgID)){
						WebRtcPeerConnection connection = (WebRtcPeerConnection)rtcPeer.GetPeer(offerData.OrgID)["connection"];
						connection.SetRemoteDescription(data.Type == MessageType.Offer ? "offer" : "answer", offerData.Data);
					}
				}
			}
		}
	}

	private void createPeer(int id){
		if(id != peer.GetUniqueId()){
			WebRtcPeerConnection connection = new WebRtcPeerConnection();
			
			Godot.Collections.Dictionary config = new Godot.Collections.Dictionary {
				["iceServers"] = new Godot.Collections.Array {
					new Godot.Collections.Dictionary {
					["urls"] = new Godot.Collections.Array {
							"stun:stun.l.google.com:19302"
						}
					}
				}
			};
			
			connection.Initialize(config);

			connection.SessionDescriptionCreated += (type, sdp) => offerCreated(type, sdp, id);
			connection.IceCandidateCreated += (media, index, name) => iceCandidateCreated(media, index, name, id);
			rtcPeer.AddPeer(connection, id);

			if(id < peer.GetUniqueId()){
				connection.CreateOffer();
			}
		}
	}

    private void iceCandidateCreated(string media, long index, string name, int id)
    {
       IceData data = new IceData(){
		ID = id,
		OrgID = peer.GetUniqueId(),
		Type = MessageType.Candidate,
		Media = media,
		index = index,
		SdpName = name,
		Lobby = lobby.LobbyValue
	   };

	   string json = JsonConvert.SerializeObject(data);
		byte[] bytes = json.ToAsciiBuffer();

		peer.PutPacket(bytes);
    }


    private void offerCreated(string type, string sdp, int id)
    {
        if(!rtcPeer.HasPeer(id)){
			return;
		}
		WebRtcPeerConnection connection = (WebRtcPeerConnection)rtcPeer.GetPeer(id)["connection"];
		connection.SetLocalDescription(type, sdp);
		if(type == "offer"){
			sendOffer(id, sdp);
		}else{
			sendAnswer(id, sdp);
		}
    }	

	private void sendOffer(int id, string sdp){
		OfferData data = new OfferData(){
			ID = id,
			Data = sdp,
			Type = MessageType.Offer,
			OrgID = peer.GetUniqueId(),
			Lobby = lobby.LobbyValue
		};
		
		string json = JsonConvert.SerializeObject(data);
		byte[] bytes = json.ToAsciiBuffer();

		peer.PutPacket(bytes);
	}

	private void sendAnswer(int id, string sdp){
		OfferData data = new OfferData(){
			ID = id,
			Data = sdp,
			Type = MessageType.Answer,
			OrgID = peer.GetUniqueId(),
			Lobby = lobby.LobbyValue
		};
		
		string json = JsonConvert.SerializeObject(data);
		byte[] bytes = json.ToAsciiBuffer();

		peer.PutPacket(bytes);
	}

    private void _on_client_join_button_down(){
		peer.CreateClient(ServerURL);
		GD.Print("Started Client on " + ServerURL);
	}

	private void _on_client_send_test_data_button_down(){
		JoinLobbyData data = new JoinLobbyData(){
			Type = MessageType.Lobby,
			LobbyValue = GetNode<LineEdit>("LineEdit").Text,
			Id = peer.GetUniqueId()
		};

		string json = JsonConvert.SerializeObject(data);
		byte[] bytes = json.ToAsciiBuffer();

		peer.PutPacket(bytes);
	}

	private void _on_button_button_down(){
		Rpc("startGame");
	}
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void startGame(){
		
		Node2D scene = ResourceLoader.Load<PackedScene>("res://LAN MultiplayerTutorial/TestScene.tscn").Instantiate<Node2D>();
		GetTree().Root.AddChild(scene);
	}
}
