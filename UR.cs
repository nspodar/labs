using System.Xml.Linq;
using System.Net.Sockets;
using System.IO;
using System;
using System.Text;


//Uni resourses for client and server
public static class UR
{
    public static string LocalPath { get { return AppDomain.CurrentDomain.BaseDirectory; } }
    public static string Req1 { get { return "Request-1.xml"; } }
    public static string Req2 { get { return "Request-2.xml"; } }
    public static string Res1 { get { return "Response-1.xml"; } }
    public static string Res2 { get { return "Response-2.xml"; } }

    public static void WriteLog(string z)
    {
        using (StreamWriter SW = new StreamWriter(LocalPath + "log.txt", true, Encoding.UTF8))
        {
            SW.WriteLine("------------------<{0:dd-MM-yyyy/H:m}>---------------------", DateTime.Now);
            SW.WriteLine(z);
        }
    }

    //WARNING! Using with already connected socket
    public static XDocument ReceiveXML(Socket s)
    {
        s.ReceiveTimeout = 3000;
        int received = 0;
        byte[] buff = new byte[1024];
        XDocument XD;
        using (MemoryStream ms = new MemoryStream())
        {
            do
            {
                try
                {
                    received = s.Receive(buff);
                    ms.Write(buff, 0, received);
                }
                catch
                {
                    break;
                }
            }
            while (s.Available > 0);

            //set position to 0 to be sure
            ms.Seek(0, SeekOrigin.Begin);
            try
            {
                XD = XDocument.Load(ms);
            }
            catch (Exception e)
            {
                WriteLog("Receiving" + e.ToString());
                XD = null;
            }
        }
        return XD;
    }
}

public class Process
{
    public string Description { get; }
    public string Path { get; }
    public uint KernelModeTime { get; }

    //using XElement instead of XmlElement class, cuz it can create element instance!
    public XElement ToXml()
    {
        return new XElement("process",
            new XAttribute("Description", Description),
            new XAttribute("ExecutablePath", Path),
            new XAttribute("KernelModeTime", KernelModeTime));
    }

    public Process(XElement element)
    {
        this.Description = element.Attribute("Description").Value.ToString();
        this.Path = element.Attribute("ExecutablePath").Value.ToString();
        this.KernelModeTime = uint.Parse(element.Attribute("KernelModeTime").Value.ToString());
    }

    public Process(string desc, string path, uint kernelTime)
    {
        this.Description = desc;
        this.Path = path;
        this.KernelModeTime = kernelTime;
    }

    public Process() { }
}
