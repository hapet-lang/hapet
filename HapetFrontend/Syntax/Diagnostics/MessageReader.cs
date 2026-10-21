using HapetCommon;
using System.Xml;

namespace HapetFrontend.Syntax.Diagnostics
{
    public static class MessageCode
    {
        public static IXmlMessage Get(MessageNumber num)
        {
            var errs = XmlMessageReader<XmlCompileTimeMessage>.GetCompileTimeErrors();
            return errs.First(x => x.RealNumber == (int)num);
        }
    }

    public class XmlMessageReader<T> where T : IXmlMessage, new()
    {
        private static XmlMessageReader<XmlCompileTimeMessage> _compileTimeMessages;

        public static List<XmlCompileTimeMessage> GetCompileTimeErrors()
        {
            _compileTimeMessages ??= new XmlMessageReader<XmlCompileTimeMessage>(XmlCompileTimeMessage.Filename);
            return _compileTimeMessages.ReadData();
        }

        private List<T> _data;
        private readonly string _filePath;

        private XmlMessageReader(string filepath)
        {
            _filePath = filepath;
        }

        private List<T> ReadData()
        {
            // check for cache
            if (_data != null)
                return _data;

            _data = new List<T>();

            string fullPath = Path.Combine(CompilerUtils.CurrentHapetDirectory, "Errors", _filePath);
            if (!File.Exists(fullPath))
                return _data;

            XmlDocument errDoc = new XmlDocument();
            try
            {
                errDoc.Load(fullPath);
            }
            catch
            {
                return _data;
            }
            if (errDoc.DocumentElement == null)
            {
                return _data;
            }

            XmlElement errRoot = errDoc.DocumentElement;
            foreach (var xnode in errRoot.ChildNodes)
            {
                if (xnode is not XmlElement element)
                    continue;

                // check that the tag is Item
                if (element.Name == "Item")
                {
                    T t = new T();
                    foreach (XmlNode cn in element.ChildNodes)
                    {
                        if (cn.Name == "Number")
                            t.Number = cn.InnerText;
                        else if (cn.Name == "Text")
                            t.Text = cn.InnerText;
                    }
                    _data.Add(t);
                }
            }
            return _data;
        }
    }

    #region Entities
    public interface IXmlMessage
    {
        public string Number { get; set; }
        public int RealNumber { get; }
        public string Text { get; set; }
    }

    public class XmlCompileTimeMessage : IXmlMessage
    {
        public string Number { get; set; }

        private int? _realNumber = null;
        public int RealNumber
        {
            get
            {
                if (!_realNumber.HasValue)
                    _realNumber = int.Parse(Number, System.Globalization.NumberStyles.HexNumber);
                return _realNumber.Value;
            }
        }
        public string Text { get; set; }

        internal static string Filename { get; } = "CompileTimeMessages.xml";
    }
    #endregion
}
