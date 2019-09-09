using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Xml.Linq;
using System.IO;
using System.Management;
using System.Threading;
using System.Linq;
using System.Text.RegularExpressions;

namespace Server
{
    public partial class Service1 : ServiceBase
    {
        
        Thread T;
        
        static ManagementScope scop = new ManagementScope(@"root\cimv2");
        
        bool mustStop = false;
        static string localPath = AppDomain.CurrentDomain.BaseDirectory;
        static ushort port = 45000;

        
        public static List<Process> GetProcesses(string path)
        {
            List<Process> res = new List<Process>();
            
            path = Regex.Replace(path, @"(?<!\\)\\(?!\\)", @"\\");
            string query = "select Description, ExecutablePath, KernelModeTime  from win32_process" +
                " WHERE ExecutablePath LIKE '" + path.Trim(' ') + "%'";
            UR.WriteLog(query);
            ObjectQuery Q = new ObjectQuery(query);
            ManagementObjectSearcher sh = new ManagementObjectSearcher(scop, Q);
            try
            {
                ManagementObjectCollection col = sh.Get();
                foreach (ManagementObject m in col)
                {
                    res.Add(new Process(m["Description"].ToString(), m["ExecutablePath"].ToString(), uint.Parse(m["KernelModeTime"].ToString())));
                }
            }
            catch (Exception e)
            {
                UR.WriteLog(e.Message);
            }
            return res;
        }

        public static string TerminateProcess(Process p)
        {
            string res = String.Empty;
            string path = Regex.Replace(p.Path, @"(?<!\\)\\(?!\\)", @"\\");
            string query = "select *  from win32_process" +
                " WHERE Description ='" + p.Description + "' AND ExecutablePath = '" + path + "'";
            UR.WriteLog(query);
            ObjectQuery Q = new ObjectQuery(query);
            ManagementObjectSearcher sherlok = new ManagementObjectSearcher(scop, Q);
            try
            {
                ManagementObjectCollection col = sherlok.Get();
                foreach (ManagementObject m in col)
                {
                    int reason = int.Parse(m.InvokeMethod("Terminate", null).ToString());
                    switch (reason)
                    {
                        case 0: res = "Succesfylly terminated " + p.Description; break;
                        case 2: res = "Access denied"; break;
                        case 3: res = "Insufficient privilege"; break;
                        case 8: res = "Unknown failure"; break;
                        case 9: res = "Path not found"; break;
                        case 21: res = "Invalid parameter"; break;
                        default: res = "Terminate failed with error code " + reason.ToString(); break;
                    }

                }
            }
            catch (Exception e)
            {
                UR.WriteLog(e.Message);
                res = e.Message;
            }
            return res;
        }

        private static string HandleRequest(XDocument reqXml)
        {
            string answerPath = String.Empty;
            XDocument answer = new XDocument();

            if (reqXml == null)
            {
                UR.WriteLog(String.Format("{0} - reqXml = null", DateTime.Now));
                answer = new XDocument(new XElement("error", "Ya washe hz"));
                answerPath = localPath + UR.Res1;
            }
            else if (reqXml.Root.Name == "path")
            {
                reqXml.Save(localPath + UR.Req1);  
                answerPath = localPath + UR.Res1;

                string path = reqXml.Element("path").IsEmpty ? "" : reqXml.Element("path").Value;
                List<Process> res = GetProcesses(path);

                answer = new XDocument(new XElement("processes"));
                if (res.Count > 0)
                {
                    foreach (Process p in res)
                    {
                        answer.Element("processes").Add(p.ToXml());
                    }
                }
            }
            else if (reqXml.Root.Name == "process")
            {
                reqXml.Save(localPath + UR.Req2);   
                answerPath = localPath + UR.Res2;

                Process proc = new Process(reqXml.Element("process"));
                answer = new XDocument(new XElement("mesg", TerminateProcess(proc)));
            }
            answer.Declaration = new XDeclaration("1.0", "utf-8", null);
            answer.Save(answerPath);
            return answerPath;
        }
        public Service1()
        {
            InitializeComponent();
        }
        
        void WorkerThread()
        {
            mustStop = false;
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, port);
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                listener.Bind(endPoint);
                listener.Listen(10);

                while (!mustStop)
                {
                    using (Socket handler = listener.Accept())
                    {
                        XDocument docReq = UR.ReceiveXML(handler);
                        handler.SendFile(HandleRequest(docReq));
                        handler.Shutdown(SocketShutdown.Both);
                    }
                }
            }
            catch (Exception e)
            {
                UR.WriteLog(e.ToString());
            }
        }

        protected override void OnStart(string[] args)
        {
            T = new Thread(WorkerThread);
            T.Start();
        }

        protected override void OnStop()
        {
            if ((T != null) && (T.IsAlive))
            {
                mustStop = true;
            }
        }
    }
}
