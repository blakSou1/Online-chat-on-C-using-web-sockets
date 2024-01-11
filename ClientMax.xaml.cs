using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace messengerClient
{

    public partial class ClientMax : Window
    {
        private ClientWebSocket _clientWebSocket;//создаем вебсокет клиента 
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly Uri _uri = new Uri("ws://localhost:8080");//адрес сервера

        public ClientMax()
        {
            InitializeComponent();
            Main();
        }

        public async void Main()
        {
            await ConnectedToServer();
            Closing += MainWindow_Closing;
        }

        private async Task ConnectedToServer()
        {
            _clientWebSocket = new ClientWebSocket();//создаем вебсокет клиента
            try
            {
                await _clientWebSocket.ConnectAsync(_uri, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show($"Ошибка подключения:{ex:Message}");
            }
            _ = ReceiveMessages();
        }

        private async Task ReceiveMessages()
        {
            try
            {

                if (_clientWebSocket != null)
                {
                    var bufer = new byte[4096];

                    while (_clientWebSocket.State == WebSocketState.Open)
                    {
                        WebSocketReceiveResult receiveResult = await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(bufer), CancellationToken.None);

                        if (receiveResult.MessageType == WebSocketMessageType.Text)
                        {
                            string message = Encoding.UTF8.GetString(bufer, 0, receiveResult.Count);

                            if (message != null)
                            {
                                //обработка сообщений от сервера
                                if (message.StartsWith("NewMessage"))
                                {
                                    string mes = message.Substring(10);
                                    NewMessage("klient", mes);
                                }
                                else
                                {
                                    _ = MessageBox.Show("ошибка принято не коректное сообщение от сервера");
                                }

                            }
                            else
                            {
                                _ = MessageBox.Show("Ошибка приема сообщений от сервера формат не текстовый");
                            }

                        }
                        else
                        {
                            _ = MessageBox.Show("Ошибка приема сообщений от сервера");
                        }

                        bufer = new byte[4096];
                    }

                }
                else
                {
                    _ = MessageBox.Show($"клиентский веб соккет не инициализирован какого черта!?");
                }

            }
            catch (Exception ex)
            {
                _ = MessageBox.Show($"Ошибка в блоке приема сообщений is моем коде:{ex.Message}");
            }

        }

        private async void SendData(string message)
        {
            try
            {
                string messages = "NewMessage " + message;
                byte[] messageByte = Encoding.UTF8.GetBytes(messages);
                await _clientWebSocket.SendAsync(new ArraySegment<byte>(messageByte), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception)
            {
                _ = MessageBox.Show($"Ошибка при отправке сообщений");
            }

        }//отправка сообщений на сервер

        private void NewMessage(string nameUser, string mess)
        {
            TextBlock naimU = new TextBlock();
            TextBlock mes = new TextBlock();
            StackPanel messag = new StackPanel();
            naimU.FontWeight = FontWeights.UltraBold;
            naimU.Text = nameUser;
            naimU.FontSize = 10;

            mes.TextWrapping = System.Windows.TextWrapping.Wrap;
            mes.Text = mess;
            mes.HorizontalAlignment = HorizontalAlignment.Stretch;
            mes.FontSize = 20;

            _ = messag.Children.Add(naimU);
            _ = messag.Children.Add(mes);

            _ = chatWindow.Children.Add(messag);
        }//вывод сообщений в чат

        private void Enter_Click(object sender, RoutedEventArgs e)
        {
            string mes = messageInput.Text;
            messageInput.Clear();

            SendData(mes);
            NewMessage("вы", mes);
        }//нажатие отправки сообщений

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SendData("Closed");
            Closing -= MainWindow_Closing;
        }

    }

}
