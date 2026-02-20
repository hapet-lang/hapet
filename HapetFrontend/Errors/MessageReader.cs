using HapetFrontend.Entities;
using HapetFrontend.Helpers;
using System.Xml;

namespace HapetFrontend.Errors
{
    public static class ErrorCode
    {
        public static IXmlMessage Get(CTEN num)
        {
            var errs = XmlMessageReader<XmlCompileTimeError>.GetCompileTimeErrors();
            return errs.First(x => x.RealNumber == (int)num);
        }

        public static IXmlMessage Get(RTEN num)
        {
            var errs = XmlMessageReader<XmlRunTimeError>.GetRunTimeErrors();
            return errs.First(x => x.RealNumber == (int)num);
        }

        public static IXmlMessage Get(CTWN num)
        {
            var errs = XmlMessageReader<XmlCompileTimeWarning>.GetCompileTimeWarnings();
            return errs.First(x => x.RealNumber == (int)num);
        }
    }

    public class XmlMessageReader<T> where T : IXmlMessage, new() 
    {
        private static XmlMessageReader<XmlCompileTimeError> _compileTimeErrors;
        private static XmlMessageReader<XmlRunTimeError> _runTimeErrors;
        private static XmlMessageReader<XmlCompileTimeWarning> _compileTimeWarnings;

        public static List<XmlCompileTimeError> GetCompileTimeErrors()
        {
            _compileTimeErrors ??= new XmlMessageReader<XmlCompileTimeError>(XmlCompileTimeError.Filename);
            return _compileTimeErrors.ReadData();
        }

        public static List<XmlRunTimeError> GetRunTimeErrors()
        {
            _runTimeErrors ??= new XmlMessageReader<XmlRunTimeError>(XmlRunTimeError.Filename);
            return _runTimeErrors.ReadData();
        }

        public static List<XmlCompileTimeWarning> GetCompileTimeWarnings()
        {
            _compileTimeWarnings ??= new XmlMessageReader<XmlCompileTimeWarning>(XmlCompileTimeWarning.Filename);
            return _compileTimeWarnings.ReadData();
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

    public class XmlRunTimeError : IXmlMessage
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

        internal static string Filename { get; } = "RunTimeErrors.xml";

        public string GetName() => $"RE{Number}";
    }

    public class XmlCompileTimeWarning : IXmlMessage
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

        internal static string Filename { get; } = "CompileTimeWarnings.xml";

        public string GetName() => $"CW{Number}";
    }
    #endregion
}
