using HapetFrontend.Entities;
using System.Xml;

namespace HapetFrontend.Errors
{
    public static class ErrorCode
    {
        public static IXmlMessage Get(CompileTimeErrorNumber num)
        {
            var errs = XmlMessageReader<XmlCompileTimeError>.GetCompileTimeErrors();
            return errs.First(x => x.RealNumber == (int)num);
        }

        public static IXmlMessage Get(RunTimeErrorNumber num)
        {
            var errs = XmlMessageReader<XmlCompileTimeError>.GetCompileTimeErrors();
            return errs.First(x => x.RealNumber == (int)num);
        }

        public static IXmlMessage Get(CompileTimeWarningNumber num)
        {
            var errs = XmlMessageReader<XmlCompileTimeError>.GetCompileTimeErrors();
            return errs.First(x => x.RealNumber == (int)num);
        }
    }

    public interface IXmlMessage
    {
        public string Number { get; set; }
        public int RealNumber { get; }
        public string Text { get; set; }

        string GetName();
    }

    public class XmlCompileTimeError : IXmlMessage
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

        internal static string Filename { get; } = "CompileTimeErrors.xml";

        public string GetName() => $"CE{Number}";
    }

    public class XmlMessageReader<T> where T : IXmlMessage, new() 
    {
        private static XmlMessageReader<XmlCompileTimeError> _compileTimeErrors;

        public static List<XmlCompileTimeError> GetCompileTimeErrors()
        {
            _compileTimeErrors ??= new XmlMessageReader<XmlCompileTimeError>(XmlCompileTimeError.Filename);
            return _compileTimeErrors.ReadData();
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

            string fullPath = $"./Errors/{_filePath}";
            if (!File.Exists(fullPath))
                return _data;

            XmlDocument errDoc = new XmlDocument();
            try
            {
                errDoc.Load(fullPath);
            }
            catch (Exception e)
            {
                return _data;
            }
            if (errDoc.DocumentElement == null)
            {
                return _data;
            }

            XmlElement errRoot = errDoc.DocumentElement;
            foreach (XmlElement xnode in errRoot)
            {
                // check that the tag is Item
                if (xnode.Name == "Item")
                {
                    T t = new T();
                    foreach (XmlNode cn in xnode.ChildNodes)
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
}
