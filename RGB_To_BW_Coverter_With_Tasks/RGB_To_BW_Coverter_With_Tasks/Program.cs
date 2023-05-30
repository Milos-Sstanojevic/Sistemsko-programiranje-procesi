using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WebServer
{
    class Program
    {
        static readonly Dictionary<string, byte[]> cache = new Dictionary<string, byte[]>();
        static readonly string rootFolder = "C:\\Users\\Milos\\OneDrive\\Radna površina\\fax\\3. godina\\sistemsko\\Sistemsko-programiranje-procesi\\RGB_To_BW_Coverter_With_Tasks\\RGB_To_BW_Coverter_With_Tasks\\bin\\Debug";
        static readonly object cacheLock = new object();

        static async Task Main(string[] args)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 8083);
            listener.Start();
            Console.WriteLine("Cekam zahtev sa porta 8083...");

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                _ = ProcessRequestAsync(client);
            }
        }

        static async Task ProcessRequestAsync(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            StreamReader reader = new StreamReader(stream);
            StreamWriter writer = new StreamWriter(stream);

            try
            {
                string request = await reader.ReadLineAsync();

                if (request != null)
                {
                    Console.WriteLine("Primljeni zahtev: " + request);

                    string[] parts = Regex.Split(request, @"\s+");

                    if (parts.Length == 3 && parts[0] == "GET")
                    {
                        string filename = parts[1].Substring(1);
                        string filepath = rootFolder + "/" + filename;

                        Console.WriteLine("Putanja to fajla je: " + filepath);

                        if (TryGetFromCache(filepath, out byte[] response))
                        {
                            Console.WriteLine("Fajl se trazi u kesu je: " + filename);

                            Console.WriteLine("Sadrzaj kesa: ");

                            await WriteResponseAsync(writer, response);
                        }
                        else if (File.Exists(filepath))
                        {
                            Console.WriteLine("Prevodim sliku: " + filename + ", u crno - belu sliku");

                            using (Bitmap bmp = new Bitmap(filepath))
                            using (MemoryStream ms = new MemoryStream())
                            {
                                bmp.Save(ms, ImageFormat.Jpeg);
                                byte[] bytes = ms.ToArray();
                                byte[] converted = await ConvertToBlackAndWhiteAsync(bytes, filename);
                                AddToCache(filepath, converted);

                                await WriteResponseAsync(writer, converted);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Nema fajla: " + filename);

                            await WriteNotFoundResponseAsync(writer);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Los zahtev: " + request);

                        await WriteBadRequestResponseAsync(writer);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Gresak sa obsluzivanjem zahteva: " + ex.Message);
            }
            finally
            {
                writer.Close();
                reader.Close();
                stream.Close();
                client.Close();
            }
        }

        static bool TryGetFromCache(string filepath, out byte[] response)
        {
            lock (cacheLock)
            {
                return cache.TryGetValue(filepath, out response);
            }
        }

        static void AddToCache(string filepath, byte[] converted)
        {
            lock (cacheLock)
            {
                cache[filepath] = converted;
            }
        }

        static async Task WriteResponseAsync(StreamWriter writer, byte[] response)
        {
            await writer.WriteAsync("HTTP/1.1 200 OK\r\n");
            await writer.WriteAsync("Content-Type: image/jpeg\r\n");
            await writer.WriteAsync("Content-Length: " + response.Length + "\r\n");
            await writer.WriteAsync("\r\n");
            await writer.FlushAsync();
            await writer.BaseStream.WriteAsync(response, 0, response.Length);
        }

        static async Task WriteNotFoundResponseAsync(StreamWriter writer)
        {
            await writer.WriteAsync("HTTP/1.1 404 Not Found\r\n");
            await writer.WriteAsync("\r\n");
            await writer.FlushAsync();
        }

        static async Task WriteBadRequestResponseAsync(StreamWriter writer)
        {
            await writer.WriteAsync("HTTP/1.1 400 Bad Request\r\n");
            await writer.WriteAsync("\r\n");
            await writer.FlushAsync();
        }

        static async Task<byte[]> ConvertToBlackAndWhiteAsync(byte[] input, string filename)
        {
            Console.WriteLine("Pozvana funkcija za konverziju u crno - belu sliku");
            using (MemoryStream ms = new MemoryStream(input))
            using (Bitmap bmp = new Bitmap(ms))
            {
                for (int i = 0; i < bmp.Width; i++)
                {
                    for (int j = 0; j < bmp.Height; j++)
                    {
                        Color color = bmp.GetPixel(i, j);
                        int average = (color.R + color.G + color.B) / 3;
                        bmp.SetPixel(i, j, Color.FromArgb(average, average, average));
                    }
                }

                string newFilename = rootFolder + Path.GetFileNameWithoutExtension(filename) + "_bw" + Path.GetExtension(filename);
                bmp.Save(newFilename, ImageFormat.Jpeg);
                using (MemoryStream ms1 = new MemoryStream())
                {
                    bmp.Save(ms1, ImageFormat.Jpeg);
                    return ms1.ToArray();
                }
            }
        }
    }
}
