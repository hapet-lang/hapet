namespace HapetFrontend.Errors
{
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

        public string GetName() => $"CE{Number}";
    }

    public class XmlMessageReader<T>
    {
        private static XmlMessageReader<XmlCompileTimeError> _compileTimeErrors;

        public static List<XmlCompileTimeError> GetCompileTimeErrors()
        {
            if (_compileTimeErrors == null)
                _compileTimeErrors = new XmlMessageReader<XmlCompileTimeError>();


        }

        private List<T> _data;
    }
}
