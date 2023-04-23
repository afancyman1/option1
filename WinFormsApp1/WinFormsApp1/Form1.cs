﻿using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace WinFormsApp1
{
    public partial class Cache : Form
    {
        private const int serverport = 8081;
        private Thread cacheThread;
        private const int cacheport = 8082;
        public Cache()
        {
            InitializeComponent();
            StartCache();
        }
        private void StartCache()
        {
            Console.WriteLine("Cache starting !");
            IPAddress ipAddr = IPAddress.Loopback;
            TcpListener listener = new TcpListener(ipAddr, cacheport);
            listener.Start();
            Console.WriteLine($"Cache started on port {cacheport}");
            cacheThread = new Thread(() =>
            {
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    Console.WriteLine("Client connected to cache");
                    Thread clientThread = new Thread(() => HandleClient(client));
                    clientThread.Start();
                }
            });
            cacheThread.IsBackground = true;
            cacheThread.Start();
        }
        private TcpClient ConnectToServer()
        {
            try
            {
                IPAddress ipAddr = IPAddress.Loopback;
                TcpClient serverClient = new TcpClient(ipAddr.ToString(), serverport);
                Console.WriteLine("connected to main server");
                return serverClient;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"连接服务器失败：{ex.Message}");
                return null;
            }
        }

        // 添加一个新的方法，用于向 log_list 控件添加日志条目
        private void AddLogEntry(string entry)
        {
            Invoke((Action)(() => log_list.AppendText(entry + Environment.NewLine)));
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
            {
                string command = reader.ReadLine();

                // 获取当前应用的路径，并将其与 cache_files 文件夹结合
                string currentFolderPath = Path.GetDirectoryName(Application.ExecutablePath);
                string cacheFolderPath = Path.Combine(currentFolderPath, "../../../cachefiles");
                if (command == "LIST")
                {
                    using (TcpClient serverClient = ConnectToServer())
                    using (NetworkStream serverStream = serverClient.GetStream())
                    using (StreamReader serverreader = new StreamReader(serverStream, Encoding.UTF8))
                    using (StreamWriter serverwriter = new StreamWriter(serverStream, Encoding.UTF8) { AutoFlush = true })
                    {
                        //向服务器发送"LIST"指令
                        serverwriter.WriteLine("LIST");
                        serverwriter.Flush();
                        // 从服务器读取文件名列表
                        string fileListString = serverreader.ReadLine();
                        string[] fileNames = fileListString.Split(',');

                        // 将从服务器接收到的文件名列表写回给客户端
                        writer.WriteLine(string.Join(",", fileNames));
                    }
                }

                else if (command.StartsWith("GET_CONTENT"))
                {
                    string fileName = command.Substring("GET_CONTENT".Length).Trim();
                    string cachedFilePath = Path.Combine(cacheFolderPath, fileName);

                    if (File.Exists(cachedFilePath))
                    {
                        // 如果文件已经缓存在 cache 中，直接向客户端返回文件内容
                        string fileContent = File.ReadAllText(cachedFilePath);
                        writer.WriteLine(fileContent);

                        // 添加日志条目
                        AddLogEntry($"user request: file {fileName} at {DateTime.Now}");
                        AddLogEntry($"response: cached file {fileName}");
                    }
                    else
                    {
                        // 否则，向主服务器请求文件
                        try
                        {
                            using (TcpClient serverClient = ConnectToServer())
                            using (NetworkStream serverStream = serverClient.GetStream())
                            using (StreamReader serverReader = new StreamReader(serverStream, Encoding.UTF8))
                            using (StreamWriter serverWriter = new StreamWriter(serverStream, Encoding.UTF8) { AutoFlush = true })
                            {
                                // 将 GET_CONTENT 命令写入主服务器的流中
                                serverWriter.WriteLine($"GET_CONTENT {fileName}");
                                string fileContent = serverReader.ReadLine();

                                if (!string.IsNullOrEmpty(fileContent))
                                {
                                    // 将文件内容写入缓存
                                    File.WriteAllText(cachedFilePath, fileContent);

                                    // 向客户端返回文件内容
                                    writer.WriteLine(fileContent);
                                    // 添加日志条目
                                    AddLogEntry($"user request: file {fileName} at {DateTime.Now}");
                                    AddLogEntry($"response: file {fileName} downloaded from the server");
                                }
                                else
                                {
                                    writer.WriteLine("Error: File not found");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"连接服务器失败：{ex.Message}");
                        }
                    }
                }

                // 根据需求，可以在此处添加其他命令的处理逻辑
            }
        }






        private void show_files_button_Click(object sender, EventArgs e)
        {
            string currentFolderPath = Path.GetDirectoryName(Application.ExecutablePath);
            string folderPath = Path.Combine(currentFolderPath, "../../../cachefiles");
            string[] allFiles = Directory.GetFiles(folderPath);
            List<string> fileNames = new List<string>();

            foreach (string file in allFiles)
            {
                string fileName = Path.GetFileName(file);
                fileNames.Add(fileName);
            }

            file_list.Items.Clear();
            file_list.Items.AddRange(fileNames.ToArray());
        }

        private void clear_selected_file_button_Click(object sender, EventArgs e)
        {
            {
                if (file_list.SelectedItem == null)
                {
                    MessageBox.Show("请选择一个要删除的文件。");
                    return;
                }

                string fileName = file_list.SelectedItem.ToString();
                string currentFolderPath = Path.GetDirectoryName(Application.ExecutablePath);
                string cacheFolderPath = Path.Combine(currentFolderPath, "../../../cachefiles");
                string filePath = Path.Combine(cacheFolderPath, fileName);

                try
                {
                    File.Delete(filePath);
                    MessageBox.Show($"已成功删除文件：{fileName}");
                    // 从列表中移除文件名
                    file_list.Items.Remove(file_list.SelectedItem);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除文件失败：{ex.Message}");
                }
            }
        }

        private void file_list_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void log_list_TextChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            log_list.Clear();
        }
    }
}