﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using NewLife.Common;
using NewLife.CommonEntity;
using NewLife.Compression;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Model;
using NewLife.Net;
using NewLife.Net.Common;
using NewLife.Net.Proxy;
using NewLife.Net.Sockets;
using NewLife.Net.Tcp;
using NewLife.Reflection;
using NewLife.Serialization;
using NewLife.Threading;
using NewLife.Xml;
using XCode.DataAccessLayer;
using XCode.Sync;
using XCode.Transform;

namespace Test
{
    public class Program
    {
        private static void Main(string[] args)
        {
            XTrace.UseConsole();
            while (true)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
#if !DEBUG
                try
                {
#endif
                    Test8();
#if !DEBUG
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
#endif

                sw.Stop();
                Console.WriteLine("OK! 耗时 {0}", sw.Elapsed);
                ConsoleKeyInfo key = Console.ReadKey(true);
                if (key.Key != ConsoleKey.C) break;
            }
        }

        static HttpProxy http = null;
        private static void Test1()
        {
            var server = new HttpReverseProxy();
            server.Port = 888;
            server.ServerHost = "www.cnblogs.com";
            server.ServerPort = 80;
            server.Start();

            var ns = Enum.GetNames(typeof(ConsoleColor));
            var vs = Enum.GetValues(typeof(ConsoleColor));
            for (int i = 0; i < ns.Length; i++)
            {
                Console.ForegroundColor = (ConsoleColor)vs.GetValue(i);
                Console.WriteLine(ns[i]);
            }
            Console.ReadKey();

            //NewLife.Net.Application.AppTest.Start();

            http = new HttpProxy();
            http.Port = 8080;
            http.EnableCache = true;
            //http.OnResponse += new EventHandler<HttpProxyEventArgs>(http_OnResponse);
            http.Start();

            var old = HttpProxy.GetIEProxy();
            if (!old.IsNullOrWhiteSpace()) Console.WriteLine("旧代理：{0}", old);
            HttpProxy.SetIEProxy("127.0.0.1:" + http.Port);
            Console.WriteLine("已设置IE代理，任意键结束测试，关闭IE代理！");

            ThreadPoolX.QueueUserWorkItem(ShowStatus);

            Console.ReadKey(true);
            HttpProxy.SetIEProxy(old);

            //server.Dispose();
            http.Dispose();

            //var ds = new DNSServer();
            //ds.Start();

            //for (int i = 5; i < 6; i++)
            //{
            //    var buffer = File.ReadAllBytes("dns" + i + ".bin");
            //    var entity2 = DNSEntity.Read(buffer, false);
            //    Console.WriteLine(entity2);

            //    var buffer2 = entity2.GetStream().ReadBytes();

            //    var p = buffer.CompareTo(buffer2);
            //    if (p != 0)
            //    {
            //        Console.WriteLine("{0:X2} {1:X2} {2:X2}", p, buffer[p], buffer2[p]);
            //    }
            //}
        }
        private static void TestNatProxy()
        {
            NATProxy proxy = new NATProxy();
            proxy.ServerAddress = System.Net.IPAddress.Parse("192.168.1.105");
            proxy.ServerPort = 6800;

            proxy.Address = proxy.ServerAddress;
            proxy.AddressFamily = AddressFamily.InterNetwork;
            proxy.Port = 8000;
            proxy.ServerProtocolType = ProtocolType.Tcp;
            proxy.Start();
        }

        static void ShowStatus()
        {
            //var pool = PropertyInfoX.GetValue<SocketBase, ObjectPool<NetEventArgs>>("Pool");
            var pool = NetEventArgs.Pool;

            while (true)
            {
                var asyncCount = 0; try
                {
                    foreach (var item in http.Servers)
                    {
                        asyncCount += item.AsyncCount;
                    }
                    foreach (var item in http.Sessions.Values.ToArray())
                    {
                        var remote = (item as IProxySession).RemoteClientSession;
                        if (remote != null) asyncCount += remote.Host.AsyncCount;
                    }
                }
                catch (Exception ex) { Console.WriteLine(ex.ToString()); }

                Int32 wt = 0;
                Int32 cpt = 0;
                ThreadPool.GetAvailableThreads(out wt, out cpt);
                Int32 threads = Process.GetCurrentProcess().Threads.Count;

                var color = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("异步:{0} 会话:{1} Thread:{2}/{3}/{4} Pool:{5}/{6}/{7}", asyncCount, http.Sessions.Count, threads, wt, cpt, pool.StockCount, pool.FreeCount, pool.CreateCount);
                Console.ForegroundColor = color;

                Thread.Sleep(3000);

                //GC.Collect();
            }
        }

