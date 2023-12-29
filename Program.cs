using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace lab11
{
    public class TickerContext : DbContext
    {
        public DbSet<Ticker> Tickers { get; set; }
        public DbSet<Price> Prices { get; set; }
        public DbSet<TodaysCondition> TodaysConditions { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=TestDB;Trusted_Connection=True;MultipleActiveResultSets=true;");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Ticker>().HasKey(t => t.Id);
            modelBuilder.Entity<Price>().HasKey(p => p.Id);
            modelBuilder.Entity<TodaysCondition>().HasKey(tc => tc.Id);
        }
    }

    public class Ticker
    {
        public int Id { get; set; }
        public string TickerSymbol { get; set; }
    }

    public class Price
    {
        public int Id { get; set; }
        public int TickerId { get; set; }
        public double PriceValue { get; set; }
        public DateTime Date { get; set; }
    }

    public class TodaysCondition
    {
        public int Id { get; set; }
        public int TickerId { get; set; }
        public string State { get; set; }

        public static double GetPriceByTicker(string ticker)
        {
            using (var context = new TickerContext())
            {
                var tickerId = context.Tickers
                    .Where(t => t.TickerSymbol == ticker)
                    .Select(t => t.Id)
                    .FirstOrDefault();


                if (tickerId != 0)
                {
                    Console.WriteLine($"Тикер Id: {tickerId}");

                    var pricesForTicker = context.Prices
                        .Where(p => p.TickerId == tickerId)
                        .OrderBy(p => p.Date)
                        .Select(p => new { p.Date, p.PriceValue })
                        .ToList();

                    Console.WriteLine($"Найдено {pricesForTicker.Count} цен для тикера {ticker}");


                    if (pricesForTicker.Any())
                    {
                        Console.WriteLine($"Цены для тикера {ticker}:");
                        foreach (var price in pricesForTicker)
                        {
                            Console.WriteLine($"Дата: {price.Date}, Цена: {price.PriceValue}");
                            return Convert.ToDouble(price.PriceValue);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Для тикера {ticker} нет цен в базе данных.");
                    }
                }
                else
                {
                    Console.WriteLine($"Тикер {ticker} не найден в базе данных.");
                }
            }

            return -1;
        }
    }


    class Program
    {
        static async Task Main(string[] args) //асинхронно вызываем
        {
            _ = StartServerAsync(); //вызов сервера
            await StartClientAsync(); //вызов клиента 
        }

        static async Task StartServerAsync()
        {
            TcpListener server = null; //создаем прослушку сервер
            try
            {
                server = new TcpListener(IPAddress.Any, 12345); //слушаем на текущем айпи и порте
                server.Start(); //старт прослушки
                Console.WriteLine("Сервер запущен. Ожидание подключений...");

                while (true) //бесконечно принимаем сигнал
                {
                    TcpClient client = await server.AcceptTcpClientAsync();
                    _ = HandleClientAsync(client); //управление клиентом
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сервера: {ex.Message}");
            }
            finally
            {
                server?.Stop();
            }
        }

        static async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[1024]; //буфер для потока данных
                    int bytesRead;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0) //пока идут данные, читаем
                    {
                        string tickerRequest = Encoding.UTF8.GetString(buffer, 0, bytesRead); //принимаем байты
                        Console.WriteLine($"Получено от клиента: {tickerRequest}");

                        double stockPrice = TodaysCondition.GetPriceByTicker(tickerRequest); //для тикера ищем цену

                        string response = $"Цена акции ({tickerRequest}): {stockPrice}"; //кидаем ответ
                        byte[] responseData = Encoding.UTF8.GetBytes(response);
                        await stream.WriteAsync(responseData, 0, responseData.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка обработки клиента: {ex.Message}");
            }
            finally
            {
                client.Close();
                Console.WriteLine("Клиент отключен.");
            }
        }

        static async Task StartClientAsync()
        {
            try
            {
                TcpClient client = new TcpClient("localhost", 12345);
                Console.WriteLine("Подключено к серверу.");

                using (NetworkStream stream = client.GetStream())
                {
                    Console.Write("Введите тикер акции: ");
                    string tickerToSend = Console.ReadLine();

                    byte[] request = Encoding.UTF8.GetBytes(tickerToSend); //кидаем ответ
                    await stream.WriteAsync(request, 0, request.Length);

                    byte[] buffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead); //получаем ответ

                    Console.WriteLine($"Ответ от сервера: {response}");
                }

                client.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка клиента: {ex.Message}");
            }
        }
    }
}

