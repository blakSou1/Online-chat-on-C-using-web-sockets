using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace MesengerServer
{
    public class Program
    {
        private static readonly List<WebSocket> users = new List<WebSocket>();//список пользователей
        private static readonly object consoleLock = new object();//необходим для устранения конкуренции за ввод вывод в консоли разными потоками
        private static int lineDel = 0;//переменная используется для чистки консоли от последовательно введенных неверных команд
        private static HttpListener? _httpListener;
        private static CancellationTokenSource? _cancellationTokenSource;

        private static Task Main(string[] args)
        {
            int port = 8080;// порт сервака
            bool serverStartedStatus = false;
            //список команд
            string[] command = ["/info/", "команда для просмотра списка существующих команд.", "/serverStoped/", "команда выключает сервак.", "/serverStarted/", "команда запускает сервер"];
            //добавлять команды строго по порядку добавляя после каждой команды ее описание и проверку ввода этой команды в цикле ниже

            Console.WriteLine("для запуска сервера введите:/serverStarted/");//выводим основные команды в консоль 
            Console.WriteLine("для остановки сервера введите команду:/serverStoped/");

            while (true)
            {
                string? console;
                console = Console.ReadLine();//читаем ввод в консоль

                if (console == command[0])
                {
                    lock (consoleLock)
                    {
                        Console.WriteLine("список команд:");
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
                    }//выводим все существующие команды и их описание в консоль

                }//вызвана команда предоставляющая список активных команд
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
                            Console.WriteLine("сервер отрубается по запросу команды...");
                        }
                        serverStartedStatus = false;

                        if (_cancellationTokenSource != null)
                        {
                            _cancellationTokenSource.Cancel();//вызываем ошибку в потоке выполнения сервера тем самым выключая
                        }

                        StopServer();//метод выключающий сервер
                    }
                    else
                    {
                        lock (consoleLock)
                        {
                            Console.WriteLine("сервер не запущен...");
                        }
                    }

                }//выключаем сервак
                else if (console == command[4])
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    CancellationToken token = _cancellationTokenSource.Token;//обнавляем токен для отмены выполнения сервера
                    serverStartedStatus = true;
                    Program pr = new Program();

                    if (lineDel > 0)
                    {
                        lineDel--;
                    }

                    lock (consoleLock)
                    {
                        Console.WriteLine("сервер стартует на порту:" + port + "...");
                    }

                    _ = ThreadPool.QueueUserWorkItem(state =>
                    {
                        pr.StartServer(port, token);
                    });
                }//запускаем сервер на указаном порту
                else
                {

                    if (lineDel == 1)
                    {
                        Console.SetCursorPosition(0, Console.CursorTop - 1);
                        Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");//чистим строчку неверной команды 
                    }
                    else
                    {
                        lineDel++;
                        lock (consoleLock)
                        {
                            Console.WriteLine("введена неверная команда, для просмотра списка команд введите -" + command[0] + "-");
                        }
                    }

                }//ошибка ввода в консоли

            }//цикл обработки команд с консоли

        }

        private async void StartServer(int port, CancellationToken token)
        {
            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add("http://localhost:" + port + "/");
                _httpListener.Start();//запуск сервера

                if (lineDel > 0)
                {
                    lineDel--;
                }

                lock (consoleLock)
                {
                    Console.WriteLine("ожидание подлючений...");
                }
                Program mesSerCode = new Program();

                while (true)
                {
                    var context = await _httpListener.GetContextAsync();//слушаем веб сокет запросы

                    if (!_httpListener.IsListening)
                    {
                        break;
                    }

                    if (context.Request.IsWebSocketRequest)
                    {
                        //принимаем веб сокет запрос
                        var webSocketContext = await context.AcceptWebSocketAsync(null);

                        _ = HandleClientAsync(webSocketContext.WebSocket);//обрабатываем запрос в отдельном потоке
                        users.Add(webSocketContext.WebSocket);//пополняем список активных клиентов подключившимся сокетом
                    }
                    else
                    {
                        //не хаваем любые не вебсокет запросы
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }

                    token.ThrowIfCancellationRequested();//проверяем была ли введена команда закрытия зервера
                }

            }
            catch (OperationCanceledException)
            {
                lock (consoleLock)
                {
                    Console.WriteLine("сервер был принудительно остановлен");
                }
            }
            catch (Exception ex)
            {
                lock (consoleLock)
                {
                    Console.WriteLine("ошибка при запуске сервера:" + ex.Message);
                }
            }
        }//запускаем сервер

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
                    }//чистим список подключеных клиентов

                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("ошибка при остановке сервера:" + ex.Message);
            }
        }//останавливаем сервер

        public async Task HandleClientAsync(WebSocket webSocket)
        {
            byte[] bute = new byte[4096];
            lock (consoleLock)
            {
                Console.WriteLine("новое подключение");
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
                                        await SendMessageToUser(users[i], message);//отправляем сообщения всем активным сокетам кроме отправителя
                                    }

                                }

                            }
                            else
                            {
                                lock (consoleLock)
                                {
                                    Console.WriteLine("ошибка: список юзеров пуст");
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
                                Console.WriteLine("ошибка: некоректное сообщение было получено");
                            }
                        }

                    }
                    else
                    {
                        lock (consoleLock)
                        {
                            Console.WriteLine("схавали некоректный тип сообщения");
                        }
                    }

                    if (receivedResult.EndOfMessage)
                    {
                        Array.Clear(bute, 0, receivedResult.Count);
                    }

                }//получаем клиентские сообщения

            }
            catch (Exception )
            {
                lock (consoleLock)
                {
                    Console.WriteLine("ошибка при приеме сообщения: ");
                }
            }
            lock (consoleLock)
            {
                Console.WriteLine("клиент закрыл соединение");
            }

            if (users != null)
            {
                int index = users.FindIndex(x => x == webSocket);
                users.RemoveAt(index);
            }//удаляем юзера из списка активных юзеров

        }//основной поток обработки клиента

        private async Task SendMessageToUser(WebSocket w, string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await w.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            lock (consoleLock)
            {
                Console.WriteLine("отправляем сообщение клиенту:" + message);
            }
        }//отправляет сообщение юзеру по веб сокету

    }

}

