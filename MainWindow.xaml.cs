using OTILib.Events;
using OTILib.Handlers;
using OTILib.Models;
using OTILib.Sockets;
using OTILib.Util;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace talk2Server
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ChatServer _server;
        private ClientRoomManager _roomManager;

        public MainWindow()
        {
            InitializeComponent();

            //List<TodoItem> items = new List<TodoItem>();
            //items.Add(new TodoItem { Title = "Title1", Description = 45 });
            //items.Add(new TodoItem { Title = "Title2", Description = 85 });
            //items.Add(new TodoItem { Title = "Title3", Description = 0 });
            //lbTodoList.ItemsSource = items;


            btnStart.Click += BtnStart_Click;
            btnStop.Click += BtnStop_Click;
        }

        private void ReLoadConnList()
        {
            lbxConn.Items.Clear();
            var list = _roomManager.RoomHandlersDict()[0];
            foreach (ClientHandler ch in list)
            {
                lbxConn.Items.Add(ch.InitialData.UsrNm + "," + "온라인");
            }
        }

        private ChatHub CreateNewStateChatHub(ChatHub hub, ChatState state)
        {
            try
            {
                return new ChatHub
                {
                    RoomId = hub.RoomId,
                    UsrNo = hub.UsrNo,
                    UsrNm = hub.UsrNm,
                    State = state,
                    Data1 = _roomManager.ClientStates(),
                };
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private void AddClientMessageList(ChatHub hub)
        {
            string message = hub.State switch
            {
                ChatState.Connect => $"★ 접속 ★ {hub} ★",
                ChatState.Disconnect => $"★ 접속 종료 ★ {hub} ★",
                _ => $"{hub}: {hub.Message}"
            };
            lbxMsg.Items.Add(message);
        }

        private void Connected(object? sender, ChatEventArgs e)
        {
            OtiLogger.log1(e.Hub.ToJsonString());
            e.ClientHandler.UsrNo = e.Hub.UsrNo;
            OtiLogger.log1(e.ClientHandler.UsrNo);
            if (e.Hub.RoomId == 0 && _roomManager.IsConnect(e.ClientHandler))
            {
                OtiLogger.log1("같데");
                e.ClientHandler.Send(new ChatHub()
                {
                    State = ChatState.ConnectFail,
                });
            }
            else
            {
                _roomManager.Add(e.ClientHandler);
                e.ClientHandler.ChangeConnState(ConnState.Online);

                var hub = CreateNewStateChatHub(e.Hub, ChatState.Connect);

                _roomManager.SendToMyRoom(hub);

                if (e.Hub.RoomId == 0)
                {
                    ReLoadConnList();
                }
                else
                {
                    lbxClients.Items.Add(e.Hub);
                    AddClientMessageList(hub);
                }
            }
        }

        private void Disconnected(object? sender, ChatEventArgs e)
        {
            if (e is not null && e.Hub is not null && e.Hub.RoomId == 0)
            {
                _roomManager.Remove(e.ClientHandler);
                _roomManager.SendToMyRoom(new ChatHub
                {
                    RoomId = 0,
                    UsrNo = e.Hub.UsrNo,
                    State = ChatState.Disconnect,
                });
                ReLoadConnList();
            }
            else if (e is not null && e.Hub is not null && e.Hub.RoomId > 0)
            {
                var hub = CreateNewStateChatHub(e.Hub, ChatState.Disconnect);

                lbxClients.Items.Remove(e.Hub);
                AddClientMessageList(hub);

                _roomManager.Remove(e.ClientHandler);
                _roomManager.SendToMyRoom(hub);
            }

        }

        private void Received(object? sender, ChatEventArgs e)
        {
            if (e.Hub.RoomId == 0)
            {
                if (e.Hub.State == ChatState.StateChange)
                {
                    e.ClientHandler.ChangeConnState(e.Hub.connState);
                    e.Hub.Data1 = _roomManager.ClientStates();
                    _roomManager.SendToMyRoom(e.Hub);
                    ReLoadConnList();
                }
                else
                {
                    _roomManager.SendToMyRoom(e.Hub);
                    ReLoadConnList();
                }
            }
            else
            {
                _roomManager.SendToMyRoom(e.Hub);
                _roomManager.SendToMyRoom(new ChatHub()
                {
                    RoomId = 0,
                    State = ChatState.ChatReload,
                });
                AddClientMessageList(e.Hub);
            }
        }

        private void RunningStateChanged(bool isRunning)
        {
            btnStart.IsEnabled = !isRunning;
            btnStop.IsEnabled = isRunning;
        }

        private void BtnStart_Click(object? sender, EventArgs e)
        {
            _roomManager = new ClientRoomManager();
            _server = new ChatServer(IPAddress.Parse(IP.Text), Convert.ToInt16(Port.Text));
            _server.Connected += Connected;
            _server.Disconnected += Disconnected;
            _server.Received += Received;
            _server.RunningStateChanged += RunningStateChanged;

            _ = _server.StartAsync();
        }

        private void BtnStop_Click(object? sender, EventArgs e)
        {
            _server.Stop();
        }
    }
}