        static void Test2()
        {
            HttpClientMessageProvider client = new HttpClientMessageProvider();
            client.Uri = new Uri("http://localhost:8/Web/MessageHandler.ashx");

            var rm = MethodMessage.Create("Admin.Login", "admin", "admin");
            //rm.Header.Channel = 88;

            //Message.Debug = true;
            //var ms = rm.GetStream();
            //var m2 = Message.Read(ms);

            Message msg = client.SendAndReceive(rm, 0);
            var rs = msg as EntityMessage;
            Console.WriteLine("返回：" + rs.Value);

            msg = client.SendAndReceive(rm, 0);
            rs = msg as EntityMessage;
            Console.WriteLine("返回：" + rs.Value);
        }

        static void Test3()
        {
            var uri = new NetUri("udp://x2:3389");

            Console.WriteLine(uri);
            Console.WriteLine(uri.ProtocolType);
            Console.WriteLine(uri.EndPoint);
            Console.WriteLine(uri.Address);
            Console.WriteLine(uri.Host);
            Console.WriteLine(uri.Port);

            var xml = uri.ToXml();
            Console.WriteLine(xml);

            uri = xml.ToXmlEntity<NetUri>();
            Console.WriteLine(uri);
        }

        static NetServer server = null;
        static IMessageProvider smp = null;
        static IMessageProvider cmp = null;
        static void Test4()
        {
            Console.Clear();
            if (server == null)
            {
                server = new NetServer();
                server.Port = 1234;
                //server.Received += new EventHandler<NetEventArgs>(server_Received);

                var mp = new ServerMessageProvider(server);
                mp.OnReceived += new EventHandler<MessageEventArgs>(smp_OnReceived);
                //mp.MaxMessageSize = 1460;
                mp.AutoJoinGroup = true;
                smp = mp;

                server.Start();
            }

            if (cmp == null)
            {
                var client = NetService.CreateSession(new NetUri("udp://::1:1234"));
                client.ReceiveAsync();
                cmp = new ClientMessageProvider() { Session = client };
                cmp.OnReceived += new EventHandler<MessageEventArgs>(cmp_OnReceived);
            }

            //Message.Debug = true;
            var msg = new EntityMessage();
            var rnd = new Random((Int32)DateTime.Now.Ticks);
            var bts = new Byte[rnd.Next(1000000, 5000000)];
            //var bts = new Byte[1460 * 1 - rnd.Next(0, 20)];
            rnd.NextBytes(bts);
            msg.Value = bts;

            //var rs = cmp.SendAndReceive(msg, 5000);
            cmp.Send(msg);
        }

        static void smp_OnReceived(object sender, MessageEventArgs e)
        {
            var msg = e.Message;
            Console.WriteLine("服务端收到：{0}", msg);
            var rs = new EntityMessage();
            rs.Value = "收到" + msg;
            (sender as IMessageProvider).Send(rs);
        }

        static void cmp_OnReceived(object sender, MessageEventArgs e)
        {
            Console.WriteLine("客户端收到：{0}", e.Message);
        }

        static void Test5()
        {
            using (var zf = new ZipFile())
            {
                zf.AddFile("XCode.pdb");
                zf.AddFile("NewLife.Core.pdb");

                zf.Write("test.zip");
                zf.Write("test.7z");
            }
            //using (var zf = new ZipFile("Test.lzma.zip"))
            //{
            //    foreach (var item in zf.Entries.Values)
            //    {
            //        Console.WriteLine("{0} {1}", item.FileName, item.CompressionMethod);
            //    }

            //    zf.Extract("lzma");
            //}
        }

