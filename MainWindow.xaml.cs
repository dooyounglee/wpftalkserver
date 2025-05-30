﻿using Microsoft.Win32;
using OTILib.DB;
using OTILib.Events;
using OTILib.Handlers;
using OTILib.Models;
using OTILib.Sockets;
using OTILib.Util;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
using talkLib.Util;
using static System.Net.Mime.MediaTypeNames;

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

        #region Column0
        private void ReLoadConnList()
        {
            lbxConn.Items.Clear();
            var list = _roomManager.RoomHandlersDict()[0];
            foreach (ClientHandler ch in list)
            {
                lbxConn.Items.Add(ch.InitialData.UsrNo + "," + ch.ConnState);
                // lbxConn.Items.Add(ch.UsrNo + "," + ch.ConnState);
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

        #region view

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
        #endregion view

        private void Connected(object? sender, ChatEventArgs e)
        {
            e.ClientHandler.UsrNo = e.Hub.UsrNo;
            if (e.Hub.RoomId == 0) // 접속
            {
                // 이미 접속상태면
                if (_roomManager.IsConnect(e.ClientHandler))
                {
                    // '로그인 실패' 다시 client로 돌려줘기
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
                    hub.Data1 = _roomManager.ClientStates();
                    _roomManager.SendToMyRoom(hub);
                    
                    // 서버 화면 표시
                    ReLoadConnList();
                }
            }
            else // 채팅
            {
                _roomManager.Add(e.ClientHandler);
                e.ClientHandler.ChangeConnState(ConnState.Online);
                var hub = CreateNewStateChatHub(e.Hub, ChatState.Connect);
                _roomManager.SendToMyRoom(hub);

                lbxClients.Items.Add(e.Hub);
                AddClientMessageList(hub);
            }
        }

        private void Disconnected(object? sender, ChatEventArgs e)
        {
            if (e is null || e.Hub is null) return;

            if (e.Hub.RoomId == 0) // 접속
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
            else // 채팅
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
            if (e.Hub.RoomId == 0) // 접속
            {
                if (e.Hub.State == ChatState.StateChange)
                {
                    ClientHandler handler = _roomManager.getClientHandler(0, e.Hub.UsrNo);
                    handler.ChangeConnState(e.Hub.connState);
                    e.Hub.Data1 = _roomManager.ClientStates();
                    _roomManager.SendToMyRoom(e.Hub);
                    ReLoadConnList();
                }
                if (e.Hub.State == ChatState.ChatReload)
                {
                    _roomManager.SendToMyRoom(new ChatHub()
                    {
                        RoomId = 0,
                        State = ChatState.ChatReload,
                    });
                }
                else
                {
                    _roomManager.SendToMyRoom(e.Hub);
                    ReLoadConnList();
                }
            }
            else // 채팅
            {
                if (e.Hub.State == ChatState.File)
                {
                    if (!System.IO.Directory.Exists(e.Hub.Data[1].ToString()))
                    {
                        System.IO.Directory.CreateDirectory(e.Hub.Data[1].ToString());
                    }
                    System.IO.File.WriteAllBytes(e.Hub.Data[1].ToString() + e.Hub.Data[2].ToString(), e.Hub.Data2);

                }
                
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (_roomManager == null) return;
            _roomManager.printDictionary();
        }
        #endregion Column0

        #region Column1
        private HttpListener httpListener;
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (httpListener == null)
            {
                httpListener = new HttpListener();
                httpListener.Prefixes.Add(string.Format("http://localhost:8686/"));
                serverStart();
            }
        }

        private void serverStart()
        {
            if (!httpListener.IsListening)
            {
                httpListener.Start();
                Debug.WriteLine("server running...");

                Task.Factory.StartNew(() =>
                {
                    while (httpListener != null)
                    {
                        HttpListenerContext context = this.httpListener.GetContext();

                        string rawurl = context.Request.RawUrl;
                        string httpmethod = context.Request.HttpMethod;

                        // string result = "";

                        // result += string.Format("httpmethod = {0}\r\n", httpmethod);
                        // result += string.Format("rawurl = {0}\r\n", rawurl);

                        Debug.WriteLine(httpmethod + " " + rawurl);
                        // lbxRequest.Items.Add(httpmethod + " " + rawurl);

                        // FileInfo fileInfo = new FileInfo("D:\\Autodown\\capture\\20220425_010943.png");
                        // byte[] output = File.ReadAllBytes(fileInfo.FullName);
                        // context.Response.OutputStream.Write(output, 0, output.Length);
                        // context.Response.ContentType = "image/png";
                        // context.Response.StatusCode = 201;
                        // context.Response.Close();

                        // if ("/image".Equals(rawurl))
                        // {
                        //     FileInfo fileInfo = new FileInfo("D:/temp/source.png");
                        //     byte[] output = System.IO.File.ReadAllBytes(fileInfo.FullName);
                        //     context.Response.OutputStream.Write(output, 0, output.Length);
                        //     // context.Response.ContentType = "image/png";
                        //     context.Response.StatusCode = 201;
                        //     // context.Response.AddHeader("content-disposision", "attachment; filename=노네임.png;");
                        // }
                        // else if ("/file".Equals(rawurl))
                        // {
                        //     FileInfo fileInfo = new FileInfo("D:/Downloads/talkLib_20250420.zip");
                        //     byte[] output = System.IO.File.ReadAllBytes(fileInfo.FullName);
                        //     context.Response.OutputStream.Write(output, 0, output.Length);
                        //     // context.Response.ContentType = "image/png";
                        //     context.Response.StatusCode = 201;
                        //     // context.Response.AddHeader("content-disposision", "attachment; filename=노네임.png;");
                        // }
                        if ("/file".Equals(rawurl.Substring(0,5)))
                        {
                            int fileNo = Int32.Parse(rawurl.Substring(6));
                            string sql = @$"SELECT file_path, file_name
                                              FROM talk.chatfile
                                             WHERE file_no = {fileNo}";
                            DataTable? dt = Query.select1(sql);
                            string file_path = (string)dt.Rows[0]["file_path"];
                            string file_name = (string)dt.Rows[0]["file_name"];

                            FileInfo fileInfo = new FileInfo(file_path + file_name);
                            byte[] output = System.IO.File.ReadAllBytes(fileInfo.FullName);
                            context.Response.OutputStream.Write(output, 0, output.Length);
                            // context.Response.ContentType = "image/png";
                            context.Response.StatusCode = 201;
                            // context.Response.AddHeader("content-disposision", "attachment; filename=노네임.png;");
                        }
                        context.Response.Close();
                    }
                });

            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            image.Source = new BitmapImage(new Uri("http://localhost:8686/image"));
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "모든 파일|*.*";
            saveFileDialog.ShowDialog();

            if (saveFileDialog.FileName != "")
            {
                WebClient wc = new WebClient();
                wc.DownloadFile("http://localhost:8686/file", saveFileDialog.FileName);
            }
        }
        #endregion Colunm1
    }
}