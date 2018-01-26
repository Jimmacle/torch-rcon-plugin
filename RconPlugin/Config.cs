using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace RconPlugin
{
    public class Config
    {
        [XmlIgnore]
        public IPAddress IP { get; set; } = IPAddress.Any;

        [XmlElement("IP")]
        public string IPString { get => IP.ToString(); set => IP = IPAddress.Parse(value); }

        public ushort Port { get; set; } = 27017;
        public string PassHash { get; set; } = "";

        private string _path;

        public Config() { }

        public Config(string path)
        {
            _path = path;
        }

        public void Save()
        {
            using (var f = File.Create(_path))
            {
                var ser = new XmlSerializer(typeof(Config));
                ser.Serialize(f, this);
            }
        }

        public static Config Load(string path)
        {
            var c = new Config(path);
            if (File.Exists(path))
            {
                try
                {
                    using (var f = File.OpenRead(path))
                    {
                        var ser = new XmlSerializer(typeof(Config));
                        c = (Config)ser.Deserialize(f);
                        c._path = path;
                    }
                }
                catch { }
            }
            else
            {
                c._path = path;
                c.Save();
            }
            return c;

        }
    }
}