        static void Test6()
        {
            Message.DumpStreamWhenError = true;
            //var msg = new EntityMessage();
            //msg.Value = Guid.NewGuid();
            var msg = new MethodMessage();
            msg.TypeName = "Admin";
            msg.Name = "Login";
            msg.Parameters = new Object[] { "admin", "password" };

            var kind = RWKinds.Json;
            var ms = msg.GetStream(kind);
            //ms = new MemoryStream(ms.ReadBytes(ms.Length - 1));
            //Console.WriteLine(ms.ReadBytes().ToHex());
            Console.WriteLine(ms.ToStr());
            //ms = msg.GetStream(RWKinds.Xml);
            //Console.WriteLine(ms.ToStr());

            Message.Debug = true;
            ms.Position = 0;
            var rs = Message.Read(ms, kind);
            Console.WriteLine(rs);
        }

        static void Test7()
        {
            //Console.Write("请输入表达式：");
            //var code = Console.ReadLine();

            //var rs = ScriptEngine.Execute(code, new Dictionary<String, Object> { { "a", 222 }, { "b", 333 } });
            ////Console.WriteLine(rs);

            //var se = ScriptEngine.Create(code);
            //var fm = code.Replace("a", "{0}").Replace("b", "{1}");
            //for (int i = 1; i <= 9; i++)
            //{
            //    for (int j = 1; j <= i; j++)
            //    {
            //        Console.Write(fm + "={2}\t", j, i, se.Invoke(i, j));
            //    }
            //    Console.WriteLine();
            //}

            var se = ScriptEngine.Create("Test.Program.TestMath(k)");
            if (se.Method == null)
            {
                se.Parameters.Add("k", typeof(Double));
                se.Compile();
            }

            var fun = (DM)(Object)Delegate.CreateDelegate(typeof(DM), se.Method as MethodInfo);

            var timer = 1000000;
            var k = 123;
            CodeTimer.ShowHeader();
            CodeTimer.TimeLine("原生", timer, n => TestMath(k));
            CodeTimer.TimeLine("动态", timer, n => se.Invoke(k));
            CodeTimer.TimeLine("动态2", timer, n => fun(k));
        }
        public static Double TestMath(Double k)
        {
            //var bts = File.ReadAllBytes(Assembly.GetExecutingAssembly().Location);
            return Math.Sin(k) * Math.Log10(k) * Math.Exp(k);
        }
        delegate Object DM(Double k);

        static SysConfig Load()
        {
            var filename = SysConfig._.ConfigFile;
            if (filename.IsNullOrWhiteSpace()) return null;
            filename = filename.GetFullPath();
            if (!File.Exists(filename)) return null;

            try
            {
                var config = filename.ToXmlFileEntity<SysConfig>();
                if (config == null) return null;

                //config.OnLoaded();

                //// 第一次加载，建立定时重载定时器
                //if (timer == null && _.ReloadTime > 0) timer = new TimerX(s => Current = null, null, _.ReloadTime * 1000, _.ReloadTime * 1000);

                return config;
            }
            catch (Exception ex) { XTrace.WriteException(ex); return null; }
        }

        static void Test9()
        {
            var tb = Administrator.Meta.Table.DataTable;
            var table = ObjectContainer.Current.Resolve<IDataTable>();
            table = table.CopyAllFrom(tb);

            // 添加两个字段
            var fi = table.CreateColumn();
            fi.ColumnName = "LastUpdate";
            fi.DataType = typeof(DateTime);
            table.Columns.Add(fi);

            fi = table.CreateColumn();
            fi.ColumnName = "LastSync";
            fi.DataType = typeof(DateTime);
            table.Columns.Add(fi);

            var dal = DAL.Create("Common99");
            // 检查架构
            dal.SetTables(table);

            var sl = new SyncSlave();
            sl.Factory = dal.CreateOperate(table.TableName);

            var mt = new SyncMaster();
            mt.Facotry = Administrator.Meta.Factory;
            //mt.LastUpdateName = Administrator._.LastLogin;

            var sm = new SyncManager();
            sm.Slave = sl;
            sm.Master = mt;

            sm.Start();
        }

