using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace MesengerServer
{
    public class Program
    {
        private static readonly List<WebSocket> users = new List<WebSocket>();//������ �������������
        private static readonly object consoleLock = new object();//��������� ��� ���������� ����������� �� ���� ����� � ������� ������� ��������
        private static int lineDel = 0;//���������� ������������ ��� ������ ������� �� ��������������� ��������� �������� ������
        private static HttpListener? _httpListener;
        private static CancellationTokenSource? _cancellationTokenSource;

        private static Task Main(string[] args)
        {
            int port = 8080;// ���� �������
            bool serverStartedStatus = false;
            //������ ������
            string[] command = ["/info/", "������� ��� ��������� ������ ������������ ������.", "/serverStoped/", "������� ��������� ������.", "/serverStarted/", "������� ��������� ������"];
            //��������� ������� ������ �� ������� �������� ����� ������ ������� �� �������� � �������� ����� ���� ������� � ����� ����

            Console.WriteLine("��� ������� ������� �������:/serverStarted/");//������� �������� ������� � ������� 
            Console.WriteLine("��� ��������� ������� ������� �������:/serverStoped/");

            while (true)
            {
                string? console;
                console = Console.ReadLine();//������ ���� � �������

                if (console == command[0])
                {
                    lock (consoleLock)
                    {
                        Console.WriteLine("������ ������:");
                    }

                    if (lineDel > 0)
                    {
                        lineDel--;
                    }

                    for (int i = 0; i < command.Length - 1; i += 2)
                    {
                        lock (consoleLock)
                        {
                            Console.WriteLine(command[i] + "-" + command[1 + i]);
                        }
                    }//������� ��� ������������ ������� � �� �������� � �������

                }//������� ������� ��������������� ������ �������� ������
                else if (console == command[2])
                {

                    if (lineDel > 0)
                    {
                        lineDel--;
                    }

                    if (serverStartedStatus == true)
                    {
                        lock (consoleLock)
                        {
                            Console.WriteLine("������ ���������� �� ������� �������...");
                        }
                        serverStartedStatus = false;

                        if (_cancellationTokenSource != null)
                        {
                            _cancellationTokenSource.Cancel();//�������� ������ � ������ ���������� ������� ��� ����� ��������
                        }

                        StopServer();//����� ����������� ������
                    }
                    else
                    {
                        lock (consoleLock)
                        {
                            Console.WriteLine("������ �� �������...");
                        }
                    }

                }//��������� ������
                else if (console == command[4])
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    CancellationToken token = _cancellationTokenSource.Token;//��������� ����� ��� ������ ���������� �������
                    serverStartedStatus = true;
                    Program pr = new Program();

                    if (lineDel > 0)
                    {
                        lineDel--;
                    }

                    lock (consoleLock)
                    {
                        Console.WriteLine("������ �������� �� �����:" + port + "...");
                    }

                    _ = ThreadPool.QueueUserWorkItem(state =>
                    {
                        pr.StartServer(port, token);
                    });
                }//��������� ������ �� �������� �����
                else
                {

                    if (lineDel == 1)
                    {
                        Console.SetCursorPosition(0, Console.CursorTop - 1);
                        Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");//������ ������� �������� ������� 
                    }
                    else
                    {
                        lineDel++;
                        lock (consoleLock)
                        {
                            Console.WriteLine("������� �������� �������, ��� ��������� ������ ������ ������� -" + command[0] + "-");
                        }
                    }

                }//������ ����� � �������

            }//���� ��������� ������ � �������

        }

        private async void StartServer(int port, CancellationToken token)
        {
            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add("http://localhost:" + port + "/");
                _httpListener.Start();//������ �������

                if (lineDel > 0)
                {
                    lineDel--;
                }

                lock (consoleLock)
                {
                    Console.WriteLine("�������� ����������...");
                }
                Program mesSerCode = new Program();

                while (true)
                {
                    var context = await _httpListener.GetContextAsync();//������� ��� ����� �������

                    if (!_httpListener.IsListening)
                    {
                        break;
                    }

                    if (context.Request.IsWebSocketRequest)
                    {
                        //��������� ��� ����� ������
                        var webSocketContext = await context.AcceptWebSocketAsync(null);

                        _ = HandleClientAsync(webSocketContext.WebSocket);//������������ ������ � ��������� ������
                        users.Add(webSocketContext.WebSocket);//��������� ������ �������� �������� �������������� �������
                    }
                    else
                    {
                        //�� ������ ����� �� �������� �������
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }

                    token.ThrowIfCancellationRequested();//��������� ���� �� ������� ������� �������� �������
                }

            }
            catch (OperationCanceledException)
            {
                lock (consoleLock)
                {
                    Console.WriteLine("������ ��� ������������� ����������");
                }
            }
            catch (Exception ex)
            {
                lock (consoleLock)
                {
                    Console.WriteLine("������ ��� ������� �������:" + ex.Message);
                }
            }
        }//��������� ������

        private static void StopServer()
        {
            try
            {

                if (_httpListener != null && _httpListener.IsListening)
                {
                    _httpListener.Stop();
                    _httpListener.Close();
                    _httpListener = null;
                    _cancellationTokenSource?.Cancel();
                    _cancellationTokenSource = null;

                    if (users != null)
                    {
                        users.Clear();
                    }//������ ������ ����������� ��������

                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("������ ��� ��������� �������:" + ex.Message);
            }
        }//������������� ������

        public async Task HandleClientAsync(WebSocket webSocket)
        {
            byte[] bute = new byte[4096];
            lock (consoleLock)
            {
                Console.WriteLine("����� �����������");
            }
            try
            {

                while (true)
                {
                    WebSocketReceiveResult receivedResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(bute), CancellationToken.None);

                    if (receivedResult.MessageType == WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(bute, 0, receivedResult.Count);

                        if (message.StartsWith("NewMessage"))
                        {

                            if (users.Count > 1)
                            {

                                for (int i = 0; i < users.Count; i++)
                                {

                                    if (users[i] != webSocket)
                                    {
                                        await SendMessageToUser(users[i], message);//���������� ��������� ���� �������� ������� ����� �����������
                                    }

                                }

                            }
                            else
                            {
                                lock (consoleLock)
                                {
                                    Console.WriteLine("������: ������ ������ ����");
                                }
                            }

                        }
                        else if (message.StartsWith("Closed"))
                        {
                            break;
                        }
                        else
                        {
                            lock (consoleLock)
                            {
                                Console.WriteLine("������: ����������� ��������� ���� ��������");
                            }
                        }

                    }
                    else
                    {
                        lock (consoleLock)
                        {
                            Console.WriteLine("������� ����������� ��� ���������");
                        }
                    }

                    if (receivedResult.EndOfMessage)
                    {
                        Array.Clear(bute, 0, receivedResult.Count);
                    }

                }//�������� ���������� ���������

            }
            catch (Exception )
            {
                lock (consoleLock)
                {
                    Console.WriteLine("������ ��� ������ ���������: ");
                }
            }
            lock (consoleLock)
            {
                Console.WriteLine("������ ������ ����������");
            }

            if (users != null)
            {
                int index = users.FindIndex(x => x == webSocket);
                users.RemoveAt(index);
            }//������� ����� �� ������ �������� ������

        }//�������� ����� ��������� �������

        private async Task SendMessageToUser(WebSocket w, string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await w.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            lock (consoleLock)
            {
                Console.WriteLine("���������� ��������� �������:" + message);
            }
        }//���������� ��������� ����� �� ��� ������

    }

}