        static void Test11()
        {
            var str = "78-01-6B-CC-D1-64-68-08-2B-67-00-01-21-86-2D-9F-4F-AD-3F-BB-FD-F8-EB-3D-6F-0D-8C-0C-4C-F5-8A-CB-D3-18-0F-2F-60-30-04-CB-32-30-70-33-94-A7-E6-24-E7-E7-A6-EA-95-54-94-00-00-38-FD-12-D4";
            var buf = str.ToHex().ReadBytes(2);
            Console.WriteLine("压缩原文：");
            Console.WriteLine(buf.ToHex("-", 16));
            Console.WriteLine("Length={0}", buf.Length);
            var old = buf;

            buf = buf.Decompress();
            var old2 = buf;
            Console.WriteLine("解压：");
            Console.WriteLine(buf.ToHex("-", 16));
            Console.WriteLine("Length={0}", buf.Length);

            buf = buf.Compress();
            Console.WriteLine("压缩：");
            Console.WriteLine(buf.ToHex("-", 16));
            Console.WriteLine("Length={0}", buf.Length);
            Console.WriteLine(buf.CompareTo(old));

            buf = buf.Decompress();
            Console.WriteLine("再次解压：");
            Console.WriteLine(buf.ToHex("-", 16));
            Console.WriteLine("Length={0}", buf.Length);
            Console.WriteLine(buf.CompareTo(old2));

            str = "81-6C-29-00-80-56-77-00-00-00-00-00-12-00-B4-F3-CA-AF-CD-B7-C7-EB-BC-ED-30-32-30-35-2E-73-77-66-01-C3-A0-00-31-00-00-00-00-00-00-00-0B-00-77-65-6C-63-6F-6D-65-2E-74-78-74";
            old = buf = str.ToHex();
            str = "78-01-ED-BD-07-60-1C-49-96-25-26-2F-6D-CA-7B-7F-4A-F5-4A-D7-E0-74-A1-08-80-60-13-24-D8-90-40-10-EC-C1-88-CD-E6-92-EC-1D-69-47-23-29-AB-2A-81-CA-65-56-65-5D-66-16-40-CC-ED-9D-BC-F7-DE-7B-EF-BD-F7-DE-7B-EF-BD-F7-BA-3B-9D-4E-27-F7-DF-FF-3F-5C-66-64-01-6C-F6-CE-4A-DA-C9-9E-21-80-AA-C8-1F-3F-7E-7C-1F-3F-22-FE-E0-F2-CE-AF-F1-07-FD-E4-D5-AF-81-E7-B7-F8-35-FE-B6-5F-F6-CF-FC-8D-FF-FC-DF-F9-4F-FE-B7-7F-DF-7F-BF-B3-B7-73-7F-DC-5C-9D-FF-9A-FF-E8-5F-F4-6B-EC-FE-1A-F2-FC-86-BF-C6-55-5E-4E-AB-45-3E-6E-DF-B5-FF-0F";
            var buf2 = str.ToHex().ReadBytes(2);

            Console.WriteLine("原文：");
            Console.WriteLine(buf.ToHex("-", 16));
            buf = buf.Compress();
            Console.WriteLine("压缩后：");
            Console.WriteLine(buf.ToHex("-", 16));
            Console.WriteLine("Length={0}", buf.Length);
            Console.WriteLine(buf.CompareTo(buf2));

            buf = buf.Decompress();
            Console.WriteLine("解压后：");
            Console.WriteLine(buf.ToHex("-", 16));
            Console.WriteLine("Length={0}", buf.Length);
            Console.WriteLine(buf.CompareTo(old));
        }

        static NetServer test12Server = null;
        static Thread[] test12Ths = null;
        static void Test12()
        {
            test12Server = new NetServer();
            test12Server.Address = IPAddress.Parse("127.0.0.1");
            test12Server.Port = 9000;
            test12Server.ProtocolType = System.Net.Sockets.ProtocolType.Tcp;
            test12Server.Accepted += new EventHandler<NetEventArgs>(test12Server_Accepted);
            test12Server.Received += new EventHandler<NetEventArgs>(test12Server_Received);
            test12Server.Start();
            foreach (var item in test12Server.Servers)
            {
                if (item is TcpServer)
                {
                    //(item as TcpServer).MaxNotActive = 5;
                    (item as TcpServer).ShowEventLog = true;
                }
            }

            //test12Ths = new Thread[1];
            //for (int i = 0; i < test12Ths.Length; i++)
            //{
            //    test12Ths[i] = new Thread(test12Send);
            //    test12Ths[i].Name = "thread " + i.ToString();
            //    test12Ths[i].IsBackground = true;
            //    test12Ths[i].Start();
            //}

        }

        static void test12Server_Accepted(object sender, NetEventArgs e)
        {
            Console.WriteLine("accept client");
        }

        static void test12Server_Received(object sender, NetEventArgs e)
        {
            //e.Session.Send(e.GetStream());
            Console.WriteLine(e.GetString());
        }
        static void test12Send()
        {
            ISocketSession session = NetService.CreateSession(new NetUri("Tcp://127.0.0.1:9000"));
            return;
            //服务端检测不到有客户端连接
            Thread.Sleep(3000);
            //while (true)
            {
                Thread.Sleep(10000);
                try
                {
                    session.Send("abcdefghijklmn");
                    //此时同时触发客户端连接事件和接收数据事件。
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        static void Test8()
        {
            var file = "Common.db";
            if (File.Exists(file)) File.Delete(file);

            var admin = Administrator.FindByID(1);

            DAL.ShowSQL = false;
            ThreadPoolX.Debug = false;
            var pool = ThreadPoolX.Instance;
            pool.MaxThreads = 5;
            idx = 0;
            Total = 0;
            Error = 0;
            var count = pool.MaxThreads * 40;
            for (int i = 0; i < count; i++)
            {
                pool.Queue(Test8_2);
            }
            pool.WaitAll(2000);
            var max = count * 10.0;
            for (int i = 0; i < 10000; i++)
            {
                Console.CursorLeft = 0;

                Console.Write("正确：{0} 错误：{1} 已完成：{2:p}", Total, Error, (Total + Error) / max);

                Thread.Sleep(500);
            }
        }

        static Int32 idx = 0;
        static Int32 Total = 0;
        static Int32 Error = 0;

        static void Test8_2(Object state)
        {
            var tid = Thread.CurrentThread.ManagedThreadId;
            var rnd = new Random((Int32)DateTime.Now.Ticks);
            var pre = "Test_" + tid + "_" + rnd.Next(999999999) + "_";
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    var admin = new Administrator();
                    //admin.Name = pre + rnd.Next(0, 999999999);
                    admin.Name = pre + idx++;
                    admin.RoleID = 1;
                    admin.Insert();

                    Total++;
                }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains(" is locked"))
                        XTrace.WriteLine(ex.Message);

                    Error++;
                }
            }
        }

        static void Test13()
        {
            var file = @"E:\BaiduYunDownload\xiaomi.db";
            var file2 = Path.ChangeExtension(file, "sqlite");
            DAL.AddConnStr("src", "Data Source=" + file, null, "sqlite");
            DAL.AddConnStr("des", "Data Source=" + file2, null, "sqlite");

            if (!File.Exists(file2))
            {
                var et = new EntityTransform();
                et.SrcConn = "src";
                et.DesConn = "des";
                //et.PartialTableNames.Add("xiaomi");
                //et.PartialCount = 1000000;

                et.Transform();
            }

            var sw = new Stopwatch();

            var dal = DAL.Create("src");
            var eop = dal.CreateOperate(dal.Tables[0].TableName);
            sw.Start();
            var count = eop.Count;
            sw.Stop();
            XTrace.WriteLine("{0} 耗时 {1}ms", count, sw.ElapsedMilliseconds);
            sw.Reset(); sw.Start();
            count = eop.FindCount();
            sw.Stop();
            XTrace.WriteLine("{0} 耗时 {1}ms", count, sw.ElapsedMilliseconds);

            var entity = eop.Create();
            entity["username"] = "Stone";
            entity.Save();
            count = eop.FindCount();
            Console.WriteLine(count);

            entity.Delete();
            count = eop.FindCount();
            Console.WriteLine(count);
        }
    }
}